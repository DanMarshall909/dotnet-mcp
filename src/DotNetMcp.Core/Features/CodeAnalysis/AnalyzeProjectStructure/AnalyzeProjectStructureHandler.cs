using System.IO.Abstractions;
using System.Text.Json;
using System.Xml.Linq;
using DotNetMcp.Core.Common;
using DotNetMcp.Core.SharedKernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Handler for analyzing project structure
/// </summary>
public class AnalyzeProjectStructureHandler : BaseHandler<AnalyzeProjectStructureCommand, AnalyzeProjectStructureResponse>
{
    private readonly IFileSystem _fileSystem;

    public AnalyzeProjectStructureHandler(ILogger<AnalyzeProjectStructureHandler> logger, IFileSystem fileSystem) 
        : base(logger)
    {
        _fileSystem = fileSystem;
    }

    protected override async Task<Result<AnalyzeProjectStructureResponse>> HandleAsync(AnalyzeProjectStructureCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate project path exists
            if (!_fileSystem.Directory.Exists(request.ProjectPath))
            {
                return Result<AnalyzeProjectStructureResponse>.Failure($"Project path not found: {request.ProjectPath}");
            }

            // Find project files
            var projectFiles = _fileSystem.Directory
                .GetFiles(request.ProjectPath, "*.csproj", SearchOption.AllDirectories)
                .ToArray();

            if (!projectFiles.Any())
            {
                return Result<AnalyzeProjectStructureResponse>.Failure("No .csproj files found in the project path");
            }

            // Analyze main project (first .csproj found)
            var mainProjectFile = projectFiles.First();
            var projectInfo = await AnalyzeProjectFile(mainProjectFile);

            // Find all C# files
            var csharpFiles = _fileSystem.Directory
                .GetFiles(request.ProjectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .ToArray();

            // Analyze file structure
            var fileInfos = await AnalyzeFiles(csharpFiles);
            
            // Analyze namespaces
            var namespaces = await AnalyzeNamespaces(csharpFiles);

            // Calculate metrics
            var metrics = CalculateMetrics(fileInfos, namespaces);

            // Analyze architecture
            var architecture = await AnalyzeArchitecture(namespaces, fileInfos, request.MaxDepth);

            // Analyze dependencies if requested
            var dependencies = request.IncludeDependencies 
                ? await AnalyzeDependencies(mainProjectFile)
                : null;

            // Analyze test structure if requested
            var testStructure = request.IncludeTestStructure 
                ? await AnalyzeTestStructure(request.ProjectPath)
                : null;

            // Apply token optimization if requested
            var estimatedTokens = EstimateTokens(fileInfos, namespaces, architecture);
            var summarizationApplied = false;

            if (request.OptimizeForTokens && estimatedTokens > request.MaxTokens)
            {
                var optimized = OptimizeForTokens(fileInfos, namespaces, architecture, request.MaxTokens);
                fileInfos = optimized.files;
                namespaces = optimized.namespaces;
                architecture = optimized.architecture;
                estimatedTokens = EstimateTokens(fileInfos, namespaces, architecture);
                summarizationApplied = true;
            }

            var response = new AnalyzeProjectStructureResponse
            {
                ProjectInfo = projectInfo,
                Namespaces = namespaces,
                Files = fileInfos,
                Metrics = metrics,
                Architecture = architecture,
                Dependencies = dependencies,
                TestStructure = testStructure,
                EstimatedTokens = estimatedTokens,
                SummarizationApplied = summarizationApplied
            };

            return Result<AnalyzeProjectStructureResponse>.Success(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error analyzing project structure for {ProjectPath}", request.ProjectPath);
            return Result<AnalyzeProjectStructureResponse>.Failure($"Error analyzing project structure: {ex.Message}", ex);
        }
    }

    private async Task<ProjectInfo> AnalyzeProjectFile(string projectFilePath)
    {
        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(projectFilePath);
            var doc = XDocument.Parse(content);

            var projectName = _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath);
            var framework = doc.Root?.Descendants("TargetFramework").FirstOrDefault()?.Value ??
                           doc.Root?.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
            var version = doc.Root?.Descendants("Version").FirstOrDefault()?.Value;

            var packageReferences = doc.Root?.Descendants("PackageReference")
                .Select(pr => $"{pr.Attribute("Include")?.Value} {pr.Attribute("Version")?.Value}")
                .Where(pr => !string.IsNullOrEmpty(pr))
                .ToArray() ?? Array.Empty<string>();

            var projectReferences = doc.Root?.Descendants("ProjectReference")
                .Select(pr => pr.Attribute("Include")?.Value)
                .Where(pr => !string.IsNullOrEmpty(pr))
                .Cast<string>() // Cast to non-nullable after null filter
                .ToArray() ?? Array.Empty<string>();

            var lastModified = _fileSystem.File.GetLastWriteTime(projectFilePath);

            return new ProjectInfo
            {
                Name = projectName,
                Path = projectFilePath,
                Framework = framework,
                Version = version,
                PackageReferences = packageReferences,
                ProjectReferences = projectReferences,
                LastModified = lastModified
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error analyzing project file {ProjectFile}", projectFilePath);
            return new ProjectInfo
            {
                Name = _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath),
                Path = projectFilePath,
                LastModified = DateTime.MinValue
            };
        }
    }

    private async Task<CodeAnalysis.FileInfo[]> AnalyzeFiles(string[] csharpFiles)
    {
        var fileInfos = new List<CodeAnalysis.FileInfo>();

        foreach (var filePath in csharpFiles)
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = await syntaxTree.GetRootAsync();

                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
                var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
                var lineCount = content.Split('\n').Length;

                var dependencies = ExtractDependencies(root);
                var complexityScore = CalculateFileComplexity(root);
                var lastModified = _fileSystem.File.GetLastWriteTime(filePath);

                fileInfos.Add(new CodeAnalysis.FileInfo
                {
                    Path = filePath,
                    Name = _fileSystem.Path.GetFileName(filePath),
                    LineCount = lineCount,
                    ClassCount = classes,
                    InterfaceCount = interfaces,
                    MethodCount = methods,
                    Dependencies = dependencies,
                    ComplexityScore = complexityScore,
                    LastModified = lastModified
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error analyzing file {FilePath}", filePath);
            }
        }

        return fileInfos.ToArray();
    }

    private async Task<NamespaceInfo[]> AnalyzeNamespaces(string[] csharpFiles)
    {
        var namespaceData = new Dictionary<string, NamespaceData>();

        foreach (var filePath in csharpFiles)
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = await syntaxTree.GetRootAsync();

                var namespaceName = GetNamespace(root);
                if (string.IsNullOrEmpty(namespaceName)) continue;

                if (!namespaceData.ContainsKey(namespaceName))
                {
                    namespaceData[namespaceName] = new NamespaceData();
                }

                var data = namespaceData[namespaceName];
                data.Files.Add(filePath);
                data.ClassCount += root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
                data.InterfaceCount += root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
                data.EnumCount += root.DescendantNodes().OfType<EnumDeclarationSyntax>().Count();
                data.TotalLines += content.Split('\n').Length;
                data.ComplexityScore += CalculateFileComplexity(root);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error analyzing namespace in file {FilePath}", filePath);
            }
        }

        return namespaceData.Select(kvp => new NamespaceInfo
        {
            Name = kvp.Key,
            ClassCount = kvp.Value.ClassCount,
            InterfaceCount = kvp.Value.InterfaceCount,
            EnumCount = kvp.Value.EnumCount,
            Files = kvp.Value.Files.ToArray(),
            TotalLines = kvp.Value.TotalLines,
            ComplexityScore = kvp.Value.ComplexityScore / Math.Max(kvp.Value.Files.Count, 1)
        }).ToArray();
    }

    private class NamespaceData
    {
        public List<string> Files { get; } = new();
        public int ClassCount { get; set; }
        public int InterfaceCount { get; set; }
        public int EnumCount { get; set; }
        public int TotalLines { get; set; }
        public double ComplexityScore { get; set; }
    }

    private ProjectMetrics CalculateMetrics(CodeAnalysis.FileInfo[] files, NamespaceInfo[] namespaces)
    {
        var totalLines = files.Sum(f => f.LineCount);
        var totalClasses = files.Sum(f => f.ClassCount);
        var totalInterfaces = files.Sum(f => f.InterfaceCount);
        var totalMethods = files.Sum(f => f.MethodCount);
        var averageComplexity = files.Average(f => f.ComplexityScore);

        var largestFiles = files
            .OrderByDescending(f => f.LineCount)
            .Take(5)
            .Select(f => $"{f.Name} ({f.LineCount} lines)")
            .ToArray();

        var mostComplexClasses = files
            .OrderByDescending(f => f.ComplexityScore)
            .Take(5)
            .Select(f => $"{f.Name} (score: {f.ComplexityScore:F1})")
            .ToArray();

        // Calculate cohesion and coupling scores (simplified)
        var cohesionScore = CalculateCohesionScore(namespaces);
        var couplingScore = CalculateCouplingScore(files);

        return new ProjectMetrics
        {
            TotalLines = totalLines,
            TotalClasses = totalClasses,
            TotalInterfaces = totalInterfaces,
            TotalMethods = totalMethods,
            TotalFiles = files.Length,
            AverageComplexity = averageComplexity,
            CohesionScore = cohesionScore,
            CouplingScore = couplingScore,
            LargestFiles = largestFiles,
            MostComplexClasses = mostComplexClasses
        };
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators - intentional for future extensibility  
    private async Task<ArchitectureAnalysis> AnalyzeArchitecture(NamespaceInfo[] namespaces, CodeAnalysis.FileInfo[] files, int maxDepth)
    #pragma warning restore CS1998
    {
        // Identify layers based on common patterns
        var layers = IdentifyLayers(namespaces);
        
        // Identify architecture patterns
        var patterns = IdentifyArchitecturePatterns(namespaces, layers);
        
        // Find potential issues
        var issues = IdentifyArchitectureIssues(layers, files);
        
        // Build dependency graph
        var dependencyGraph = BuildDependencyGraph(files, namespaces);
        
        // Calculate architecture score
        var architectureScore = CalculateArchitectureScore(layers, dependencyGraph, issues);

        return new ArchitectureAnalysis
        {
            Layers = layers,
            ArchitecturePatterns = patterns,
            PotentialIssues = issues,
            DependencyGraph = dependencyGraph,
            ArchitectureScore = architectureScore
        };
    }

    private LayerInfo[] IdentifyLayers(NamespaceInfo[] namespaces)
    {
        var layers = new List<LayerInfo>();

        // Common layer patterns
        var layerPatterns = new Dictionary<string, string[]>
        {
            ["Presentation"] = new[] { "Controllers", "Views", "Web", "API", "UI" },
            ["Application"] = new[] { "Services", "Handlers", "Commands", "Queries", "Features" },
            ["Domain"] = new[] { "Domain", "Entities", "Models", "Core" },
            ["Infrastructure"] = new[] { "Data", "Repository", "Infrastructure", "External" },
            ["Tests"] = new[] { "Tests", "Test", "Specs" }
        };

        foreach (var pattern in layerPatterns)
        {
            var matchingNamespaces = namespaces
                .Where(ns => pattern.Value.Any(keyword => 
                    ns.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (matchingNamespaces.Any())
            {
                var dependencies = matchingNamespaces
                    .SelectMany(ns => ns.Files)
                    .SelectMany(file => GetFileDependencies(file))
                    .Distinct()
                    .ToArray();

                layers.Add(new LayerInfo
                {
                    Name = pattern.Key,
                    Namespaces = matchingNamespaces.Select(ns => ns.Name).ToArray(),
                    Dependencies = dependencies,
                    Purpose = GetLayerPurpose(pattern.Key),
                    ClassCount = matchingNamespaces.Sum(ns => ns.ClassCount)
                });
            }
        }

        return layers.ToArray();
    }

    private string[] IdentifyArchitecturePatterns(NamespaceInfo[] namespaces, LayerInfo[] layers)
    {
        var patterns = new List<string>();

        // Check for common patterns
        if (layers.Any(l => l.Name == "Domain") && layers.Any(l => l.Name == "Application"))
        {
            patterns.Add("Clean Architecture");
        }

        if (namespaces.Any(ns => ns.Name.Contains("MediatR", StringComparison.OrdinalIgnoreCase)))
        {
            patterns.Add("CQRS");
        }

        if (namespaces.Any(ns => ns.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase)))
        {
            patterns.Add("Repository Pattern");
        }

        if (layers.Any(l => l.Name == "Presentation") && layers.Any(l => l.Name == "Application"))
        {
            patterns.Add("Layered Architecture");
        }

        return patterns.ToArray();
    }

    private string[] IdentifyArchitectureIssues(LayerInfo[] layers, CodeAnalysis.FileInfo[] files)
    {
        var issues = new List<string>();

        // Check for large files
        var largeFiles = files.Where(f => f.LineCount > 500).Count();
        if (largeFiles > files.Length * 0.1)
        {
            issues.Add($"Too many large files ({largeFiles} files > 500 lines)");
        }

        // Check for high complexity
        var complexFiles = files.Where(f => f.ComplexityScore > 10).Count();
        if (complexFiles > 0)
        {
            issues.Add($"High complexity files detected ({complexFiles} files)");
        }

        // Check for circular dependencies (simplified)
        var circularDeps = DetectCircularDependencies(files);
        if (circularDeps > 0)
        {
            issues.Add($"Potential circular dependencies detected ({circularDeps})");
        }

        return issues.ToArray();
    }

    private DependencyGraph BuildDependencyGraph(CodeAnalysis.FileInfo[] files, NamespaceInfo[] namespaces)
    {
        var nodes = new List<DependencyNode>();
        var edges = new List<DependencyEdge>();

        // Create nodes for namespaces
        foreach (var ns in namespaces)
        {
            nodes.Add(new DependencyNode
            {
                Id = ns.Name,
                Name = ns.Name,
                Type = "namespace",
                InDegree = 0, // Will be calculated later
                OutDegree = 0
            });
        }

        // Create edges based on file dependencies (simplified)
        var nodeDict = nodes.ToDictionary(n => n.Id, n => n);
        foreach (var file in files)
        {
            var fileNamespace = GetNamespaceFromFile(file.Path);
            if (string.IsNullOrEmpty(fileNamespace)) continue;

            foreach (var dependency in file.Dependencies)
            {
                if (nodeDict.ContainsKey(dependency) && dependency != fileNamespace)
                {
                    edges.Add(new DependencyEdge
                    {
                        From = fileNamespace,
                        To = dependency,
                        Type = "uses",
                        Weight = 1
                    });
                }
            }
        }

        // Calculate in/out degrees
        foreach (var edge in edges)
        {
            if (nodeDict.TryGetValue(edge.From, out var fromNode))
            {
                nodeDict[edge.From] = fromNode with { OutDegree = fromNode.OutDegree + 1 };
            }
            if (nodeDict.TryGetValue(edge.To, out var toNode))
            {
                nodeDict[edge.To] = toNode with { InDegree = toNode.InDegree + 1 };
            }
        }

        var circularDependencies = DetectCircularDependencies(files);

        return new DependencyGraph
        {
            Nodes = nodeDict.Values.ToArray(),
            Edges = edges.ToArray(),
            CircularDependencies = circularDependencies
        };
    }

    private async Task<ProjectDependency[]> AnalyzeDependencies(string projectFilePath)
    {
        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(projectFilePath);
            var doc = XDocument.Parse(content);

            var dependencies = new List<ProjectDependency>();

            // Package references
            var packageRefs = doc.Root?.Descendants("PackageReference");
            if (packageRefs != null)
            {
                foreach (var packageRef in packageRefs)
                {
                    var name = packageRef.Attribute("Include")?.Value;
                    var version = packageRef.Attribute("Version")?.Value;
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        dependencies.Add(new ProjectDependency
                        {
                            Name = name,
                            Version = version ?? "unknown",
                            Type = "package",
                            IsTransitive = false
                        });
                    }
                }
            }

            return dependencies.ToArray();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error analyzing dependencies for {ProjectFile}", projectFilePath);
            return Array.Empty<ProjectDependency>();
        }
    }

    private async Task<TestStructureInfo> AnalyzeTestStructure(string projectPath)
    {
        var testProjects = _fileSystem.Directory
            .GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var testFiles = _fileSystem.Directory
            .GetFiles(projectPath, "*Test*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToArray();

        var testFileInfos = new List<TestFileInfo>();
        foreach (var testFile in testFiles)
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(testFile);
                var testMethods = content.Split('\n')
                    .Count(line => line.Contains("[Test]") || line.Contains("[Fact]") || line.Contains("[Theory]"));

                var testedClass = ExtractTestedClass(content);
                
                testFileInfos.Add(new TestFileInfo
                {
                    Path = testFile,
                    TestedClass = testedClass,
                    TestMethodCount = testMethods
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error analyzing test file {TestFile}", testFile);
            }
        }

        return new TestStructureInfo
        {
            TestProjects = testProjects,
            TestFiles = testFileInfos.ToArray(),
            TestCoverage = 0.0, // Would need actual coverage analysis
            UncoveredClasses = Array.Empty<string>(),
            TestFramework = DetectTestFramework(testFiles)
        };
    }

    private static string[] ExtractDependencies(SyntaxNode root)
    {
        return root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray()!;
    }

    private static double CalculateFileComplexity(SyntaxNode root)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
        var conditionals = root.DescendantNodes().Count(n => 
            n.IsKind(SyntaxKind.IfStatement) || 
            n.IsKind(SyntaxKind.SwitchStatement) ||
            n.IsKind(SyntaxKind.ForStatement));

        return methods + classes * 2 + interfaces + conditionals * 0.5;
    }

    private static string GetNamespace(SyntaxNode root)
    {
        var namespaceNode = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceNode != null)
        {
            return namespaceNode.Name.ToString();
        }

        var fileScopedNamespace = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return fileScopedNamespace?.Name.ToString() ?? "Global";
    }

    private string GetNamespaceFromFile(string filePath)
    {
        try
        {
            var content = _fileSystem.File.ReadAllText(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(content);
            var root = syntaxTree.GetRoot();
            return GetNamespace(root);
        }
        catch
        {
            return "";
        }
    }

    private static double CalculateCohesionScore(NamespaceInfo[] namespaces)
    {
        if (!namespaces.Any()) return 0.0;
        
        return namespaces.Average(ns => ns.ClassCount / Math.Max(ns.Files.Length, 1.0));
    }

    private static double CalculateCouplingScore(CodeAnalysis.FileInfo[] files)
    {
        if (!files.Any()) return 0.0;
        
        return files.Average(f => f.Dependencies.Length);
    }

    private static double CalculateArchitectureScore(LayerInfo[] layers, DependencyGraph dependencyGraph, string[] issues)
    {
        var baseScore = 10.0;
        
        // Deduct points for issues
        baseScore -= issues.Length * 0.5;
        
        // Deduct points for circular dependencies
        baseScore -= dependencyGraph.CircularDependencies * 1.0;
        
        // Add points for good layering
        if (layers.Length >= 3 && layers.Length <= 6)
        {
            baseScore += 2.0;
        }
        
        return Math.Max(0, Math.Min(10, baseScore));
    }

    private string[] GetFileDependencies(string filePath)
    {
        try
        {
            var content = _fileSystem.File.ReadAllText(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(content);
            var root = syntaxTree.GetRoot();
            return ExtractDependencies(root);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string GetLayerPurpose(string layerName)
    {
        return layerName switch
        {
            "Presentation" => "User interface and external API endpoints",
            "Application" => "Business logic and use cases",
            "Domain" => "Core business entities and rules",
            "Infrastructure" => "External systems and data access",
            "Tests" => "Unit, integration, and end-to-end tests",
            _ => "Unknown purpose"
        };
    }

    private static int DetectCircularDependencies(CodeAnalysis.FileInfo[] files)
    {
        // Simplified circular dependency detection
        var dependencyMap = files.ToDictionary(f => f.Name, f => f.Dependencies.ToHashSet());
        var visited = new HashSet<string>();
        var circularCount = 0;

        foreach (var file in files)
        {
            if (visited.Contains(file.Name)) continue;
            
            var stack = new HashSet<string>();
            if (HasCircularDependency(file.Name, dependencyMap, visited, stack))
            {
                circularCount++;
            }
        }

        return circularCount;
    }

    private static bool HasCircularDependency(string node, Dictionary<string, HashSet<string>> graph, HashSet<string> visited, HashSet<string> stack)
    {
        if (stack.Contains(node)) return true;
        if (visited.Contains(node)) return false;

        visited.Add(node);
        stack.Add(node);

        if (graph.TryGetValue(node, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (HasCircularDependency(dependency, graph, visited, stack))
                {
                    return true;
                }
            }
        }

        stack.Remove(node);
        return false;
    }

    private static string ExtractTestedClass(string testFileContent)
    {
        // Simple heuristic to extract tested class name
        var lines = testFileContent.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("class") && line.Contains("Test"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.EndsWith("Test") || part.EndsWith("Tests"))
                    {
                        return part.Replace("Test", "").Replace("Tests", "");
                    }
                }
            }
        }
        return "Unknown";
    }

    private static string DetectTestFramework(string[] testFiles)
    {
        foreach (var testFile in testFiles.Take(5)) // Sample a few files
        {
            try
            {
                var content = System.IO.File.ReadAllText(testFile);
                if (content.Contains("using Xunit")) return "xUnit";
                if (content.Contains("using NUnit")) return "NUnit";
                if (content.Contains("using Microsoft.VisualStudio.TestTools")) return "MSTest";
            }
            catch
            {
                // Ignore errors
            }
        }
        return "Unknown";
    }

    private static int EstimateTokens(CodeAnalysis.FileInfo[] files, NamespaceInfo[] namespaces, ArchitectureAnalysis architecture)
    {
        var fileTokens = files.Sum(f => f.Name.Length + f.Path.Length + 100);
        var namespaceTokens = namespaces.Sum(ns => ns.Name.Length + ns.Files.Length * 20);
        var architectureTokens = architecture.Layers.Sum(l => l.Name.Length + l.Purpose.Length + 50);
        
        return fileTokens + namespaceTokens + architectureTokens + 500; // Base overhead
    }

    private static (CodeAnalysis.FileInfo[] files, NamespaceInfo[] namespaces, ArchitectureAnalysis architecture) OptimizeForTokens(
        CodeAnalysis.FileInfo[] files, NamespaceInfo[] namespaces, ArchitectureAnalysis architecture, int maxTokens)
    {
        // Prioritize most important files and namespaces
        var prioritizedFiles = files
            .OrderByDescending(f => f.ClassCount + f.InterfaceCount)
            .Take(20)
            .ToArray();

        var prioritizedNamespaces = namespaces
            .OrderByDescending(ns => ns.ClassCount + ns.InterfaceCount)
            .Take(10)
            .ToArray();

        // Keep architecture analysis but truncate potential issues
        var optimizedArchitecture = architecture with
        {
            PotentialIssues = architecture.PotentialIssues.Take(5).ToArray()
        };

        return (prioritizedFiles, prioritizedNamespaces, optimizedArchitecture);
    }
}
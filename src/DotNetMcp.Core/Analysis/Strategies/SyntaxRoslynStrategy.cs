using System.IO.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Analysis.Strategies;

/// <summary>
/// Syntax-only Roslyn analysis strategy (no compilation required)
/// </summary>
public class SyntaxRoslynStrategy : IAnalysisStrategy
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SyntaxRoslynStrategy> _logger;

    public AnalysisStrategyType Type => AnalysisStrategyType.SyntaxRoslyn;
    public int Priority => 2; // Medium priority

    public SyntaxRoslynStrategy(IFileSystem fileSystem, ILogger<SyntaxRoslynStrategy> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public bool CanHandle(AnalysisRequest request, ProjectContext context)
    {
        // Can handle if we have C# files, regardless of build state
        return context.AvailableFiles.Any(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(request.ProjectPath) && HasCSharpFiles(request.ProjectPath));
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting syntax-only Roslyn analysis for request type: {RequestType}", request.RequestType);

            var syntaxTrees = await ParseSyntaxTreesAsync(request);
            
            if (!syntaxTrees.Any())
            {
                throw new InvalidOperationException("No valid C# syntax trees could be parsed");
            }

            var result = request.RequestType switch
            {
                "find_symbol" => await FindSymbolAsync(request, syntaxTrees),
                "find_symbol_usages" => await FindSymbolUsagesAsync(request, syntaxTrees),
                "get_class_context" => await GetClassContextAsync(request, syntaxTrees),
                "analyze_project_structure" => await AnalyzeProjectStructureAsync(request, syntaxTrees),
                _ => throw new NotSupportedException($"Request type '{request.RequestType}' not supported by syntax-only strategy")
            };

            var executionTime = DateTime.UtcNow - startTime;
            
            return new AnalysisResult
            {
                Success = true,
                Data = result,
                StrategyUsed = Type,
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    ["syntaxTreeCount"] = syntaxTrees.Count,
                    ["analysisType"] = "syntax-only"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Syntax-only Roslyn analysis failed for request type: {RequestType}", request.RequestType);
            
            return new AnalysisResult
            {
                Success = false,
                ErrorMessage = $"Syntax-only analysis failed: {ex.Message}",
                StrategyUsed = Type,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    public AnalysisCapabilities GetCapabilities()
    {
        return new AnalysisCapabilities
        {
            HasSymbolResolution = true, // Basic symbol recognition
            HasTypeInformation = false, // No semantic model
            HasCrossReferences = false, // Limited without compilation
            HasSemanticAnalysis = false,
            HasSyntaxAnalysis = true,
            HasTextMatching = false,
            Performance = PerformanceLevel.Fast,
            Reliability = ReliabilityLevel.Reliable,
            Limitations = new[]
            {
                "No type information available",
                "No cross-reference resolution",
                "No semantic analysis",
                "Cannot resolve using statements",
                "Limited inheritance analysis"
            },
            Strengths = new[]
            {
                "Works without successful compilation",
                "Fast syntax tree parsing",
                "Accurate symbol identification",
                "Good structural analysis",
                "Handles syntax errors gracefully"
            }
        };
    }

    private async Task<List<SyntaxTree>> ParseSyntaxTreesAsync(AnalysisRequest request)
    {
        var syntaxTrees = new List<SyntaxTree>();
        var files = await GetRelevantFilesAsync(request);

        foreach (var filePath in files)
        {
            try
            {
                if (!_fileSystem.File.Exists(filePath))
                    continue;

                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content, path: filePath);
                
                // Check for severe syntax errors that would prevent analysis
                var diagnostics = syntaxTree.GetDiagnostics();
                var severeErrors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                
                if (severeErrors.Length > 10) // Too many syntax errors
                {
                    _logger.LogWarning("Skipping file {FilePath} due to {ErrorCount} syntax errors", filePath, severeErrors.Length);
                    continue;
                }

                syntaxTrees.Add(syntaxTree);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse syntax tree for file: {FilePath}", filePath);
            }
        }

        return syntaxTrees;
    }

    private async Task<object> FindSymbolAsync(AnalysisRequest request, List<SyntaxTree> syntaxTrees)
    {
        var symbolName = request.SymbolName;
        var results = new List<SyntaxSymbolMatch>();

        foreach (var syntaxTree in syntaxTrees)
        {
            var root = await syntaxTree.GetRootAsync();
            var symbols = FindSymbolsInSyntaxTree(root, symbolName, syntaxTree.FilePath);
            results.AddRange(symbols);
        }

        return new SyntaxBasedFindSymbolResult
        {
            SymbolName = symbolName,
            Matches = results.ToArray(),
            Strategy = "syntax-roslyn",
            TotalMatches = results.Count,
            FilesAnalyzed = syntaxTrees.Count
        };
    }

    private async Task<object> FindSymbolUsagesAsync(AnalysisRequest request, List<SyntaxTree> syntaxTrees)
    {
        var symbolName = request.SymbolName;
        var results = new List<SyntaxSymbolUsage>();

        foreach (var syntaxTree in syntaxTrees)
        {
            var root = await syntaxTree.GetRootAsync();
            var usages = FindUsagesInSyntaxTree(root, symbolName, syntaxTree.FilePath);
            results.AddRange(usages);
        }

        return new SyntaxBasedFindUsagesResult
        {
            SymbolName = symbolName,
            Usages = results.ToArray(),
            Strategy = "syntax-roslyn",
            TotalUsages = results.Count,
            FilesAnalyzed = syntaxTrees.Count
        };
    }

    private async Task<object> GetClassContextAsync(AnalysisRequest request, List<SyntaxTree> syntaxTrees)
    {
        var className = request.Parameters.GetValueOrDefault("className", request.SymbolName)?.ToString() ?? "";

        foreach (var syntaxTree in syntaxTrees)
        {
            var root = await syntaxTree.GetRootAsync();
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == className);

            if (classDeclaration != null)
            {
                var methods = classDeclaration.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Select(m => new SyntaxMethodInfo
                    {
                        Name = m.Identifier.ValueText,
                        ReturnType = m.ReturnType.ToString(),
                        Parameters = m.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}").ToArray(),
                        IsPublic = m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)),
                        IsStatic = m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword))
                    }).ToArray();

                var properties = classDeclaration.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Select(p => new SyntaxPropertyInfo
                    {
                        Name = p.Identifier.ValueText,
                        Type = p.Type.ToString(),
                        HasGetter = p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false,
                        HasSetter = p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ?? false,
                        IsPublic = p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword))
                    }).ToArray();

                var baseTypes = classDeclaration.BaseList?.Types
                    .Select(t => t.Type.ToString()).ToArray() ?? Array.Empty<string>();

                return new SyntaxBasedClassContext
                {
                    ClassName = className,
                    FilePath = syntaxTree.FilePath,
                    Methods = methods,
                    Properties = properties,
                    BaseTypes = baseTypes,
                    IsPublic = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)),
                    IsStatic = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)),
                    IsPartial = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword)),
                    Strategy = "syntax-roslyn"
                };
            }
        }

        throw new InvalidOperationException($"Class '{className}' not found in available syntax trees");
    }

    private async Task<object> AnalyzeProjectStructureAsync(AnalysisRequest request, List<SyntaxTree> syntaxTrees)
    {
        var classes = new List<SyntaxTypeInfo>();
        var interfaces = new List<SyntaxTypeInfo>();
        var totalMethods = 0;
        var totalProperties = 0;

        foreach (var syntaxTree in syntaxTrees)
        {
            var root = await syntaxTree.GetRootAsync();

            // Analyze classes
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDeclarations)
            {
                classes.Add(new SyntaxTypeInfo
                {
                    Name = classDecl.Identifier.ValueText,
                    FilePath = syntaxTree.FilePath,
                    IsPublic = classDecl.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)),
                    IsStatic = classDecl.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)),
                    MemberCount = classDecl.DescendantNodes().OfType<MemberDeclarationSyntax>().Count()
                });

                totalMethods += classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
                totalProperties += classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>().Count();
            }

            // Analyze interfaces
            var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            foreach (var interfaceDecl in interfaceDeclarations)
            {
                interfaces.Add(new SyntaxTypeInfo
                {
                    Name = interfaceDecl.Identifier.ValueText,
                    FilePath = syntaxTree.FilePath,
                    IsPublic = interfaceDecl.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)),
                    MemberCount = interfaceDecl.DescendantNodes().OfType<MemberDeclarationSyntax>().Count()
                });
            }
        }

        return new SyntaxBasedProjectStructure
        {
            ProjectPath = request.ProjectPath,
            TotalFiles = syntaxTrees.Count,
            Classes = classes.ToArray(),
            Interfaces = interfaces.ToArray(),
            TotalMethods = totalMethods,
            TotalProperties = totalProperties,
            Strategy = "syntax-roslyn"
        };
    }

    private List<SyntaxSymbolMatch> FindSymbolsInSyntaxTree(SyntaxNode root, string symbolName, string filePath)
    {
        var matches = new List<SyntaxSymbolMatch>();

        // Find class declarations
        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == symbolName);

        foreach (var classDecl in classDeclarations)
        {
            matches.Add(CreateSymbolMatch(classDecl, "class", filePath));
        }

        // Find interface declarations
        var interfaceDeclarations = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .Where(i => i.Identifier.ValueText == symbolName);

        foreach (var interfaceDecl in interfaceDeclarations)
        {
            matches.Add(CreateSymbolMatch(interfaceDecl, "interface", filePath));
        }

        // Find method declarations
        var methodDeclarations = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == symbolName);

        foreach (var methodDecl in methodDeclarations)
        {
            matches.Add(CreateSymbolMatch(methodDecl, "method", filePath));
        }

        // Find property declarations
        var propertyDeclarations = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Identifier.ValueText == symbolName);

        foreach (var propertyDecl in propertyDeclarations)
        {
            matches.Add(CreateSymbolMatch(propertyDecl, "property", filePath));
        }

        return matches;
    }

    private List<SyntaxSymbolUsage> FindUsagesInSyntaxTree(SyntaxNode root, string symbolName, string filePath)
    {
        var usages = new List<SyntaxSymbolUsage>();

        // Find identifier references
        var identifierNames = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(i => i.Identifier.ValueText == symbolName);

        foreach (var identifier in identifierNames)
        {
            var usage = CreateSymbolUsage(identifier, GetUsageType(identifier), filePath);
            usages.Add(usage);
        }

        return usages;
    }

    private SyntaxSymbolMatch CreateSymbolMatch(SyntaxNode node, string symbolType, string filePath)
    {
        var location = node.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new SyntaxSymbolMatch
        {
            FilePath = filePath,
            LineNumber = lineSpan.StartLinePosition.Line + 1,
            LineContent = GetLineContent(node),
            SymbolType = symbolType,
            StartColumn = lineSpan.StartLinePosition.Character,
            Length = node.Span.Length
        };
    }

    private SyntaxSymbolUsage CreateSymbolUsage(SyntaxNode node, string usageType, string filePath)
    {
        var location = node.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new SyntaxSymbolUsage
        {
            FilePath = filePath,
            LineNumber = lineSpan.StartLinePosition.Line + 1,
            LineContent = GetLineContent(node),
            UsageType = usageType,
            StartColumn = lineSpan.StartLinePosition.Character,
            Length = node.Span.Length
        };
    }

    private string GetLineContent(SyntaxNode node)
    {
        var sourceText = node.SyntaxTree.GetText();
        var lineSpan = node.GetLocation().GetLineSpan();
        var line = sourceText.Lines[lineSpan.StartLinePosition.Line];
        return line.ToString().Trim();
    }

    private string GetUsageType(IdentifierNameSyntax identifier)
    {
        var parent = identifier.Parent;
        
        return parent switch
        {
            MemberAccessExpressionSyntax => "member_access",
            InvocationExpressionSyntax => "method_call",
            ObjectCreationExpressionSyntax => "type_instantiation",
            VariableDeclarationSyntax => "variable_declaration",
            TypeSyntax => "type_reference",
            _ => "identifier_reference"
        };
    }

    private async Task<string[]> GetRelevantFilesAsync(AnalysisRequest request)
    {
        if (request.FilePaths.Any())
            return request.FilePaths.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (string.IsNullOrEmpty(request.ProjectPath))
            return Array.Empty<string>();

        if (_fileSystem.File.Exists(request.ProjectPath) && request.ProjectPath.EndsWith(".cs"))
            return new[] { request.ProjectPath };

        if (_fileSystem.Directory.Exists(request.ProjectPath))
        {
            return _fileSystem.Directory
                .GetFiles(request.ProjectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private bool HasCSharpFiles(string path)
    {
        try
        {
            if (_fileSystem.File.Exists(path))
                return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

            if (_fileSystem.Directory.Exists(path))
            {
                return _fileSystem.Directory
                    .GetFiles(path, "*.cs", SearchOption.AllDirectories)
                    .Any(f => !f.Contains("bin") && !f.Contains("obj"));
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }
}

// Response models for syntax-based analysis
public record SyntaxSymbolMatch
{
    public string FilePath { get; init; } = "";
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = "";
    public string SymbolType { get; init; } = "";
    public int StartColumn { get; init; }
    public int Length { get; init; }
}

public record SyntaxSymbolUsage
{
    public string FilePath { get; init; } = "";
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = "";
    public string UsageType { get; init; } = "";
    public int StartColumn { get; init; }
    public int Length { get; init; }
}

public record SyntaxMethodInfo
{
    public string Name { get; init; } = "";
    public string ReturnType { get; init; } = "";
    public string[] Parameters { get; init; } = Array.Empty<string>();
    public bool IsPublic { get; init; }
    public bool IsStatic { get; init; }
}

public record SyntaxPropertyInfo
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public bool IsPublic { get; init; }
}

public record SyntaxTypeInfo
{
    public string Name { get; init; } = "";
    public string FilePath { get; init; } = "";
    public bool IsPublic { get; init; }
    public bool IsStatic { get; init; }
    public int MemberCount { get; init; }
}

public record SyntaxBasedFindSymbolResult
{
    public string SymbolName { get; init; } = "";
    public SyntaxSymbolMatch[] Matches { get; init; } = Array.Empty<SyntaxSymbolMatch>();
    public string Strategy { get; init; } = "";
    public int TotalMatches { get; init; }
    public int FilesAnalyzed { get; init; }
}

public record SyntaxBasedFindUsagesResult
{
    public string SymbolName { get; init; } = "";
    public SyntaxSymbolUsage[] Usages { get; init; } = Array.Empty<SyntaxSymbolUsage>();
    public string Strategy { get; init; } = "";
    public int TotalUsages { get; init; }
    public int FilesAnalyzed { get; init; }
}

public record SyntaxBasedClassContext
{
    public string ClassName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public SyntaxMethodInfo[] Methods { get; init; } = Array.Empty<SyntaxMethodInfo>();
    public SyntaxPropertyInfo[] Properties { get; init; } = Array.Empty<SyntaxPropertyInfo>();
    public string[] BaseTypes { get; init; } = Array.Empty<string>();
    public bool IsPublic { get; init; }
    public bool IsStatic { get; init; }
    public bool IsPartial { get; init; }
    public string Strategy { get; init; } = "";
}

public record SyntaxBasedProjectStructure
{
    public string ProjectPath { get; init; } = "";
    public int TotalFiles { get; init; }
    public SyntaxTypeInfo[] Classes { get; init; } = Array.Empty<SyntaxTypeInfo>();
    public SyntaxTypeInfo[] Interfaces { get; init; } = Array.Empty<SyntaxTypeInfo>();
    public int TotalMethods { get; init; }
    public int TotalProperties { get; init; }
    public string Strategy { get; init; } = "";
}
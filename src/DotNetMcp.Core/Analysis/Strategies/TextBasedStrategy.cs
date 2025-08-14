using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Analysis.Strategies;

/// <summary>
/// Text-based analysis strategy using regex patterns
/// </summary>
public class TextBasedStrategy : IAnalysisStrategy
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<TextBasedStrategy> _logger;

    public AnalysisStrategyType Type => AnalysisStrategyType.TextBased;
    public int Priority => 3; // Low priority (fallback)

    public TextBasedStrategy(IFileSystem fileSystem, ILogger<TextBasedStrategy> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public bool CanHandle(AnalysisRequest request, ProjectContext context)
    {
        // Text-based strategy can always handle requests if files are available
        return context.AvailableFiles.Any() || !string.IsNullOrEmpty(request.ProjectPath);
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting text-based analysis for request type: {RequestType}", request.RequestType);

            var result = request.RequestType switch
            {
                "find_symbol" => await FindSymbolAsync(request),
                "find_symbol_usages" => await FindSymbolUsagesAsync(request),
                "get_class_context" => await GetClassContextAsync(request),
                "analyze_project_structure" => await AnalyzeProjectStructureAsync(request),
                _ => throw new NotSupportedException($"Request type '{request.RequestType}' not supported by text-based strategy")
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
                    ["searchPattern"] = "text-based-regex",
                    ["filesScanned"] = GetFileCount(request)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text-based analysis failed for request type: {RequestType}", request.RequestType);
            
            return new AnalysisResult
            {
                Success = false,
                ErrorMessage = $"Text-based analysis failed: {ex.Message}",
                StrategyUsed = Type,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    public AnalysisCapabilities GetCapabilities()
    {
        return new AnalysisCapabilities
        {
            HasSymbolResolution = false, // Pattern-based only
            HasTypeInformation = false,
            HasCrossReferences = false,
            HasSemanticAnalysis = false,
            HasSyntaxAnalysis = false,
            HasTextMatching = true,
            Performance = PerformanceLevel.Fast,
            Reliability = ReliabilityLevel.Reliable,
            Limitations = new[]
            {
                "No semantic understanding",
                "No type information",
                "No cross-reference resolution",
                "Pattern matching only",
                "May have false positives"
            },
            Strengths = new[]
            {
                "Always works regardless of build state",
                "Fast execution",
                "Good for simple symbol searches",
                "Handles large codebases well"
            }
        };
    }

    private async Task<object> FindSymbolAsync(AnalysisRequest request)
    {
        var symbolName = request.SymbolName;
        var files = await GetRelevantFilesAsync(request);
        var results = new List<SymbolMatch>();

        foreach (var filePath in files)
        {
            var content = await _fileSystem.File.ReadAllTextAsync(filePath);
            var matches = FindSymbolInContent(content, symbolName, filePath);
            results.AddRange(matches);
        }

        return new TextBasedFindSymbolResult
        {
            SymbolName = symbolName,
            Matches = results.ToArray(),
            Strategy = "text-based",
            TotalMatches = results.Count,
            FilesSearched = files.Length
        };
    }

    private async Task<object> FindSymbolUsagesAsync(AnalysisRequest request)
    {
        var symbolName = request.SymbolName;
        var files = await GetRelevantFilesAsync(request);
        var results = new List<SymbolUsage>();

        foreach (var filePath in files)
        {
            var content = await _fileSystem.File.ReadAllTextAsync(filePath);
            var usages = FindUsagesInContent(content, symbolName, filePath);
            results.AddRange(usages);
        }

        return new TextBasedFindUsagesResult
        {
            SymbolName = symbolName,
            Usages = results.ToArray(),
            Strategy = "text-based",
            TotalUsages = results.Count,
            FilesSearched = files.Length
        };
    }

    private async Task<object> GetClassContextAsync(AnalysisRequest request)
    {
        var className = request.Parameters.GetValueOrDefault("className", request.SymbolName)?.ToString() ?? "";
        var files = await GetRelevantFilesAsync(request);
        
        foreach (var filePath in files)
        {
            var content = await _fileSystem.File.ReadAllTextAsync(filePath);
            var classMatch = FindClassDefinition(content, className, filePath);
            
            if (classMatch != null)
            {
                var methods = FindMethodsInClass(content, className);
                var properties = FindPropertiesInClass(content, className);
                var dependencies = FindDependenciesInClass(content);

                return new TextBasedClassContext
                {
                    ClassName = className,
                    FilePath = filePath,
                    Methods = methods,
                    Properties = properties,
                    Dependencies = dependencies,
                    Strategy = "text-based"
                };
            }
        }

        throw new InvalidOperationException($"Class '{className}' not found in available files");
    }

    private async Task<object> AnalyzeProjectStructureAsync(AnalysisRequest request)
    {
        var files = await GetRelevantFilesAsync(request);
        var classes = new List<string>();
        var interfaces = new List<string>();
        var methods = new List<string>();

        foreach (var filePath in files)
        {
            var content = await _fileSystem.File.ReadAllTextAsync(filePath);
            
            classes.AddRange(ExtractClasses(content));
            interfaces.AddRange(ExtractInterfaces(content));
            methods.AddRange(ExtractMethods(content));
        }

        return new TextBasedProjectStructure
        {
            ProjectPath = request.ProjectPath,
            TotalFiles = files.Length,
            Classes = classes.Distinct().ToArray(),
            Interfaces = interfaces.Distinct().ToArray(),
            TotalMethods = methods.Count,
            Strategy = "text-based"
        };
    }

    private async Task<string[]> GetRelevantFilesAsync(AnalysisRequest request)
    {
        if (request.FilePaths.Any())
            return request.FilePaths;

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

    private List<SymbolMatch> FindSymbolInContent(string content, string symbolName, string filePath)
    {
        var matches = new List<SymbolMatch>();
        var lines = content.Split('\n');

        // Class definitions
        var classPattern = $@"(public|private|internal|protected)?\s*(static\s+)?(partial\s+)?class\s+{Regex.Escape(symbolName)}\b";
        FindMatches(lines, classPattern, filePath, "class", matches);

        // Interface definitions
        var interfacePattern = $@"(public|private|internal|protected)?\s*interface\s+{Regex.Escape(symbolName)}\b";
        FindMatches(lines, interfacePattern, filePath, "interface", matches);

        // Method definitions
        var methodPattern = $@"(public|private|internal|protected)?\s*(static\s+)?(async\s+)?\w+\s+{Regex.Escape(symbolName)}\s*\(";
        FindMatches(lines, methodPattern, filePath, "method", matches);

        // Property definitions
        var propertyPattern = $@"(public|private|internal|protected)?\s*(static\s+)?\w+\s+{Regex.Escape(symbolName)}\s*{{\s*(get|set)";
        FindMatches(lines, propertyPattern, filePath, "property", matches);

        return matches;
    }

    private List<SymbolUsage> FindUsagesInContent(string content, string symbolName, string filePath)
    {
        var usages = new List<SymbolUsage>();
        var lines = content.Split('\n');

        // Method calls
        var methodCallPattern = $@"\w+\.{Regex.Escape(symbolName)}\s*\(";
        FindUsageMatches(lines, methodCallPattern, filePath, "method_call", usages);

        // Property access
        var propertyAccessPattern = $@"\w+\.{Regex.Escape(symbolName)}\b";
        FindUsageMatches(lines, propertyAccessPattern, filePath, "property_access", usages);

        // Type usage
        var typeUsagePattern = $@"new\s+{Regex.Escape(symbolName)}\s*\(";
        FindUsageMatches(lines, typeUsagePattern, filePath, "type_instantiation", usages);

        // Variable declarations
        var declarationPattern = $@"{Regex.Escape(symbolName)}\s+\w+";
        FindUsageMatches(lines, declarationPattern, filePath, "variable_declaration", usages);

        return usages;
    }

    private void FindMatches(string[] lines, string pattern, string filePath, string symbolType, List<SymbolMatch> matches)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                matches.Add(new SymbolMatch
                {
                    FilePath = filePath,
                    LineNumber = i + 1,
                    LineContent = lines[i].Trim(),
                    SymbolType = symbolType,
                    StartColumn = match.Index,
                    Length = match.Length
                });
            }
        }
    }

    private void FindUsageMatches(string[] lines, string pattern, string filePath, string usageType, List<SymbolUsage> usages)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                usages.Add(new SymbolUsage
                {
                    FilePath = filePath,
                    LineNumber = i + 1,
                    LineContent = lines[i].Trim(),
                    UsageType = usageType,
                    StartColumn = match.Index,
                    Length = match.Length
                });
            }
        }
    }

    private SymbolMatch? FindClassDefinition(string content, string className, string filePath)
    {
        var lines = content.Split('\n');
        var pattern = $@"(public|private|internal|protected)?\s*(static\s+)?(partial\s+)?class\s+{Regex.Escape(className)}\b";
        
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return new SymbolMatch
                {
                    FilePath = filePath,
                    LineNumber = i + 1,
                    LineContent = lines[i].Trim(),
                    SymbolType = "class",
                    StartColumn = match.Index,
                    Length = match.Length
                };
            }
        }

        return null;
    }

    private string[] FindMethodsInClass(string content, string className)
    {
        var methods = new List<string>();
        var methodPattern = @"(public|private|internal|protected)?\s*(static\s+)?(async\s+)?\w+\s+(\w+)\s*\(";
        var matches = Regex.Matches(content, methodPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 4)
            {
                methods.Add(match.Groups[4].Value);
            }
        }

        return methods.Distinct().ToArray();
    }

    private string[] FindPropertiesInClass(string content, string className)
    {
        var properties = new List<string>();
        var propertyPattern = @"(public|private|internal|protected)?\s*(static\s+)?\w+\s+(\w+)\s*{\s*(get|set)";
        var matches = Regex.Matches(content, propertyPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 3)
            {
                properties.Add(match.Groups[3].Value);
            }
        }

        return properties.Distinct().ToArray();
    }

    private string[] FindDependenciesInClass(string content)
    {
        var dependencies = new List<string>();
        var usingPattern = @"using\s+([\w\.]+);";
        var matches = Regex.Matches(content, usingPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                dependencies.Add(match.Groups[1].Value);
            }
        }

        return dependencies.Distinct().ToArray();
    }

    private string[] ExtractClasses(string content)
    {
        var classes = new List<string>();
        var classPattern = @"(public|private|internal|protected)?\s*(static\s+)?(partial\s+)?class\s+(\w+)";
        var matches = Regex.Matches(content, classPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 4)
            {
                classes.Add(match.Groups[4].Value);
            }
        }

        return classes.ToArray();
    }

    private string[] ExtractInterfaces(string content)
    {
        var interfaces = new List<string>();
        var interfacePattern = @"(public|private|internal|protected)?\s*interface\s+(\w+)";
        var matches = Regex.Matches(content, interfacePattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 2)
            {
                interfaces.Add(match.Groups[2].Value);
            }
        }

        return interfaces.ToArray();
    }

    private string[] ExtractMethods(string content)
    {
        var methods = new List<string>();
        var methodPattern = @"(public|private|internal|protected)?\s*(static\s+)?(async\s+)?\w+\s+(\w+)\s*\(";
        var matches = Regex.Matches(content, methodPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 4)
            {
                methods.Add(match.Groups[4].Value);
            }
        }

        return methods.ToArray();
    }

    private int GetFileCount(AnalysisRequest request)
    {
        if (request.FilePaths.Any())
            return request.FilePaths.Length;

        try
        {
            if (!string.IsNullOrEmpty(request.ProjectPath) && _fileSystem.Directory.Exists(request.ProjectPath))
            {
                return _fileSystem.Directory
                    .GetFiles(request.ProjectPath, "*.cs", SearchOption.AllDirectories)
                    .Count(f => !f.Contains("bin") && !f.Contains("obj"));
            }
        }
        catch
        {
            // Ignore errors
        }

        return 0;
    }
}

// Response models for text-based analysis
public record SymbolMatch
{
    public string FilePath { get; init; } = "";
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = "";
    public string SymbolType { get; init; } = "";
    public int StartColumn { get; init; }
    public int Length { get; init; }
}

public record SymbolUsage
{
    public string FilePath { get; init; } = "";
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = "";
    public string UsageType { get; init; } = "";
    public int StartColumn { get; init; }
    public int Length { get; init; }
}

public record TextBasedFindSymbolResult
{
    public string SymbolName { get; init; } = "";
    public SymbolMatch[] Matches { get; init; } = Array.Empty<SymbolMatch>();
    public string Strategy { get; init; } = "";
    public int TotalMatches { get; init; }
    public int FilesSearched { get; init; }
}

public record TextBasedFindUsagesResult
{
    public string SymbolName { get; init; } = "";
    public SymbolUsage[] Usages { get; init; } = Array.Empty<SymbolUsage>();
    public string Strategy { get; init; } = "";
    public int TotalUsages { get; init; }
    public int FilesSearched { get; init; }
}

public record TextBasedClassContext
{
    public string ClassName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string[] Methods { get; init; } = Array.Empty<string>();
    public string[] Properties { get; init; } = Array.Empty<string>();
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public string Strategy { get; init; } = "";
}

public record TextBasedProjectStructure
{
    public string ProjectPath { get; init; } = "";
    public int TotalFiles { get; init; }
    public string[] Classes { get; init; } = Array.Empty<string>();
    public string[] Interfaces { get; init; } = Array.Empty<string>();
    public int TotalMethods { get; init; }
    public string Strategy { get; init; } = "";
}
using System.IO.Abstractions;
using DotNetMcp.Core.Common.Errors;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Services;

/// <summary>
/// Service for analyzing and creating structured error responses
/// </summary>
public class ErrorAnalysisService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ErrorAnalysisService> _logger;

    public ErrorAnalysisService(IFileSystem fileSystem, ILogger<ErrorAnalysisService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes files for potential duplicate issues
    /// </summary>
    public async Task<DuplicateFilesError?> AnalyzeDuplicateFilesAsync(string[] filePaths)
    {
        var filesByName = new Dictionary<string, List<string>>();
        
        foreach (var filePath in filePaths)
        {
            var fileName = _fileSystem.Path.GetFileName(filePath);
            if (!filesByName.ContainsKey(fileName))
            {
                filesByName[fileName] = new List<string>();
            }
            filesByName[fileName].Add(filePath);
        }

        var duplicates = filesByName
            .Where(kvp => kvp.Value.Count > 1)
            .ToArray();

        if (!duplicates.Any())
        {
            return null;
        }

        var duplicateInfos = new List<DuplicateFileInfo>();

        foreach (var (fileName, locations) in duplicates)
        {
            var projects = locations
                .Select(path => ExtractProjectName(path))
                .Distinct()
                .ToArray();

            var identicalContent = await CheckIdenticalContentAsync(locations);
            var resolution = SuggestResolution(fileName, locations, identicalContent);

            duplicateInfos.Add(new DuplicateFileInfo
            {
                FileName = fileName,
                Locations = locations.ToArray(),
                Projects = projects,
                IdenticalContent = identicalContent,
                SuggestedResolution = resolution
            });
        }

        var resolutionStrategies = GenerateResolutionStrategies(duplicateInfos);

        return new DuplicateFilesError
        {
            DuplicateFiles = duplicateInfos.ToArray(),
            AffectedFileCount = duplicates.Sum(d => d.Value.Count),
            ResolutionStrategies = resolutionStrategies
        };
    }

    /// <summary>
    /// Creates build validation error from build result
    /// </summary>
    public BuildValidationError CreateBuildValidationError(BuildValidationResult buildResult)
    {
        var errorCategories = CategorizeBuildErrors(buildResult.Errors);
        var failedProjects = ExtractFailedProjects(buildResult.Errors);
        var errorSummary = CreateErrorSummary(buildResult.Errors, errorCategories);

        return new BuildValidationError
        {
            CompilationErrors = buildResult.Errors.ToArray(),
            ErrorSummary = errorSummary,
            ErrorCount = buildResult.Errors.Count,
            WarningCount = 0, // Would need to parse warnings from build output
            FailedProjects = failedProjects,
            CommonErrorTypes = errorCategories
        };
    }

    /// <summary>
    /// Creates project discovery error
    /// </summary>
    public ProjectDiscoveryError CreateProjectDiscoveryError(string path, string reason, DiscoveryType discoveryType)
    {
        var foundFiles = DiscoverAvailableFiles(path);
        var suggestedPaths = SuggestAlternativePaths(path, foundFiles);

        return new ProjectDiscoveryError
        {
            AttemptedPath = path,
            FailureReason = reason,
            DiscoveryType = discoveryType,
            FoundFiles = foundFiles,
            SuggestedPaths = suggestedPaths
        };
    }

    /// <summary>
    /// Creates configuration error
    /// </summary>
    public ConfigurationError CreateConfigurationError(string parameter, string providedValue, string expectedFormat, string issue)
    {
        var exampleValues = GenerateExampleValues(parameter, expectedFormat);

        return new ConfigurationError
        {
            ConfigurationIssue = issue,
            InvalidParameter = parameter,
            ProvidedValue = providedValue,
            ExpectedFormat = expectedFormat,
            ExampleValues = exampleValues
        };
    }

    /// <summary>
    /// Creates resource limit error
    /// </summary>
    public ResourceLimitError CreateResourceLimitError(ResourceLimitType limitType, string limitValue, string currentUsage)
    {
        var adjustments = SuggestResourceAdjustments(limitType, limitValue, currentUsage);

        return new ResourceLimitError
        {
            LimitType = limitType,
            LimitValue = limitValue,
            CurrentUsage = currentUsage,
            SuggestedAdjustments = adjustments
        };
    }

    /// <summary>
    /// Creates analysis context from current operation
    /// </summary>
    public AnalysisContext CreateAnalysisContext(string projectPath, string analysisType, int filesProcessed, string failurePoint, TimeSpan elapsed)
    {
        return new AnalysisContext
        {
            ProjectPath = projectPath,
            AnalysisType = analysisType,
            FilesProcessed = filesProcessed,
            FailurePoint = failurePoint,
            ElapsedTime = elapsed,
            Metadata = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["machineName"] = Environment.MachineName,
                ["workingDirectory"] = Environment.CurrentDirectory
            }
        };
    }

    private string ExtractProjectName(string filePath)
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            return "Unknown";

        // Look for .csproj in the directory or parent directories
        var current = directory;
        while (!string.IsNullOrEmpty(current))
        {
            var projectFiles = _fileSystem.Directory.GetFiles(current, "*.csproj");
            if (projectFiles.Any())
            {
                return _fileSystem.Path.GetFileNameWithoutExtension(projectFiles.First());
            }
            current = _fileSystem.Path.GetDirectoryName(current);
        }

        return _fileSystem.Path.GetFileName(directory) ?? "Unknown";
    }

    private async Task<bool> CheckIdenticalContentAsync(List<string> filePaths)
    {
        if (filePaths.Count < 2)
            return true;

        try
        {
            var firstContent = await _fileSystem.File.ReadAllTextAsync(filePaths[0]);
            
            for (int i = 1; i < filePaths.Count; i++)
            {
                var content = await _fileSystem.File.ReadAllTextAsync(filePaths[i]);
                if (content != firstContent)
                    return false;
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string SuggestResolution(string fileName, List<string> locations, bool identicalContent)
    {
        return fileName.ToLower() switch
        {
            "globalusings.cs" when identicalContent => "Consider consolidating global usings into a single shared project",
            "globalusings.cs" => "Review global using differences and standardize across projects",
            "assemblyinfo.cs" => "AssemblyInfo files should be unique per project - ensure different assembly attributes",
            _ when identicalContent => "Consider moving shared code to a common library",
            _ => "Review file differences and ensure unique naming or consolidate if appropriate"
        };
    }

    private string[] GenerateResolutionStrategies(List<DuplicateFileInfo> duplicates)
    {
        var strategies = new List<string>
        {
            "Analyze individual projects instead of the full solution",
            "Use text-based analysis tools for large solutions",
            "Consider using the optimizeForTokens option to reduce compilation overhead"
        };

        if (duplicates.Any(d => d.FileName.ToLower() == "globalusings.cs"))
        {
            strategies.Add("Consolidate global using statements into a shared project");
        }

        if (duplicates.Count > 5)
        {
            strategies.Add("Break analysis into smaller chunks by project or directory");
        }

        return strategies.ToArray();
    }

    private ErrorCategory[] CategorizeBuildErrors(List<BuildError> errors)
    {
        var categories = errors
            .GroupBy(e => CategorizeBuildError(e.Code))
            .Select(g => new ErrorCategory
            {
                Category = g.Key,
                Count = g.Count(),
                Description = GetCategoryDescription(g.Key),
                SuggestedFix = GetCategorySuggestedFix(g.Key)
            })
            .ToArray();

        return categories;
    }

    private string CategorizeBuildError(string errorCode)
    {
        return errorCode switch
        {
            "CS0246" => "Missing References",
            "CS0103" => "Undefined Names",
            "CS1061" => "Missing Members",
            "CS0117" => "Incorrect Member Access",
            "CS0029" => "Type Conversion",
            "CS1002" => "Syntax Errors",
            _ => "Other"
        };
    }

    private string GetCategoryDescription(string category)
    {
        return category switch
        {
            "Missing References" => "Required assemblies or packages are not referenced",
            "Undefined Names" => "Variables, types, or namespaces are not defined or imported",
            "Missing Members" => "Methods, properties, or fields do not exist on the target type",
            "Incorrect Member Access" => "Accessing members that don't exist or are inaccessible",
            "Type Conversion" => "Incompatible type assignments or conversions",
            "Syntax Errors" => "Code syntax is invalid",
            _ => "Various compilation errors"
        };
    }

    private string GetCategorySuggestedFix(string category)
    {
        return category switch
        {
            "Missing References" => "Add required package references or assembly references",
            "Undefined Names" => "Add missing using statements or fix typos in names",
            "Missing Members" => "Check API documentation for correct member names",
            "Incorrect Member Access" => "Verify member exists and is accessible",
            "Type Conversion" => "Add explicit type conversions or fix type mismatches",
            "Syntax Errors" => "Review code syntax and fix structural issues",
            _ => "Review and fix compilation errors"
        };
    }

    private string[] ExtractFailedProjects(List<BuildError> errors)
    {
        return errors
            .Select(e => ExtractProjectName(e.File))
            .Distinct()
            .ToArray();
    }

    private string CreateErrorSummary(List<BuildError> errors, ErrorCategory[] categories)
    {
        if (!categories.Any())
            return "Multiple compilation errors detected";

        var topCategory = categories.OrderByDescending(c => c.Count).First();
        return $"Primary issue: {topCategory.Category} ({topCategory.Count} errors). {topCategory.SuggestedFix}";
    }

    private string[] DiscoverAvailableFiles(string path)
    {
        try
        {
            if (_fileSystem.File.Exists(path))
                return new[] { path };

            if (_fileSystem.Directory.Exists(path))
            {
                var projects = _fileSystem.Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories);
                var solutions = _fileSystem.Directory.GetFiles(path, "*.sln", SearchOption.TopDirectoryOnly);
                return projects.Concat(solutions).ToArray();
            }

            return Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private string[] SuggestAlternativePaths(string attemptedPath, string[] foundFiles)
    {
        var suggestions = new List<string>();

        if (foundFiles.Any())
        {
            suggestions.AddRange(foundFiles.Take(3));
        }

        var parent = _fileSystem.Path.GetDirectoryName(attemptedPath);
        if (!string.IsNullOrEmpty(parent) && _fileSystem.Directory.Exists(parent))
        {
            suggestions.Add($"Parent directory: {parent}");
        }

        return suggestions.ToArray();
    }

    private string[] GenerateExampleValues(string parameter, string expectedFormat)
    {
        return parameter.ToLower() switch
        {
            "projectpath" => new[] { "/path/to/project.csproj", "/path/to/solution.sln", "/path/to/directory" },
            "symbolname" => new[] { "MyClass", "MyMethod", "MyNamespace.MyType" },
            "maxtokens" => new[] { "1000", "2000", "5000" },
            "maxresults" => new[] { "10", "50", "100" },
            _ => new[] { expectedFormat }
        };
    }

    private Dictionary<string, object> SuggestResourceAdjustments(ResourceLimitType limitType, string limitValue, string currentUsage)
    {
        return limitType switch
        {
            ResourceLimitType.TokenCount => new()
            {
                ["maxTokens"] = "Increase to 5000 or use optimizeForTokens=true",
                ["optimizeForTokens"] = true,
                ["alternative"] = "Use text-based analysis"
            },
            ResourceLimitType.FileCount => new()
            {
                ["maxFiles"] = "Analyze smaller directory scope",
                ["alternative"] = "Analyze individual projects"
            },
            ResourceLimitType.ResultCount => new()
            {
                ["maxResults"] = "Increase limit or use more specific search terms"
            },
            _ => new()
        };
    }
}
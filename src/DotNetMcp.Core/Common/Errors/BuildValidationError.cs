using DotNetMcp.Core.Services;

namespace DotNetMcp.Core.Common.Errors;

/// <summary>
/// Error when build validation fails before analysis
/// </summary>
public record BuildValidationError : AnalysisError
{
    public override string Code => "BUILD_VALIDATION_FAILED";
    
    public override string Message => 
        $"Build validation failed with {ErrorCount} error(s). {ErrorSummary}";
    
    public override string Suggestion => 
        "Fix compilation errors before running analysis, or use text-based analysis if semantic analysis isn't required";
    
    public override string[] Alternatives => new[]
    {
        "find_symbol with optimizeForTokens=true",
        "analyze_project_structure",
        "find_symbol_usages with text-based search"
    };
    
    public override ErrorSeverity Severity => ErrorSeverity.Error;
    public override bool CanRetry => false; // Must fix build first
    
    /// <summary>
    /// Build errors from the compilation
    /// </summary>
    public BuildError[] CompilationErrors { get; init; } = Array.Empty<BuildError>();
    
    /// <summary>
    /// Summary of the most critical errors
    /// </summary>
    public string ErrorSummary { get; init; } = "";
    
    /// <summary>
    /// Number of errors found
    /// </summary>
    public int ErrorCount { get; init; }
    
    /// <summary>
    /// Number of warnings found
    /// </summary>
    public int WarningCount { get; init; }
    
    /// <summary>
    /// Projects that failed to build
    /// </summary>
    public string[] FailedProjects { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Most common error types to help with resolution
    /// </summary>
    public ErrorCategory[] CommonErrorTypes { get; init; } = Array.Empty<ErrorCategory>();
}

/// <summary>
/// Categorized build error information
/// </summary>
public record ErrorCategory
{
    public string Category { get; init; } = "";
    public int Count { get; init; }
    public string Description { get; init; } = "";
    public string SuggestedFix { get; init; } = "";
}
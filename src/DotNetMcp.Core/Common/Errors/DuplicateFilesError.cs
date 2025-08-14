namespace DotNetMcp.Core.Common.Errors;

/// <summary>
/// Error when duplicate file names prevent Roslyn compilation
/// </summary>
public record DuplicateFilesError : AnalysisError
{
    public override string Code => "DUPLICATE_FILES_DETECTED";
    
    public override string Message => 
        $"Analysis failed due to {DuplicateFiles.Length} duplicate file name(s) in the compilation";
    
    public override string Suggestion => 
        "Try analyzing individual projects instead of the full solution, or use text-based analysis";
    
    public override string[] Alternatives => new[]
    {
        "analyze_project_structure",
        "find_symbol_usages with optimizeForTokens=true",
        "get_class_context with smaller scope"
    };
    
    public override ErrorSeverity Severity => ErrorSeverity.Warning;
    public override bool CanRetry => true;
    
    /// <summary>
    /// Information about each duplicate file detected
    /// </summary>
    public DuplicateFileInfo[] DuplicateFiles { get; init; } = Array.Empty<DuplicateFileInfo>();
    
    /// <summary>
    /// Total number of files that would be affected
    /// </summary>
    public int AffectedFileCount { get; init; }
    
    /// <summary>
    /// Suggestions for resolving each duplicate
    /// </summary>
    public string[] ResolutionStrategies { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Information about a specific duplicate file
/// </summary>
public record DuplicateFileInfo
{
    /// <summary>
    /// The duplicate file name (e.g., "GlobalUsings.cs")
    /// </summary>
    public string FileName { get; init; } = "";
    
    /// <summary>
    /// All locations where this file name appears
    /// </summary>
    public string[] Locations { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// The projects these files belong to
    /// </summary>
    public string[] Projects { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether the files have identical content
    /// </summary>
    public bool IdenticalContent { get; init; }
    
    /// <summary>
    /// Suggested resolution for this specific duplicate
    /// </summary>
    public string SuggestedResolution { get; init; } = "";
}
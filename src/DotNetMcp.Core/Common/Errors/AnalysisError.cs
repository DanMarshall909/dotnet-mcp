namespace DotNetMcp.Core.Common.Errors;

/// <summary>
/// Base class for all analysis-specific errors with structured information
/// </summary>
public abstract record AnalysisError
{
    /// <summary>
    /// Unique error code for programmatic handling
    /// </summary>
    public abstract string Code { get; }
    
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public abstract string Message { get; }
    
    /// <summary>
    /// Actionable suggestion for resolving the error
    /// </summary>
    public abstract string Suggestion { get; }
    
    /// <summary>
    /// Alternative tools or approaches to try
    /// </summary>
    public abstract string[] Alternatives { get; }
    
    /// <summary>
    /// Severity level of the error
    /// </summary>
    public virtual ErrorSeverity Severity { get; } = ErrorSeverity.Error;
    
    /// <summary>
    /// Whether this error allows for automatic retry with different strategy
    /// </summary>
    public virtual bool CanRetry { get; } = false;
    
    /// <summary>
    /// Additional metadata specific to the error type
    /// </summary>
    public virtual Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Error severity levels
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Context information about when and where an error occurred
/// </summary>
public record AnalysisContext
{
    public string ProjectPath { get; init; } = "";
    public string AnalysisType { get; init; } = "";
    public int FilesProcessed { get; init; } = 0;
    public string FailurePoint { get; init; } = "";
    public TimeSpan ElapsedTime { get; init; } = TimeSpan.Zero;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Comprehensive error response with context and recovery information
/// </summary>
public record ErrorResponse
{
    public string QuickSummary { get; init; } = "";
    public AnalysisError DetailedError { get; init; } = new GenericAnalysisError();
    public AnalysisContext Context { get; init; } = new();
    public string[] RecommendedActions { get; init; } = Array.Empty<string>();
    public bool CanRetryWithDifferentStrategy { get; init; } = false;
}

/// <summary>
/// Generic analysis error for cases where specific error types aren't applicable
/// </summary>
public record GenericAnalysisError : AnalysisError
{
    public override string Code => "GENERIC_ANALYSIS_ERROR";
    
    private readonly string _message;
    private readonly string _suggestion;
    private readonly string[] _alternatives;
    
    public override string Message => _message;
    public override string Suggestion => _suggestion;
    public override string[] Alternatives => _alternatives;
    
    public GenericAnalysisError() 
    {
        _message = "An analysis error occurred";
        _suggestion = "Review the error details and try a different approach";
        _alternatives = Array.Empty<string>();
    }
    
    public GenericAnalysisError(string message, string suggestion = "", string[]? alternatives = null)
    {
        _message = message;
        _suggestion = suggestion.IsNullOrEmpty() ? "Review the error details and try a different approach" : suggestion;
        _alternatives = alternatives ?? Array.Empty<string>();
    }
}
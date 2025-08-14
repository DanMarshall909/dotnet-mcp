namespace DotNetMcp.Core.Common.Errors;

/// <summary>
/// Error when project or solution discovery fails
/// </summary>
public record ProjectDiscoveryError : AnalysisError
{
    public override string Code => "PROJECT_DISCOVERY_FAILED";
    
    public override string Message => 
        $"Failed to discover projects: {FailureReason}";
    
    public override string Suggestion => 
        "Verify the path exists and contains valid .NET projects or solution files";
    
    public override string[] Alternatives => new[]
    {
        "Try specifying a .csproj file directly",
        "Use a different project path",
        "Check file permissions"
    };
    
    public override ErrorSeverity Severity => ErrorSeverity.Error;
    public override bool CanRetry => true;
    
    /// <summary>
    /// The path that failed discovery
    /// </summary>
    public string AttemptedPath { get; init; } = "";
    
    /// <summary>
    /// Specific reason for the failure
    /// </summary>
    public string FailureReason { get; init; } = "";
    
    /// <summary>
    /// What type of discovery was attempted
    /// </summary>
    public DiscoveryType DiscoveryType { get; init; } = DiscoveryType.Auto;
    
    /// <summary>
    /// Files that were found (if any)
    /// </summary>
    public string[] FoundFiles { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Suggested alternative paths to try
    /// </summary>
    public string[] SuggestedPaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Configuration error when parameters are invalid
/// </summary>
public record ConfigurationError : AnalysisError
{
    public override string Code => "INVALID_CONFIGURATION";
    
    public override string Message => 
        $"Invalid configuration: {ConfigurationIssue}";
    
    public override string Suggestion => 
        "Review the tool parameters and ensure all required fields are provided with valid values";
    
    public override string[] Alternatives => new[]
    {
        "Check the tool documentation for required parameters",
        "Use default values for optional parameters",
        "Validate file paths exist and are accessible"
    };
    
    public override ErrorSeverity Severity => ErrorSeverity.Error;
    public override bool CanRetry => true;
    
    /// <summary>
    /// Description of the configuration problem
    /// </summary>
    public string ConfigurationIssue { get; init; } = "";
    
    /// <summary>
    /// The parameter or setting that is invalid
    /// </summary>
    public string InvalidParameter { get; init; } = "";
    
    /// <summary>
    /// The value that was provided
    /// </summary>
    public string ProvidedValue { get; init; } = "";
    
    /// <summary>
    /// Expected value format or range
    /// </summary>
    public string ExpectedFormat { get; init; } = "";
    
    /// <summary>
    /// Valid example values
    /// </summary>
    public string[] ExampleValues { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Error when memory or performance limits are exceeded
/// </summary>
public record ResourceLimitError : AnalysisError
{
    public override string Code => "RESOURCE_LIMIT_EXCEEDED";
    
    public override string Message => 
        $"Analysis exceeded {LimitType} limit: {LimitValue}";
    
    public override string Suggestion => 
        "Try reducing the scope of analysis or use a more targeted approach";
    
    public override string[] Alternatives => new[]
    {
        "Use optimizeForTokens=true to reduce output",
        "Analyze individual projects instead of full solution",
        "Increase maxResults or maxTokens limits",
        "Use text-based analysis instead of semantic analysis"
    };
    
    public override ErrorSeverity Severity => ErrorSeverity.Warning;
    public override bool CanRetry => true;
    
    /// <summary>
    /// Type of limit that was exceeded
    /// </summary>
    public ResourceLimitType LimitType { get; init; } = ResourceLimitType.Memory;
    
    /// <summary>
    /// The limit value that was exceeded
    /// </summary>
    public string LimitValue { get; init; } = "";
    
    /// <summary>
    /// Current usage when limit was hit
    /// </summary>
    public string CurrentUsage { get; init; } = "";
    
    /// <summary>
    /// Suggested limit adjustments
    /// </summary>
    public Dictionary<string, object> SuggestedAdjustments { get; init; } = new();
}

/// <summary>
/// Types of project discovery
/// </summary>
public enum DiscoveryType
{
    Auto,
    Solution,
    Project,
    Directory
}

/// <summary>
/// Types of resource limits
/// </summary>
public enum ResourceLimitType
{
    Memory,
    Time,
    FileCount,
    TokenCount,
    ResultCount
}
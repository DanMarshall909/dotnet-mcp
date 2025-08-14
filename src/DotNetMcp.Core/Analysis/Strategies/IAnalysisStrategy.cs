namespace DotNetMcp.Core.Analysis.Strategies;

/// <summary>
/// Analysis strategy enumeration
/// </summary>
public enum AnalysisStrategyType
{
    SemanticRoslyn,    // Full semantic analysis (preferred)
    SyntaxRoslyn,      // Syntax-only analysis (no compilation)
    TextBased,         // Regex/text pattern matching
    Hybrid             // Combination of strategies
}

/// <summary>
/// Base interface for analysis strategies
/// </summary>
public interface IAnalysisStrategy
{
    /// <summary>
    /// The type of this strategy
    /// </summary>
    AnalysisStrategyType Type { get; }
    
    /// <summary>
    /// Priority order (lower = higher priority)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Determines if this strategy can handle the given request
    /// </summary>
    bool CanHandle(AnalysisRequest request, ProjectContext context);
    
    /// <summary>
    /// Performs the analysis using this strategy
    /// </summary>
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the capabilities of this strategy
    /// </summary>
    AnalysisCapabilities GetCapabilities();
}

/// <summary>
/// Generic analysis request
/// </summary>
public record AnalysisRequest
{
    public string RequestType { get; init; } = "";
    public string ProjectPath { get; init; } = "";
    public string SymbolName { get; init; } = "";
    public string[] FilePaths { get; init; } = Array.Empty<string>();
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Project context information
/// </summary>
public record ProjectContext
{
    public bool CanCompile { get; init; } = false;
    public bool HasBuildErrors { get; init; } = false;
    public int FileCount { get; init; } = 0;
    public string[] AvailableFiles { get; init; } = Array.Empty<string>();
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromMinutes(2);
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Analysis result with strategy information
/// </summary>
public record AnalysisResult
{
    public bool Success { get; init; } = false;
    public string ErrorMessage { get; init; } = "";
    public object? Data { get; init; }
    public AnalysisStrategyType StrategyUsed { get; init; }
    public AnalysisCapabilities Capabilities { get; init; } = new();
    public TimeSpan ExecutionTime { get; init; } = TimeSpan.Zero;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Capabilities of an analysis strategy
/// </summary>
public record AnalysisCapabilities
{
    public bool HasSymbolResolution { get; init; } = false;
    public bool HasTypeInformation { get; init; } = false;
    public bool HasCrossReferences { get; init; } = false;
    public bool HasSemanticAnalysis { get; init; } = false;
    public bool HasSyntaxAnalysis { get; init; } = false;
    public bool HasTextMatching { get; init; } = false;
    public PerformanceLevel Performance { get; init; } = PerformanceLevel.Medium;
    public ReliabilityLevel Reliability { get; init; } = ReliabilityLevel.Medium;
    public string[] Limitations { get; init; } = Array.Empty<string>();
    public string[] Strengths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Performance levels
/// </summary>
public enum PerformanceLevel
{
    Slow,
    Medium,
    Fast
}

/// <summary>
/// Reliability levels
/// </summary>
public enum ReliabilityLevel
{
    Fragile,
    Medium,
    Reliable
}

/// <summary>
/// Exception thrown when all strategies fail
/// </summary>
public class AllStrategiesFailedException : Exception
{
    public string[] FailedStrategies { get; }
    public Dictionary<string, Exception> StrategyExceptions { get; }
    
    public AllStrategiesFailedException(string[] failedStrategies, Dictionary<string, Exception> exceptions)
        : base($"All analysis strategies failed: {string.Join(", ", failedStrategies)}")
    {
        FailedStrategies = failedStrategies;
        StrategyExceptions = exceptions;
    }
}
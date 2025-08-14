using DotNetMcp.Core.Common;
using DotNetMcp.Core.SharedKernel;
using MediatR;

namespace DotNetMcp.Core.Features.AutoFix;

/// <summary>
/// Command to apply automatic fixes to code
/// </summary>
public record AutoFixCommand : IAppRequest<AutoFixResponse>
{
    /// <summary>
    /// Source code to fix
    /// </summary>
    public string Code { get; init; } = "";
    
    /// <summary>
    /// File path (optional, for context)
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// Build errors to fix (optional)
    /// </summary>
    public string[] BuildErrors { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Types of fixes to apply
    /// </summary>
    public AutoFixTypes FixTypes { get; init; } = AutoFixTypes.All;
    
    /// <summary>
    /// Whether to apply fixes automatically or just suggest them
    /// </summary>
    public bool ApplyFixes { get; init; } = true;
    
    /// <summary>
    /// Maximum number of fixes to apply (safety limit)
    /// </summary>
    public int MaxFixes { get; init; } = 50;
}

/// <summary>
/// Response from auto-fix operation
/// </summary>
public record AutoFixResponse
{
    /// <summary>
    /// Fixed code (if ApplyFixes was true)
    /// </summary>
    public string? FixedCode { get; init; }
    
    /// <summary>
    /// List of fixes that were applied or suggested
    /// </summary>
    public AppliedFix[] AppliedFixes { get; init; } = Array.Empty<AppliedFix>();
    
    /// <summary>
    /// Fixes that were suggested but not applied
    /// </summary>
    public SuggestedFix[] SuggestedFixes { get; init; } = Array.Empty<SuggestedFix>();
    
    /// <summary>
    /// Summary of the auto-fix operation
    /// </summary>
    public AutoFixSummary Summary { get; init; } = new();
    
    /// <summary>
    /// Any warnings or issues encountered
    /// </summary>
    public string[] Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Information about a fix that was applied
/// </summary>
public record AppliedFix
{
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public string OriginalCode { get; init; } = "";
    public string FixedCode { get; init; } = "";
    public int LineNumber { get; init; }
    public FixConfidence Confidence { get; init; } = FixConfidence.Medium;
}

/// <summary>
/// Information about a suggested fix
/// </summary>
public record SuggestedFix
{
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public string Suggestion { get; init; } = "";
    public string Reasoning { get; init; } = "";
    public FixConfidence Confidence { get; init; } = FixConfidence.Medium;
}

/// <summary>
/// Summary of auto-fix operation
/// </summary>
public record AutoFixSummary
{
    public int TotalFixesApplied { get; init; }
    public int TotalSuggestionsGenerated { get; init; }
    public int BuildErrorsAddressed { get; init; }
    public int StyleImprovements { get; init; }
    public int PerformanceOptimizations { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public string[] Categories { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Types of auto-fixes available
/// </summary>
[Flags]
public enum AutoFixTypes
{
    None = 0,
    UsingsStatements = 1,
    Nullability = 2,
    AsyncMethods = 4,
    CodeStyle = 8,
    Performance = 16,
    BuildErrors = 32,
    DIRegistration = 64,
    All = UsingsStatements | Nullability | AsyncMethods | CodeStyle | Performance | BuildErrors | DIRegistration
}

/// <summary>
/// Confidence level in the fix
/// </summary>
public enum FixConfidence
{
    Low,      // Might not be correct, manual review needed
    Medium,   // Likely correct, but should be verified  
    High,     // Very confident, safe to apply automatically
    Critical  // Must be applied to compile/run
}
using DotNetMcp.Core.Services;
using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.SolutionAnalysis;

/// <summary>
/// Command to analyze solution structure and dependencies
/// </summary>
public record AnalyzeSolutionCommand : IAppRequest<AnalyzeSolutionResponse>
{
    public required string SolutionPath { get; init; }
    public bool IncludeDependencyGraph { get; init; } = true;
    public bool IncludeProjectDetails { get; init; } = true;
    public bool ValidateBuilds { get; init; } = false;
    public bool DetectIssues { get; init; } = true;
}

/// <summary>
/// Response with solution analysis results
/// </summary>
public record AnalyzeSolutionResponse
{
    public required SolutionSummary Summary { get; init; }
    public ProjectAnalysis[] Projects { get; init; } = Array.Empty<ProjectAnalysis>();
    public DependencyAnalysis Dependencies { get; init; } = new();
    public SolutionIssue[] Issues { get; init; } = Array.Empty<SolutionIssue>();
    public BuildSummary? BuildSummary { get; init; }
}

/// <summary>
/// Summary of solution structure
/// </summary>
public record SolutionSummary
{
    public string SolutionPath { get; init; } = "";
    public int ProjectCount { get; init; }
    public int SourceFileCount { get; init; }
    public string[] TargetFrameworks { get; init; } = Array.Empty<string>();
    public ProjectTypeSummary[] ProjectTypes { get; init; } = Array.Empty<ProjectTypeSummary>();
}

/// <summary>
/// Analysis of individual project
/// </summary>
public record ProjectAnalysis
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public ProjectType Type { get; init; }
    public string TargetFramework { get; init; } = "";
    public int SourceFileCount { get; init; }
    public int ProjectReferenceCount { get; init; }
    public int PackageReferenceCount { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public ProjectMetrics Metrics { get; init; } = new();
}

/// <summary>
/// Dependency analysis results
/// </summary>
public record DependencyAnalysis
{
    public DependencyNode[] Graph { get; init; } = Array.Empty<DependencyNode>();
    public string[] CircularDependencies { get; init; } = Array.Empty<string>();
    public LayerViolation[] LayerViolations { get; init; } = Array.Empty<LayerViolation>();
    public int MaxDepth { get; init; }
}

/// <summary>
/// Node in dependency graph
/// </summary>
public record DependencyNode
{
    public string ProjectName { get; init; } = "";
    public string[] DirectDependencies { get; init; } = Array.Empty<string>();
    public string[] TransitiveDependencies { get; init; } = Array.Empty<string>();
    public int DependencyCount { get; init; }
    public int DependentCount { get; init; }
}

/// <summary>
/// Solution-level issue
/// </summary>
public record SolutionIssue
{
    public SolutionIssueType Type { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public IssueSeverity Severity { get; init; }
    public string[] AffectedProjects { get; init; } = Array.Empty<string>();
    public string Recommendation { get; init; } = "";
}

/// <summary>
/// Project type summary
/// </summary>
public record ProjectTypeSummary
{
    public ProjectType Type { get; init; }
    public int Count { get; init; }
}

/// <summary>
/// Project metrics
/// </summary>
public record ProjectMetrics
{
    public int LinesOfCode { get; init; }
    public int ClassCount { get; init; }
    public int InterfaceCount { get; init; }
    public int TestCount { get; init; }
}

/// <summary>
/// Layer violation in architecture
/// </summary>
public record LayerViolation
{
    public string FromProject { get; init; } = "";
    public string ToProject { get; init; } = "";
    public string ViolationType { get; init; } = "";
    public string Description { get; init; } = "";
}

/// <summary>
/// Build summary for all projects
/// </summary>
public record BuildSummary
{
    public int TotalProjects { get; init; }
    public int SuccessfulBuilds { get; init; }
    public int FailedBuilds { get; init; }
    public ProjectBuildResult[] Results { get; init; } = Array.Empty<ProjectBuildResult>();
}

/// <summary>
/// Build result for individual project
/// </summary>
public record ProjectBuildResult
{
    public string ProjectName { get; init; } = "";
    public bool Success { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public TimeSpan BuildTime { get; init; }
}

/// <summary>
/// Types of solution issues
/// </summary>
public enum SolutionIssueType
{
    CircularDependency,
    LayerViolation,
    DuplicateGlobalUsings,
    MissingProjectReferences,
    UnusedProjects,
    FrameworkInconsistency,
    BuildErrors
}

/// <summary>
/// Issue severity levels
/// </summary>
public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Validator for analyze solution command
/// </summary>
public class AnalyzeSolutionCommandValidator : AbstractValidator<AnalyzeSolutionCommand>
{
    public AnalyzeSolutionCommandValidator()
    {
        RuleFor(x => x.SolutionPath)
            .NotEmpty()
            .WithMessage("Solution path cannot be empty");
    }
}
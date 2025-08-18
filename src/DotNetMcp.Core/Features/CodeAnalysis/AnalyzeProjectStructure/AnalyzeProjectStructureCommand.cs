using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Command to analyze the structure of a project
/// </summary>
public record AnalyzeProjectStructureCommand : IAppRequest<AnalyzeProjectStructureResponse>
{
    public required string ProjectPath { get; init; }
    public bool IncludeDependencies { get; init; } = true;
    public bool IncludeMetrics { get; init; } = true;
    public bool IncludeArchitecture { get; init; } = true;
    public bool IncludeTestStructure { get; init; } = false;
    public bool OptimizeForTokens { get; init; } = false;
    public int MaxTokens { get; init; } = 3000;
    public int MaxDepth { get; init; } = 3;
}

/// <summary>
/// Response containing project structure analysis
/// </summary>
public record AnalyzeProjectStructureResponse
{
    public required ProjectInfo ProjectInfo { get; init; }
    public required NamespaceInfo[] Namespaces { get; init; }
    public required FileInfo[] Files { get; init; }
    public required ProjectMetrics Metrics { get; init; }
    public required ArchitectureAnalysis Architecture { get; init; }
    public ProjectDependency[]? Dependencies { get; init; }
    public TestStructureInfo? TestStructure { get; init; }
    public int EstimatedTokens { get; init; }
    public bool SummarizationApplied { get; init; }
}

/// <summary>
/// Information about the project
/// </summary>
public record ProjectInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Framework { get; init; }
    public string? Version { get; init; }
    public string[]? PackageReferences { get; init; }
    public string[]? ProjectReferences { get; init; }
    public DateTime LastModified { get; init; }
}

/// <summary>
/// Information about a namespace
/// </summary>
public record NamespaceInfo
{
    public required string Name { get; init; }
    public required int ClassCount { get; init; }
    public required int InterfaceCount { get; init; }
    public required int EnumCount { get; init; }
    public required string[] Files { get; init; }
    public int TotalLines { get; init; }
    public double ComplexityScore { get; init; }
}

/// <summary>
/// Information about a file
/// </summary>
public record FileInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required int LineCount { get; init; }
    public required int ClassCount { get; init; }
    public required int InterfaceCount { get; init; }
    public required int MethodCount { get; init; }
    public required string[] Dependencies { get; init; }
    public DateTime LastModified { get; init; }
    public double ComplexityScore { get; init; }
}

/// <summary>
/// Project metrics
/// </summary>
public record ProjectMetrics
{
    public int TotalLines { get; init; }
    public int TotalClasses { get; init; }
    public int TotalInterfaces { get; init; }
    public int TotalMethods { get; init; }
    public int TotalFiles { get; init; }
    public double AverageComplexity { get; init; }
    public double CohesionScore { get; init; }
    public double CouplingScore { get; init; }
    public string[] LargestFiles { get; init; } = Array.Empty<string>();
    public string[] MostComplexClasses { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Architecture analysis
/// </summary>
public record ArchitectureAnalysis
{
    public required LayerInfo[] Layers { get; init; }
    public required string[] ArchitecturePatterns { get; init; }
    public required string[] PotentialIssues { get; init; }
    public required DependencyGraph DependencyGraph { get; init; }
    public double ArchitectureScore { get; init; }
}

/// <summary>
/// Information about architectural layers
/// </summary>
public record LayerInfo
{
    public required string Name { get; init; }
    public required string[] Namespaces { get; init; }
    public required string[] Dependencies { get; init; }
    public required string Purpose { get; init; }
    public int ClassCount { get; init; }
}

/// <summary>
/// Dependency graph representation
/// </summary>
public record DependencyGraph
{
    public required DependencyNode[] Nodes { get; init; }
    public required DependencyEdge[] Edges { get; init; }
    public int CircularDependencies { get; init; }
}

/// <summary>
/// Dependency graph node
/// </summary>
public record DependencyNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; } // "namespace", "class", "file"
    public int InDegree { get; init; }
    public int OutDegree { get; init; }
}

/// <summary>
/// Dependency graph edge
/// </summary>
public record DependencyEdge
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Type { get; init; } // "uses", "inherits", "implements"
    public int Weight { get; init; }
}

/// <summary>
/// Project dependency information
/// </summary>
public record ProjectDependency
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Type { get; init; } // "package", "project"
    public bool IsTransitive { get; init; }
    public string[]? Vulnerabilities { get; init; }
}

/// <summary>
/// Test structure information
/// </summary>
public record TestStructureInfo
{
    public required string[] TestProjects { get; init; }
    public required TestFileInfo[] TestFiles { get; init; }
    public double TestCoverage { get; init; }
    public string[] UncoveredClasses { get; init; } = Array.Empty<string>();
    public string TestFramework { get; init; } = "";
}

/// <summary>
/// Test file information
/// </summary>
public record TestFileInfo
{
    public required string Path { get; init; }
    public required string TestedClass { get; init; }
    public required int TestMethodCount { get; init; }
    public string[]? TestCategories { get; init; }
}

/// <summary>
/// Validator for analyze project structure command
/// </summary>
public class AnalyzeProjectStructureCommandValidator : AbstractValidator<AnalyzeProjectStructureCommand>
{
    public AnalyzeProjectStructureCommandValidator()
    {
        RuleFor(x => x.ProjectPath)
            .NotEmpty()
            .WithMessage("Project path cannot be empty");

        RuleFor(x => x.MaxDepth)
            .InclusiveBetween(1, 100)
            .WithMessage("Max depth must be between 1 and 100");

        RuleFor(x => x.MaxTokens)
            .InclusiveBetween(1, 50000)
            .When(x => x.OptimizeForTokens)
            .WithMessage("Max tokens must be between 1 and 50000 when token optimization is enabled");
    }
}
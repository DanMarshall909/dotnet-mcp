using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Command to find all usages of a symbol in the codebase
/// </summary>
public record FindSymbolUsagesCommand : IAppRequest<FindSymbolUsagesResponse>
{
    public required string ProjectPath { get; init; }
    public required string SymbolName { get; init; }
    public required SymbolType SymbolType { get; init; } = SymbolType.Any;
    public string? SymbolNamespace { get; init; }
    public bool IncludeDeclaration { get; init; } = true;
    public bool IncludeReferences { get; init; } = true;
    public bool IncludeImplementations { get; init; } = false;
    public bool IncludeInheritance { get; init; } = false;
    public bool GroupByFile { get; init; } = true;
    public bool OptimizeForTokens { get; init; } = false;
    public int MaxTokens { get; init; } = 2500;
    public int MaxResults { get; init; } = 100;
}

/// <summary>
/// Response containing symbol usage information
/// </summary>
public record FindSymbolUsagesResponse
{
    public required SymbolDeclaration? Declaration { get; init; }
    public required SymbolUsage[] Usages { get; init; }
    public required UsagesByFile[] UsagesByFile { get; init; }
    public required FindSymbolUsagesSummary Summary { get; init; }
    public int EstimatedTokens { get; init; }
    public bool SummarizationApplied { get; init; }
}

/// <summary>
/// Information about a symbol declaration
/// </summary>
public record SymbolDeclaration
{
    public required string Name { get; init; }
    public required SymbolType SymbolType { get; init; }
    public required string Namespace { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Signature { get; init; }
    public required AccessModifier AccessModifier { get; init; }
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public string? Documentation { get; init; }
    public string? ContainingClass { get; init; }
}

/// <summary>
/// Information about a symbol usage
/// </summary>
public record SymbolUsage
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required int ColumnNumber { get; init; }
    public required UsageType UsageType { get; init; }
    public required string Context { get; init; } // The line of code showing usage
    public required string ContainingMethod { get; init; }
    public required string ContainingClass { get; init; }
    public bool IsInSameClass { get; init; }
    public bool IsInSameNamespace { get; init; }
    public string? CallChain { get; init; } // Method call chain context
}

/// <summary>
/// Usage type enumeration
/// </summary>
public enum UsageType
{
    Declaration,
    MethodCall,
    PropertyAccess,
    FieldAccess,
    Constructor,
    TypeReference,
    Inheritance,
    Implementation,
    Assignment,
    Parameter,
    LocalVariable,
    Return
}

/// <summary>
/// Usages grouped by file
/// </summary>
public record UsagesByFile
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required SymbolUsage[] Usages { get; init; }
    public required UsageStatistics Statistics { get; init; }
}

/// <summary>
/// Usage statistics for a file
/// </summary>
public record UsageStatistics
{
    public int TotalUsages { get; init; }
    public int MethodCalls { get; init; }
    public int PropertyAccesses { get; init; }
    public int TypeReferences { get; init; }
    public int Assignments { get; init; }
    public bool HasDeclaration { get; init; }
    public double UsageDensity { get; init; } // Usages per 100 lines
}

/// <summary>
/// Summary of symbol usage analysis
/// </summary>
public record FindSymbolUsagesSummary
{
    public int TotalUsages { get; init; }
    public int FilesWithUsages { get; init; }
    public int NamespacesWithUsages { get; init; }
    public int ProjectsWithUsages { get; init; }
    public string[] ProjectNames { get; init; } = Array.Empty<string>();
    public UsageType[] UsageTypes { get; init; } = Array.Empty<UsageType>();
    public UsageDistribution Distribution { get; init; } = new();
    public string[] HeaviestFiles { get; init; } = Array.Empty<string>(); // Files with most usages
    public string[] ImpactAnalysis { get; init; } = Array.Empty<string>(); // Change impact insights
    public bool IsSolutionWide { get; init; } // Indicates if analysis spans multiple projects
}

/// <summary>
/// Usage distribution statistics
/// </summary>
public record UsageDistribution
{
    public int SameClass { get; init; }
    public int SameNamespace { get; init; }
    public int DifferentNamespace { get; init; }
    public int ExternalProjects { get; init; }
}

/// <summary>
/// Validator for find symbol usages command
/// </summary>
public class FindSymbolUsagesCommandValidator : AbstractValidator<FindSymbolUsagesCommand>
{
    public FindSymbolUsagesCommandValidator()
    {
        RuleFor(x => x.ProjectPath)
            .NotEmpty()
            .WithMessage("Project path cannot be empty");

        RuleFor(x => x.SymbolName)
            .NotEmpty()
            .WithMessage("Symbol name cannot be empty")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Symbol name must be a valid C# identifier");

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 1000)
            .WithMessage("Max results must be between 1 and 1000");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0)
            .When(x => x.OptimizeForTokens)
            .WithMessage("Max tokens must be greater than 0 when token optimization is enabled");
    }
}
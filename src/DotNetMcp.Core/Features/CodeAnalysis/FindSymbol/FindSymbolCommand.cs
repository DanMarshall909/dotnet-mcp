using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Command to find symbols in a codebase
/// </summary>
public record FindSymbolCommand : IAppRequest<FindSymbolResponse>
{
    public required string ProjectPath { get; init; }
    public required string SymbolName { get; init; }
    public SymbolType SymbolType { get; init; } = SymbolType.Any;
    public bool IncludeImplementations { get; init; } = false;
    public bool IncludeUsages { get; init; } = false;
    public int MaxResults { get; init; } = 50;
    public bool OptimizeForTokens { get; init; } = false;
    public int MaxTokens { get; init; } = 1000;
}

/// <summary>
/// Response containing found symbols
/// </summary>
public record FindSymbolResponse
{
    public required SymbolInfo[] Symbols { get; init; }
    public required FindSymbolSummary Summary { get; init; }
    public int EstimatedTokens { get; init; }
    public bool SummarizationApplied { get; init; }
}

/// <summary>
/// Information about a found symbol
/// </summary>
public record SymbolInfo
{
    public required string Name { get; init; }
    public required SymbolType SymbolType { get; init; }
    public required string Namespace { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public string? Signature { get; init; }
    public string? Documentation { get; init; }
    public ParameterInfo[]? Parameters { get; init; }
    public string? ReturnType { get; init; }
    public SymbolInfo[]? Implementations { get; init; }
    public AccessModifier AccessModifier { get; init; }
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
}

/// <summary>
/// Parameter information for methods
/// </summary>
public record ParameterInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsOptional { get; init; }
}

/// <summary>
/// Summary of symbol search results
/// </summary>
public record FindSymbolSummary
{
    public int TotalFound { get; init; }
    public int TotalShown { get; init; }
    public Dictionary<SymbolType, int> SymbolsByType { get; init; } = new();
    public Dictionary<string, int> SymbolsByNamespace { get; init; } = new();
    public string[]? SimilarSymbols { get; init; }
}

/// <summary>
/// Types of symbols that can be searched
/// </summary>
public enum SymbolType
{
    Any,
    Class,
    Interface,
    Method,
    Property,
    Field,
    Event,
    Enum,
    Struct,
    Delegate
}

/// <summary>
/// Access modifiers for symbols
/// </summary>
public enum AccessModifier
{
    Public,
    Private,
    Protected,
    Internal,
    ProtectedInternal,
    PrivateProtected
}

/// <summary>
/// Validator for find symbol command
/// </summary>
public class FindSymbolCommandValidator : AbstractValidator<FindSymbolCommand>
{
    public FindSymbolCommandValidator()
    {
        RuleFor(x => x.ProjectPath)
            .NotEmpty()
            .WithMessage("Project path cannot be empty");

        RuleFor(x => x.SymbolName)
            .NotEmpty()
            .WithMessage("Symbol name cannot be empty")
            .Must(name => !string.IsNullOrWhiteSpace(name))
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
using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.RenameSymbol;

/// <summary>
/// Command to rename a symbol across one or multiple files
/// </summary>
public record RenameSymbolCommand : IAppRequest<RenameSymbolResponse>
{
    public required string FilePath { get; init; }
    public required string OldName { get; init; }
    public required string NewName { get; init; }
    public string SymbolType { get; init; } = "auto";
    public bool MultiFile { get; init; } = false;
    public string? SolutionPath { get; init; }
}

/// <summary>
/// Response containing renamed symbol result
/// </summary>
public record RenameSymbolResponse
{
    public required bool Success { get; init; }
    public required string ModifiedCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string[]? AffectedFiles { get; init; }
    public int ChangesCount { get; init; }
}

/// <summary>
/// Validator for rename symbol command
/// </summary>
public class RenameSymbolCommandValidator : AbstractValidator<RenameSymbolCommand>
{
    public RenameSymbolCommandValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .WithMessage("File path cannot be empty")
            .Must(path => path.EndsWith(".cs"))
            .WithMessage("File path must be a C# file (.cs extension)");

        RuleFor(x => x.OldName)
            .NotEmpty()
            .WithMessage("Old name cannot be empty")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Old name must be a valid C# identifier");

        RuleFor(x => x.NewName)
            .NotEmpty()
            .WithMessage("New name cannot be empty")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("New name must be a valid C# identifier");

        RuleFor(x => x.SymbolType)
            .Must(type => new[] { "auto", "class", "method", "variable", "property" }.Contains(type))
            .WithMessage("Symbol type must be one of: auto, class, method, variable, property");

        RuleFor(x => x.SolutionPath)
            .Must(path => string.IsNullOrEmpty(path) || path.EndsWith(".sln") || path.EndsWith(".csproj"))
            .WithMessage("Solution path must be a .sln or .csproj file")
            .When(x => x.MultiFile);

        RuleFor(x => x)
            .Must(x => !x.MultiFile || !string.IsNullOrEmpty(x.SolutionPath))
            .WithMessage("Solution path is required for multi-file rename operations");
    }
}
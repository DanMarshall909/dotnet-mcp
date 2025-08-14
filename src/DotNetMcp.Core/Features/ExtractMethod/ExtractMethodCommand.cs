using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.ExtractMethod;

/// <summary>
/// Command to extract a method from selected code
/// </summary>
public record ExtractMethodCommand : IAppRequest<ExtractMethodResponse>
{
    public required string Code { get; init; }
    public required string SelectedCode { get; init; }
    public required string MethodName { get; init; }
    public string? FilePath { get; init; }
    public bool ReturnDelta { get; init; } = false;
}

/// <summary>
/// Response containing the extracted method result
/// </summary>
public record ExtractMethodResponse
{
    public required string ModifiedCode { get; init; }
    public required string ExtractedMethod { get; init; }
    public required string[] UsedVariables { get; init; }
    public required string ReturnType { get; init; }
    public string? Delta { get; init; }
    public int? TokensSaved { get; init; }
}

/// <summary>
/// Validator for extract method command
/// </summary>
public class ExtractMethodCommandValidator : AbstractValidator<ExtractMethodCommand>
{
    public ExtractMethodCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Code cannot be empty");

        RuleFor(x => x.SelectedCode)
            .NotEmpty()
            .WithMessage("Selected code cannot be empty");

        RuleFor(x => x.MethodName)
            .NotEmpty()
            .WithMessage("Method name cannot be empty")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Method name must be a valid C# identifier");

        RuleFor(x => x.FilePath)
            .Must(path => string.IsNullOrEmpty(path) || path.EndsWith(".cs"))
            .WithMessage("File path must be a C# file (.cs extension)")
            .When(x => !string.IsNullOrEmpty(x.FilePath));
    }
}
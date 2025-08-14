using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.ExtractInterface;

/// <summary>
/// Command to extract an interface from a class
/// </summary>
public record ExtractInterfaceCommand : IAppRequest<ExtractInterfaceResponse>
{
    public required string Code { get; init; }
    public required string ClassName { get; init; }
    public required string InterfaceName { get; init; }
    public string[]? MemberNames { get; init; }
}

/// <summary>
/// Response containing the extracted interface result
/// </summary>
public record ExtractInterfaceResponse
{
    public required string ModifiedCode { get; init; }
    public required string ExtractedInterface { get; init; }
    public required string[] ExtractedMembers { get; init; }
    public required string InterfaceName { get; init; }
}

/// <summary>
/// Validator for extract interface command
/// </summary>
public class ExtractInterfaceCommandValidator : AbstractValidator<ExtractInterfaceCommand>
{
    public ExtractInterfaceCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Code cannot be empty");

        RuleFor(x => x.ClassName)
            .NotEmpty()
            .WithMessage("Class name cannot be empty")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Class name must be a valid C# identifier");

        RuleFor(x => x.InterfaceName)
            .NotEmpty()
            .WithMessage("Interface name cannot be empty")
            .Matches(@"^I[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Interface name must be a valid C# identifier starting with 'I'");

        RuleForEach(x => x.MemberNames)
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Member names must be valid C# identifiers")
            .When(x => x.MemberNames != null);
    }
}
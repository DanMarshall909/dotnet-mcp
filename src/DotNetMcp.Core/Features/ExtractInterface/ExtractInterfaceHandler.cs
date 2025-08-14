using DotNetMcp.Core.Common;
using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.ExtractInterface;

/// <summary>
/// Handler for extract interface command using VSA pattern
/// </summary>
public class ExtractInterfaceHandler : BaseHandler<ExtractInterfaceCommand, ExtractInterfaceResponse>
{
    private readonly ModernExtractInterfaceRefactorer _refactorer;
    private readonly IValidator<ExtractInterfaceCommand> _validator;

    public ExtractInterfaceHandler(
        ILogger<ExtractInterfaceHandler> logger,
        ModernExtractInterfaceRefactorer refactorer,
        IValidator<ExtractInterfaceCommand> validator) : base(logger)
    {
        _refactorer = refactorer;
        _validator = validator;
    }

    protected override async Task<Result<Unit>> ValidateAsync(ExtractInterfaceCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Unit>.Failure($"Validation failed: {errors}");
        }

        return Result<Unit>.Success(Unit.Value);
    }

    protected override async Task<Result<ExtractInterfaceResponse>> HandleAsync(
        ExtractInterfaceCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var memberNames = request.MemberNames != null 
                ? Option.Some(request.MemberNames) 
                : Option.None<string[]>();

            var extractRequest = new ModernExtractInterfaceRefactorer.ExtractInterfaceRequest(
                request.Code,
                request.ClassName,
                request.InterfaceName,
                memberNames);

            var result = await _refactorer.ExtractInterfaceAsync(extractRequest);

            return result.Match(
                success => Result<ExtractInterfaceResponse>.Success(new ExtractInterfaceResponse
                {
                    ModifiedCode = success.ModifiedCode,
                    ExtractedInterface = success.ExtractedInterface,
                    ExtractedMembers = success.ExtractedMembers,
                    InterfaceName = success.InterfaceName
                }),
                (error, exception) => Result<ExtractInterfaceResponse>.Failure(error, exception ?? new InvalidOperationException(error)));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract interface {InterfaceName} from class {ClassName}", 
                request.InterfaceName, request.ClassName);
            return Result<ExtractInterfaceResponse>.Failure("Failed to extract interface", ex);
        }
    }
}
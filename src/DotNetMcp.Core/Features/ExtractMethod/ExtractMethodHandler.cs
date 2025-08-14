using DotNetMcp.Core.Common;
using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.Services;
using DotNetMcp.Core.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.ExtractMethod;

/// <summary>
/// Handler for extract method command using VSA pattern
/// </summary>
public class ExtractMethodHandler : BaseHandler<ExtractMethodCommand, ExtractMethodResponse>
{
    private readonly ExtractMethodRefactorer _refactorer;
    private readonly DeltaGenerator _deltaGenerator;
    private readonly IValidator<ExtractMethodCommand> _validator;

    public ExtractMethodHandler(
        ILogger<ExtractMethodHandler> logger,
        ExtractMethodRefactorer refactorer,
        DeltaGenerator deltaGenerator,
        IValidator<ExtractMethodCommand> validator) : base(logger)
    {
        _refactorer = refactorer;
        _deltaGenerator = deltaGenerator;
        _validator = validator;
    }

    protected override async Task<Result<Unit>> ValidateAsync(ExtractMethodCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Unit>.Failure($"Validation failed: {errors}");
        }

        return Result<Unit>.Success(Unit.Value);
    }

    protected override async Task<Result<ExtractMethodResponse>> HandleAsync(
        ExtractMethodCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract the method using the existing refactorer
            var extractResult = await _refactorer.ExtractMethodAsync(
                request.Code, 
                request.SelectedCode, 
                request.MethodName);

            // Generate delta if requested
            string? delta = null;
            int? tokensSaved = null;
            
            if (request.ReturnDelta && !string.IsNullOrEmpty(request.FilePath))
            {
                var deltaResult = _deltaGenerator.GenerateDelta(
                    request.FilePath,
                    request.Code,
                    extractResult.ModifiedCode,
                    extractResult.ExtractedMethod,
                    extractResult.UsedVariables);
                
                delta = System.Text.Json.JsonSerializer.Serialize(deltaResult);
                tokensSaved = _deltaGenerator.EstimateTokenSavings(deltaResult, request.Code);
            }

            var response = new ExtractMethodResponse
            {
                ModifiedCode = extractResult.ModifiedCode,
                ExtractedMethod = extractResult.ExtractedMethod,
                UsedVariables = extractResult.UsedVariables,
                ReturnType = extractResult.ReturnType,
                Delta = delta,
                TokensSaved = tokensSaved
            };

            return Result<ExtractMethodResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<ExtractMethodResponse>.Failure("Failed to extract method", ex);
        }
    }
}
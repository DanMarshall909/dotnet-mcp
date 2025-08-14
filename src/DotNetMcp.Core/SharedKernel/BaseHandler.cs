using DotNetMcp.Core.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.SharedKernel;

/// <summary>
/// Base handler providing common functionality for all handlers
/// </summary>
public abstract class BaseHandler<TRequest, TResponse> : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : IAppRequest<TResponse>
{
    protected readonly ILogger Logger;

    protected BaseHandler(ILogger logger)
    {
        Logger = logger;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        try
        {
            Logger.LogInformation("Handling {RequestName}", requestName);
            
            var validationResult = await ValidateAsync(request, cancellationToken);
            if (validationResult.IsFailure)
            {
                Logger.LogWarning("Validation failed for {RequestName}: {Error}", requestName, validationResult.Error);
                return Result<TResponse>.Failure(validationResult.Error);
            }

            var result = await HandleAsync(request, cancellationToken);
            
            if (result.IsSuccess)
            {
                Logger.LogInformation("Successfully handled {RequestName}", requestName);
            }
            else
            {
                Logger.LogError("Failed to handle {RequestName}: {Error}", requestName, result.Error);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unhandled exception in {RequestName}", requestName);
            return Result<TResponse>.Failure($"An error occurred while processing {requestName}", ex);
        }
    }

    protected virtual Task<Result<Unit>> ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    protected abstract Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
using System.Diagnostics.CodeAnalysis;
using DotNetMcp.Core.Common.Errors;

namespace DotNetMcp.Core.Common;

/// <summary>
/// Discriminated union representing the result of an operation that can succeed or fail
/// </summary>
public abstract record Result<T>
{
    public static Result<T> Success(T value) => new SuccessResult<T>(value);
    public static Result<T> Failure(string error) => new FailureResult<T>(error);
    public static Result<T> Failure(string error, Exception exception) => new FailureResult<T>(error, exception);
    public static Result<T> Failure(AnalysisError error, AnalysisContext? context = null) => new DetailedFailureResult<T>(error, context);
    
    public abstract bool IsSuccess { get; }
    public abstract bool IsFailure { get; }
    
    public abstract T Value { get; }
    public abstract string Error { get; }
    public abstract Exception? Exception { get; }
    public abstract AnalysisError? DetailedError { get; }
    public abstract AnalysisContext? Context { get; }
    
    // Pattern matching helpers
    public abstract TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure);
    public abstract TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure, Func<AnalysisError, AnalysisContext?, TResult> onDetailedFailure);
    public abstract Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure);
    public abstract Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure, Func<AnalysisError, AnalysisContext?, Task<TResult>> onDetailedFailure);
    
    // Monadic operations
    public abstract Result<TNext> Map<TNext>(Func<T, TNext> mapper);
    public abstract Result<TNext> Bind<TNext>(Func<T, Result<TNext>> binder);
    
    public static implicit operator Result<T>(T value) => Success(value);
}

public sealed record SuccessResult<T>(T Data) : Result<T>
{
    public override bool IsSuccess => true;
    public override bool IsFailure => false;
    public override T Value => Data;
    public override string Error => throw new InvalidOperationException("Success result has no error");
    public override Exception? Exception => null;
    public override AnalysisError? DetailedError => null;
    public override AnalysisContext? Context => null;
    
    public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure)
        => onSuccess(Data);
        
    public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure, Func<AnalysisError, AnalysisContext?, TResult> onDetailedFailure)
        => onSuccess(Data);
    
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure)
        => await onSuccess(Data);
        
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure, Func<AnalysisError, AnalysisContext?, Task<TResult>> onDetailedFailure)
        => await onSuccess(Data);
    
    public override Result<TNext> Map<TNext>(Func<T, TNext> mapper)
        => Result<TNext>.Success(mapper(Data));
    
    public override Result<TNext> Bind<TNext>(Func<T, Result<TNext>> binder)
        => binder(Data);
}

public sealed record FailureResult<T>(string ErrorMessage, Exception? InnerException = null) : Result<T>
{
    public override bool IsSuccess => false;
    public override bool IsFailure => true;
    public override T Value => throw new InvalidOperationException($"Failure result has no value: {ErrorMessage}");
    public override string Error => ErrorMessage;
    public override Exception? Exception => InnerException;
    public override AnalysisError? DetailedError => null;
    public override AnalysisContext? Context => null;
    
    public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure)
        => onFailure(ErrorMessage, InnerException);
        
    public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure, Func<AnalysisError, AnalysisContext?, TResult> onDetailedFailure)
        => onFailure(ErrorMessage, InnerException);
    
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure)
        => await onFailure(ErrorMessage, InnerException);
        
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure, Func<AnalysisError, AnalysisContext?, Task<TResult>> onDetailedFailure)
        => await onFailure(ErrorMessage, InnerException);
    
    public override Result<TNext> Map<TNext>(Func<T, TNext> mapper)
        => Result<TNext>.Failure(ErrorMessage, InnerException ?? new InvalidOperationException(ErrorMessage));
    
    public override Result<TNext> Bind<TNext>(Func<T, Result<TNext>> binder)
        => Result<TNext>.Failure(ErrorMessage, InnerException ?? new InvalidOperationException(ErrorMessage));
}

public sealed record DetailedFailureResult<T>(AnalysisError ErrorDetails, AnalysisContext? ErrorContext = null) : Result<T>
{
    public override bool IsSuccess => false;
    public override bool IsFailure => true;
    public override T Value => throw new InvalidOperationException($"Failure result has no value: {ErrorDetails.Message}");
    public override string Error => ErrorDetails.Message;
    public override Exception? Exception => null;
    public override AnalysisError? DetailedError => ErrorDetails;
    public override AnalysisContext? Context => ErrorContext;
    
    public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure)
        => onFailure(ErrorDetails.Message, null);
        
    public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception?, TResult> onFailure, Func<AnalysisError, AnalysisContext?, TResult> onDetailedFailure)
        => onDetailedFailure(ErrorDetails, ErrorContext);
    
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure)
        => await onFailure(ErrorDetails.Message, null);
        
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<string, Exception?, Task<TResult>> onFailure, Func<AnalysisError, AnalysisContext?, Task<TResult>> onDetailedFailure)
        => await onDetailedFailure(ErrorDetails, ErrorContext);
    
    public override Result<TNext> Map<TNext>(Func<T, TNext> mapper)
        => Result<TNext>.Failure(ErrorDetails, ErrorContext);
    
    public override Result<TNext> Bind<TNext>(Func<T, Result<TNext>> binder)
        => Result<TNext>.Failure(ErrorDetails, ErrorContext);
}

// Convenience methods
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
    public static Result<T> Failure<T>(string error, Exception exception) => Result<T>.Failure(error, exception);
    public static Result<T> Failure<T>(AnalysisError error, AnalysisContext? context = null) => Result<T>.Failure(error, context);
}
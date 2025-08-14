using DotNetMcp.Core.Common;
using MediatR;

namespace DotNetMcp.Core.SharedKernel;

/// <summary>
/// Base interface for all requests that return a Result
/// </summary>
public interface IAppRequest<T> : IRequest<Result<T>>
{
}

/// <summary>
/// Base interface for all requests that don't return a value
/// </summary>
public interface ICommand : IAppRequest<Unit>
{
}

/// <summary>
/// Base interface for all queries that return data
/// </summary>
public interface IQuery<T> : IAppRequest<T>
{
}
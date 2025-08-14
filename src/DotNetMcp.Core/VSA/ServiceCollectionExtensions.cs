using System.IO.Abstractions;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.Features.ExtractInterface;
using DotNetMcp.Core.Features.ExtractMethod;
using DotNetMcp.Core.Features.RenameSymbol;
using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetMcp.Core.VSA;

/// <summary>
/// Extension methods for configuring VSA services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all VSA services to the DI container
    /// </summary>
    public static IServiceCollection AddVerticalSliceArchitecture(this IServiceCollection services)
    {
        // Add MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        // Add validators
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        // Add feature handlers
        services.AddScoped<ExtractMethodHandler>();
        services.AddScoped<RenameSymbolHandler>();
        services.AddScoped<ExtractInterfaceHandler>();
        services.AddScoped<FindSymbolHandler>();
        services.AddScoped<GetClassContextHandler>();
        services.AddScoped<AnalyzeProjectStructureHandler>();
        services.AddScoped<FindSymbolUsagesHandler>();

        // Add existing refactoring services
        services.AddScoped<ExtractMethodRefactorer>();
        services.AddScoped<RenameSymbolRefactorer>();
        services.AddScoped<ModernExtractInterfaceRefactorer>();
        services.AddScoped<MultiFileRefactoringEngine>();
        services.AddScoped<DeltaGenerator>();
        
        // Add file system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();
        
        // Add build validation service
        services.AddScoped<BuildValidationService>();

        return services;
    }

    /// <summary>
    /// Add specific feature slice services
    /// </summary>
    public static IServiceCollection AddFeatureSlice<THandler>(this IServiceCollection services)
        where THandler : class
    {
        services.AddScoped<THandler>();
        return services;
    }
}
using System.IO.Abstractions;
using DotNetMcp.Core.Features.AutoFix;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.Features.ExtractInterface;
using DotNetMcp.Core.Features.ExtractMethod;
using DotNetMcp.Core.Features.RenameSymbol;
using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetMcp.Core.Extensions;

/// <summary>
/// Extension methods for configuring core services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all core services to the DI container
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Add MediatR with behaviors
        services.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(Common.ValidationBehavior<,>));
        });

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
        services.AddScoped<AutoFixHandler>();
        
        // Add solution analysis handlers
        services.AddScoped<Features.SolutionAnalysis.AnalyzeSolutionHandler>();

        // Add existing refactoring services
        services.AddScoped<ExtractMethodRefactorer>();
        services.AddScoped<RenameSymbolRefactorer>();
        services.AddScoped<ModernExtractInterfaceRefactorer>();
        services.AddScoped<MultiFileRefactoringEngine>();
        services.AddScoped<DeltaGenerator>();
        
        // Add file system abstraction (only if not already registered)
        if (!services.Any(x => x.ServiceType == typeof(IFileSystem)))
        {
            services.AddSingleton<IFileSystem, FileSystem>();
        }
        
        // Add build validation service
        services.AddScoped<IBuildValidationService, BuildValidationService>();
        services.AddScoped<BuildValidationService>();
        
        // Add compilation service for duplicate file handling
        services.AddScoped<CompilationService>();
        
        // Add solution-wide analysis services
        services.AddScoped<SolutionDiscoveryService>();
        services.AddScoped<WorkspaceManager>();
        
        // Add error analysis service
        services.AddScoped<ErrorAnalysisService>();
        
        // Add analysis strategies
        services.AddScoped<Analysis.Strategies.SemanticRoslynStrategy>();
        services.AddScoped<Analysis.Strategies.SyntaxRoslynStrategy>();
        services.AddScoped<Analysis.Strategies.TextBasedStrategy>();
        services.AddScoped<Analysis.Strategies.HybridStrategy>();
        services.AddScoped<Analysis.Strategies.AnalysisStrategyChain>();
        
        // Register strategies as IAnalysisStrategy implementations
        services.AddScoped<Analysis.Strategies.IAnalysisStrategy, Analysis.Strategies.SemanticRoslynStrategy>();
        services.AddScoped<Analysis.Strategies.IAnalysisStrategy, Analysis.Strategies.SyntaxRoslynStrategy>();
        services.AddScoped<Analysis.Strategies.IAnalysisStrategy, Analysis.Strategies.TextBasedStrategy>();
        services.AddScoped<Analysis.Strategies.IAnalysisStrategy, Analysis.Strategies.HybridStrategy>();

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
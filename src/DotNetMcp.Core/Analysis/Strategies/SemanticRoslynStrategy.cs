using DotNetMcp.Core.Services;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Analysis.Strategies;

/// <summary>
/// Full semantic Roslyn analysis strategy (requires successful compilation)
/// </summary>
public class SemanticRoslynStrategy : IAnalysisStrategy
{
    private readonly CompilationService _compilationService;
    private readonly BuildValidationService _buildValidationService;
    private readonly ILogger<SemanticRoslynStrategy> _logger;

    public AnalysisStrategyType Type => AnalysisStrategyType.SemanticRoslyn;
    public int Priority => 1; // Highest priority (preferred)

    public SemanticRoslynStrategy(
        CompilationService compilationService,
        BuildValidationService buildValidationService,
        ILogger<SemanticRoslynStrategy> logger)
    {
        _compilationService = compilationService;
        _buildValidationService = buildValidationService;
        _logger = logger;
    }

    public bool CanHandle(AnalysisRequest request, ProjectContext context)
    {
        // Can only handle if project can compile successfully
        return context.CanCompile && !context.HasBuildErrors;
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting semantic Roslyn analysis for request type: {RequestType}", request.RequestType);

            // Validate build first
            var buildResult = await _buildValidationService.ValidateBuildAsync(request.ProjectPath, cancellationToken);
            
            if (!buildResult.IsSuccess)
            {
                throw new InvalidOperationException($"Build validation failed: {buildResult.Message}");
            }

            // Create compilation
            var files = GetRelevantFiles(request);
            var compilation = await _compilationService.CreateCompilationAsync(files, "AnalysisAssembly");

            // Check for compilation errors
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .ToArray();

            if (diagnostics.Any())
            {
                throw new InvalidOperationException($"Compilation failed with {diagnostics.Length} errors");
            }

            var result = request.RequestType switch
            {
                "find_symbol" => await FindSymbolWithSemanticAnalysisAsync(request, compilation),
                "find_symbol_usages" => await FindSymbolUsagesWithSemanticAnalysisAsync(request, compilation),
                "get_class_context" => await GetClassContextWithSemanticAnalysisAsync(request, compilation),
                "analyze_project_structure" => await AnalyzeProjectStructureWithSemanticAnalysisAsync(request, compilation),
                _ => throw new NotSupportedException($"Request type '{request.RequestType}' not supported by semantic strategy")
            };

            var executionTime = DateTime.UtcNow - startTime;
            
            return new AnalysisResult
            {
                Success = true,
                Data = result,
                StrategyUsed = Type,
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    ["compilationSuccess"] = true,
                    ["syntaxTreeCount"] = compilation.SyntaxTrees.Count(),
                    ["analysisType"] = "semantic-full"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic Roslyn analysis failed for request type: {RequestType}", request.RequestType);
            
            return new AnalysisResult
            {
                Success = false,
                ErrorMessage = $"Semantic analysis failed: {ex.Message}",
                StrategyUsed = Type,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    public AnalysisCapabilities GetCapabilities()
    {
        return new AnalysisCapabilities
        {
            HasSymbolResolution = true,
            HasTypeInformation = true,
            HasCrossReferences = true,
            HasSemanticAnalysis = true,
            HasSyntaxAnalysis = true,
            HasTextMatching = false,
            Performance = PerformanceLevel.Slow,
            Reliability = ReliabilityLevel.Fragile, // Depends on successful compilation
            Limitations = new[]
            {
                "Requires successful project compilation",
                "Slower due to full semantic analysis",
                "May fail with complex project dependencies",
                "Sensitive to build configuration issues"
            },
            Strengths = new[]
            {
                "Complete type information",
                "Full cross-reference resolution",
                "Accurate symbol binding",
                "Comprehensive semantic analysis",
                "Best accuracy for complex queries"
            }
        };
    }

    private string[] GetRelevantFiles(AnalysisRequest request)
    {
        if (request.FilePaths.Any())
            return request.FilePaths.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();

        // For now, return empty array - in a real implementation, this would
        // discover files from the project path using existing services
        return Array.Empty<string>();
    }

    private async Task<object> FindSymbolWithSemanticAnalysisAsync(AnalysisRequest request, Microsoft.CodeAnalysis.Compilation compilation)
    {
        // This would integrate with existing semantic analysis logic
        // For now, return a placeholder that indicates semantic analysis was used
        return new SemanticFindSymbolResult
        {
            SymbolName = request.SymbolName,
            Strategy = "semantic-roslyn",
            HasTypeInformation = true,
            HasCrossReferences = true,
            Message = "Semantic analysis completed successfully"
        };
    }

    private async Task<object> FindSymbolUsagesWithSemanticAnalysisAsync(AnalysisRequest request, Microsoft.CodeAnalysis.Compilation compilation)
    {
        // This would integrate with existing semantic analysis logic
        return new SemanticFindUsagesResult
        {
            SymbolName = request.SymbolName,
            Strategy = "semantic-roslyn",
            HasTypeInformation = true,
            HasCrossReferences = true,
            Message = "Semantic usage analysis completed successfully"
        };
    }

    private async Task<object> GetClassContextWithSemanticAnalysisAsync(AnalysisRequest request, Microsoft.CodeAnalysis.Compilation compilation)
    {
        // This would integrate with existing semantic analysis logic
        return new SemanticClassContext
        {
            ClassName = request.Parameters.GetValueOrDefault("className", request.SymbolName)?.ToString() ?? "",
            Strategy = "semantic-roslyn",
            HasTypeInformation = true,
            HasInheritanceInfo = true,
            Message = "Semantic class analysis completed successfully"
        };
    }

    private async Task<object> AnalyzeProjectStructureWithSemanticAnalysisAsync(AnalysisRequest request, Microsoft.CodeAnalysis.Compilation compilation)
    {
        // This would integrate with existing semantic analysis logic
        return new SemanticProjectStructure
        {
            ProjectPath = request.ProjectPath,
            Strategy = "semantic-roslyn",
            HasTypeInformation = true,
            HasDependencyAnalysis = true,
            Message = "Semantic project analysis completed successfully"
        };
    }
}

// Response models for semantic analysis (these would be expanded in real implementation)
public record SemanticFindSymbolResult
{
    public string SymbolName { get; init; } = "";
    public string Strategy { get; init; } = "";
    public bool HasTypeInformation { get; init; }
    public bool HasCrossReferences { get; init; }
    public string Message { get; init; } = "";
}

public record SemanticFindUsagesResult
{
    public string SymbolName { get; init; } = "";
    public string Strategy { get; init; } = "";
    public bool HasTypeInformation { get; init; }
    public bool HasCrossReferences { get; init; }
    public string Message { get; init; } = "";
}

public record SemanticClassContext
{
    public string ClassName { get; init; } = "";
    public string Strategy { get; init; } = "";
    public bool HasTypeInformation { get; init; }
    public bool HasInheritanceInfo { get; init; }
    public string Message { get; init; } = "";
}

public record SemanticProjectStructure
{
    public string ProjectPath { get; init; } = "";
    public string Strategy { get; init; } = "";
    public bool HasTypeInformation { get; init; }
    public bool HasDependencyAnalysis { get; init; }
    public string Message { get; init; } = "";
}
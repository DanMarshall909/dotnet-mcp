using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Analysis.Strategies;

/// <summary>
/// Hybrid strategy that combines multiple analysis approaches
/// </summary>
public class HybridStrategy : IAnalysisStrategy
{
    private readonly SemanticRoslynStrategy _semanticStrategy;
    private readonly SyntaxRoslynStrategy _syntaxStrategy;
    private readonly TextBasedStrategy _textStrategy;
    private readonly ILogger<HybridStrategy> _logger;

    public AnalysisStrategyType Type => AnalysisStrategyType.Hybrid;
    public int Priority => 4; // Lowest priority (used as last resort)

    public HybridStrategy(
        SemanticRoslynStrategy semanticStrategy,
        SyntaxRoslynStrategy syntaxStrategy,
        TextBasedStrategy textStrategy,
        ILogger<HybridStrategy> logger)
    {
        _semanticStrategy = semanticStrategy;
        _syntaxStrategy = syntaxStrategy;
        _textStrategy = textStrategy;
        _logger = logger;
    }

    public bool CanHandle(AnalysisRequest request, ProjectContext context)
    {
        // Hybrid strategy can always handle requests by falling back to text analysis
        return true;
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting hybrid analysis for request type: {RequestType}", request.RequestType);

            var results = new List<AnalysisResult>();
            var strategiesUsed = new List<string>();

            // Try semantic analysis first if possible
            if (_semanticStrategy.CanHandle(request, new ProjectContext { CanCompile = true, HasBuildErrors = false }))
            {
                try
                {
                    var semanticResult = await _semanticStrategy.AnalyzeAsync(request, cancellationToken);
                    if (semanticResult.Success)
                    {
                        results.Add(semanticResult);
                        strategiesUsed.Add("semantic");
                        _logger.LogInformation("Semantic analysis succeeded in hybrid strategy");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Semantic analysis failed in hybrid strategy, continuing with other strategies");
                }
            }

            // Try syntax analysis if semantic failed or wasn't available
            if (!results.Any() || ShouldAddSyntaxAnalysis(request))
            {
                try
                {
                    var syntaxResult = await _syntaxStrategy.AnalyzeAsync(request, cancellationToken);
                    if (syntaxResult.Success)
                    {
                        results.Add(syntaxResult);
                        strategiesUsed.Add("syntax");
                        _logger.LogInformation("Syntax analysis succeeded in hybrid strategy");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Syntax analysis failed in hybrid strategy, continuing with text analysis");
                }
            }

            // Always try text analysis as a fallback
            try
            {
                var textResult = await _textStrategy.AnalyzeAsync(request, cancellationToken);
                if (textResult.Success)
                {
                    results.Add(textResult);
                    strategiesUsed.Add("text");
                    _logger.LogInformation("Text analysis succeeded in hybrid strategy");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Text analysis failed in hybrid strategy");
            }

            if (!results.Any())
            {
                throw new InvalidOperationException("All analysis strategies failed in hybrid approach");
            }

            // Merge results intelligently
            var mergedResult = MergeResults(results, request);
            var executionTime = DateTime.UtcNow - startTime;

            return new AnalysisResult
            {
                Success = true,
                Data = mergedResult,
                StrategyUsed = Type,
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    ["strategiesUsed"] = strategiesUsed.ToArray(),
                    ["resultCount"] = results.Count,
                    ["analysisType"] = "hybrid"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hybrid analysis failed for request type: {RequestType}", request.RequestType);
            
            return new AnalysisResult
            {
                Success = false,
                ErrorMessage = $"Hybrid analysis failed: {ex.Message}",
                StrategyUsed = Type,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    public AnalysisCapabilities GetCapabilities()
    {
        return new AnalysisCapabilities
        {
            HasSymbolResolution = true, // Best available
            HasTypeInformation = true, // If semantic analysis works
            HasCrossReferences = true, // If semantic analysis works
            HasSemanticAnalysis = true, // If compilation succeeds
            HasSyntaxAnalysis = true,
            HasTextMatching = true,
            Performance = PerformanceLevel.Medium, // Variable depending on what succeeds
            Reliability = ReliabilityLevel.Reliable, // Always provides some result
            Limitations = new[]
            {
                "Performance varies based on available strategies",
                "Result quality depends on which strategies succeed",
                "May produce mixed-quality results"
            },
            Strengths = new[]
            {
                "Always provides some result",
                "Combines best available information",
                "Graceful degradation of capabilities",
                "Robust against various failure modes"
            }
        };
    }

    private bool ShouldAddSyntaxAnalysis(AnalysisRequest request)
    {
        // Add syntax analysis for structural queries even if semantic succeeded
        return request.RequestType is "analyze_project_structure" or "get_class_context";
    }

    private object MergeResults(List<AnalysisResult> results, AnalysisRequest request)
    {
        if (results.Count == 1)
            return results[0].Data ?? new object();

        // Create a hybrid result that combines information from multiple strategies
        var hybridResult = new HybridAnalysisResult
        {
            RequestType = request.RequestType,
            SymbolName = request.SymbolName,
            ProjectPath = request.ProjectPath,
            StrategiesUsed = results.Select(r => r.StrategyUsed.ToString()).ToArray(),
            Results = results.Select(r => new StrategyResult
            {
                Strategy = r.StrategyUsed.ToString(),
                Success = r.Success,
                Data = r.Data,
                ExecutionTime = r.ExecutionTime.TotalMilliseconds,
                Capabilities = r.Capabilities
            }).ToArray(),
            MergedCapabilities = MergeCapabilities(results),
            PrimaryResult = SelectPrimaryResult(results),
            AdditionalInsights = ExtractAdditionalInsights(results)
        };

        return hybridResult;
    }

    private AnalysisCapabilities MergeCapabilities(List<AnalysisResult> results)
    {
        var capabilities = results.Select(r => r.Capabilities).ToArray();
        
        return new AnalysisCapabilities
        {
            HasSymbolResolution = capabilities.Any(c => c.HasSymbolResolution),
            HasTypeInformation = capabilities.Any(c => c.HasTypeInformation),
            HasCrossReferences = capabilities.Any(c => c.HasCrossReferences),
            HasSemanticAnalysis = capabilities.Any(c => c.HasSemanticAnalysis),
            HasSyntaxAnalysis = capabilities.Any(c => c.HasSyntaxAnalysis),
            HasTextMatching = capabilities.Any(c => c.HasTextMatching),
            Performance = capabilities.Max(c => c.Performance),
            Reliability = capabilities.Max(c => c.Reliability),
            Limitations = capabilities.SelectMany(c => c.Limitations).Distinct().ToArray(),
            Strengths = capabilities.SelectMany(c => c.Strengths).Distinct().ToArray()
        };
    }

    private object? SelectPrimaryResult(List<AnalysisResult> results)
    {
        // Prefer semantic results, then syntax, then text
        var priorityOrder = new[] { AnalysisStrategyType.SemanticRoslyn, AnalysisStrategyType.SyntaxRoslyn, AnalysisStrategyType.TextBased };
        
        foreach (var strategyType in priorityOrder)
        {
            var result = results.FirstOrDefault(r => r.StrategyUsed == strategyType);
            if (result != null)
                return result.Data;
        }

        return results.FirstOrDefault()?.Data;
    }

    private string[] ExtractAdditionalInsights(List<AnalysisResult> results)
    {
        var insights = new List<string>();

        if (results.Any(r => r.StrategyUsed == AnalysisStrategyType.SemanticRoslyn))
        {
            insights.Add("Full semantic analysis was available");
        }
        else if (results.Any(r => r.StrategyUsed == AnalysisStrategyType.SyntaxRoslyn))
        {
            insights.Add("Syntax-only analysis was used (no compilation available)");
        }
        else
        {
            insights.Add("Only text-based analysis was possible");
        }

        var totalStrategies = results.Count;
        if (totalStrategies > 1)
        {
            insights.Add($"Combined information from {totalStrategies} analysis strategies");
        }

        return insights.ToArray();
    }
}

/// <summary>
/// Result that combines multiple analysis strategies
/// </summary>
public record HybridAnalysisResult
{
    public string RequestType { get; init; } = "";
    public string SymbolName { get; init; } = "";
    public string ProjectPath { get; init; } = "";
    public string[] StrategiesUsed { get; init; } = Array.Empty<string>();
    public StrategyResult[] Results { get; init; } = Array.Empty<StrategyResult>();
    public AnalysisCapabilities MergedCapabilities { get; init; } = new();
    public object? PrimaryResult { get; init; }
    public string[] AdditionalInsights { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result from a single strategy within a hybrid analysis
/// </summary>
public record StrategyResult
{
    public string Strategy { get; init; } = "";
    public bool Success { get; init; }
    public object? Data { get; init; }
    public double ExecutionTime { get; init; }
    public AnalysisCapabilities Capabilities { get; init; } = new();
}
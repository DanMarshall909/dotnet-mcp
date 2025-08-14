using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Analysis.Strategies;

/// <summary>
/// Chains multiple analysis strategies with fallback support
/// </summary>
public class AnalysisStrategyChain
{
    private readonly IAnalysisStrategy[] _strategies;
    private readonly ILogger<AnalysisStrategyChain> _logger;

    public AnalysisStrategyChain(IEnumerable<IAnalysisStrategy> strategies, ILogger<AnalysisStrategyChain> logger)
    {
        _strategies = strategies.OrderBy(s => s.Priority).ToArray();
        _logger = logger;
    }

    /// <summary>
    /// Analyzes using the first capable strategy in the chain
    /// </summary>
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, ProjectContext context, CancellationToken cancellationToken = default)
    {
        var failedStrategies = new List<string>();
        var exceptions = new Dictionary<string, Exception>();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting analysis with strategy chain for request type: {RequestType}", request.RequestType);

        foreach (var strategy in _strategies)
        {
            if (!strategy.CanHandle(request, context))
            {
                _logger.LogDebug("Strategy {StrategyType} cannot handle request", strategy.Type);
                continue;
            }

            try
            {
                _logger.LogInformation("Attempting analysis with strategy: {StrategyType}", strategy.Type);
                
                var result = await strategy.AnalyzeAsync(request, cancellationToken);
                
                if (result.Success)
                {
                    var totalTime = DateTime.UtcNow - startTime;
                    _logger.LogInformation("Analysis succeeded with strategy {StrategyType} in {Duration}ms", 
                        strategy.Type, totalTime.TotalMilliseconds);
                    
                    return result with 
                    { 
                        StrategyUsed = strategy.Type,
                        Capabilities = strategy.GetCapabilities(),
                        ExecutionTime = totalTime
                    };
                }
                else
                {
                    _logger.LogWarning("Strategy {StrategyType} returned unsuccessful result: {Error}", 
                        strategy.Type, result.ErrorMessage);
                    failedStrategies.Add(strategy.Type.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Strategy {StrategyType} failed with exception", strategy.Type);
                failedStrategies.Add(strategy.Type.ToString());
                exceptions[strategy.Type.ToString()] = ex;
                
                // Don't continue to fallback if cancellation was requested
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Analysis was cancelled", ex);
                }
            }
        }

        _logger.LogError("All strategies failed for request type: {RequestType}", request.RequestType);
        throw new AllStrategiesFailedException(failedStrategies.ToArray(), exceptions);
    }

    /// <summary>
    /// Gets the best available strategy for a given request
    /// </summary>
    public IAnalysisStrategy? GetBestStrategy(AnalysisRequest request, ProjectContext context)
    {
        return _strategies.FirstOrDefault(s => s.CanHandle(request, context));
    }

    /// <summary>
    /// Gets all capable strategies for a given request
    /// </summary>
    public IAnalysisStrategy[] GetCapableStrategies(AnalysisRequest request, ProjectContext context)
    {
        return _strategies.Where(s => s.CanHandle(request, context)).ToArray();
    }

    /// <summary>
    /// Gets capabilities matrix for all strategies
    /// </summary>
    public StrategyCapabilityMatrix GetCapabilityMatrix()
    {
        var matrix = new Dictionary<AnalysisStrategyType, AnalysisCapabilities>();
        
        foreach (var strategy in _strategies)
        {
            matrix[strategy.Type] = strategy.GetCapabilities();
        }

        return new StrategyCapabilityMatrix { Strategies = matrix };
    }
}

/// <summary>
/// Matrix showing capabilities of all available strategies
/// </summary>
public record StrategyCapabilityMatrix
{
    public Dictionary<AnalysisStrategyType, AnalysisCapabilities> Strategies { get; init; } = new();
    
    /// <summary>
    /// Gets the best strategy type for specific capabilities
    /// </summary>
    public AnalysisStrategyType? GetBestFor(bool needsSemantics = false, bool needsTypes = false, bool needsReferences = false)
    {
        var candidates = Strategies.Where(kvp =>
        {
            var caps = kvp.Value;
            return (!needsSemantics || caps.HasSemanticAnalysis) &&
                   (!needsTypes || caps.HasTypeInformation) &&
                   (!needsReferences || caps.HasCrossReferences);
        }).ToArray();

        if (!candidates.Any())
            return null;

        // Prefer higher capability strategies
        return candidates
            .OrderByDescending(kvp => GetCapabilityScore(kvp.Value))
            .First().Key;
    }

    private static int GetCapabilityScore(AnalysisCapabilities caps)
    {
        int score = 0;
        if (caps.HasSemanticAnalysis) score += 4;
        if (caps.HasTypeInformation) score += 3;
        if (caps.HasCrossReferences) score += 2;
        if (caps.HasSymbolResolution) score += 1;
        return score;
    }
}
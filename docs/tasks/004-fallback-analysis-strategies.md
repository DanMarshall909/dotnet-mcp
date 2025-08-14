# Task 004: Fallback Analysis Strategies

## Priority: 🎯 High
**Status**: Pending  
**Estimated Effort**: 5 hours  
**Dependencies**: Task 003

## Problem Description

When Roslyn-based semantic analysis fails (due to build errors, complex project structures, or other issues), the MCP tools should gracefully fall back to alternative analysis methods rather than failing completely.

### Current Behavior
- Tool fails completely when Roslyn analysis is impossible
- No alternative analysis methods available
- Users get no results even for simple text-based queries

### Desired Behavior
- Automatic fallback to text-based analysis
- Progressive capability degradation with clear communication
- Multiple analysis strategies for different scenarios

## Solution Design

### 1. Analysis Strategy Hierarchy
```csharp
public enum AnalysisStrategy
{
    SemanticRoslyn,    // Full semantic analysis (preferred)
    SyntaxRoslyn,      // Syntax-only analysis (no compilation)
    TextBased,         // Regex/text pattern matching
    Hybrid             // Combination of strategies
}

public interface IAnalysisStrategy
{
    AnalysisStrategy Type { get; }
    bool CanHandle(AnalysisRequest request, ProjectContext context);
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request);
    AnalysisCapabilities GetCapabilities();
}
```

### 2. Fallback Chain Implementation
```csharp
public class AnalysisStrategyChain
{
    private readonly IAnalysisStrategy[] _strategies;
    
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request)
    {
        foreach (var strategy in _strategies)
        {
            if (strategy.CanHandle(request, context))
            {
                try
                {
                    var result = await strategy.AnalyzeAsync(request);
                    result.StrategyUsed = strategy.Type;
                    result.Capabilities = strategy.GetCapabilities();
                    return result;
                }
                catch (Exception ex)
                {
                    // Log and try next strategy
                    continue;
                }
            }
        }
        
        throw new AllStrategiesFailedException();
    }
}
```

### 3. Strategy-Specific Implementations

#### Semantic Roslyn Strategy (Preferred)
- Full compilation and semantic model
- Complete type information
- Cross-reference resolution
- Most accurate results

#### Syntax Roslyn Strategy (Fallback 1)
- Parse syntax trees without compilation
- Basic symbol recognition
- No type resolution
- Good for structural analysis

#### Text-Based Strategy (Fallback 2)
- Regex pattern matching
- File content search
- No syntax understanding
- Always works, limited accuracy

#### Hybrid Strategy (Fallback 3)
- Combines multiple approaches
- Uses best available information
- Merges results intelligently

## Implementation Steps

1. **Create Strategy Interfaces**
   ```csharp
   - IAnalysisStrategy base interface
   - AnalysisCapabilities definition
   - Strategy registration system
   ```

2. **Implement Individual Strategies**
   ```csharp
   - SemanticRoslynStrategy (existing logic)
   - SyntaxRoslynStrategy (new)
   - TextBasedStrategy (new)
   - HybridStrategy (new)
   ```

3. **Update Analysis Handlers**
   ```csharp
   - Replace direct Roslyn calls with strategy chain
   - Add capability reporting
   - Include strategy used in responses
   ```

4. **Add New Fallback Tools**
   ```csharp
   - find_symbol_text: Text-based symbol search
   - find_usages_text: Text-based usage search
   - analyze_syntax: Syntax-only analysis
   ```

## Strategy Capabilities Matrix

| Strategy | Symbol Resolution | Type Info | Cross-Ref | Performance | Reliability |
|----------|-------------------|-----------|-----------|-------------|-------------|
| Semantic | ✅ Full          | ✅ Full   | ✅ Full   | ⚠️ Slow     | ⚠️ Fragile  |
| Syntax   | ✅ Basic         | ❌ None   | ⚠️ Limited| ⚡ Fast     | ✅ Reliable |
| Text     | ⚠️ Pattern       | ❌ None   | ❌ None   | ⚡ Fast     | ✅ Always   |
| Hybrid   | ✅ Best Available| ⚠️ Mixed  | ⚠️ Mixed  | ⚠️ Variable | ✅ Robust   |

## Acceptance Criteria

- [ ] Tools never fail completely due to analysis issues
- [ ] Clear communication about analysis limitations
- [ ] Performance remains acceptable for all strategies
- [ ] Results indicate which strategy was used
- [ ] Capability matrix accurately reflects limitations
- [ ] Seamless strategy switching

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/Analysis/Strategies/`
  - `IAnalysisStrategy.cs`
  - `SemanticRoslynStrategy.cs`
  - `SyntaxRoslynStrategy.cs`
  - `TextBasedStrategy.cs`
  - `HybridStrategy.cs`
  - `AnalysisStrategyChain.cs`

**Modified Files:**
- All analysis handlers to use strategy chain
- Response models to include strategy information
- MCP tool definitions for new fallback tools

## Text-Based Analysis Patterns

### Symbol Finding
```csharp
// Class definitions
@"(public|private|internal|protected)?\s*(static\s+)?(partial\s+)?class\s+(?<name>\w+)"

// Method definitions  
@"(public|private|internal|protected)?\s*(static\s+)?(async\s+)?(?<returnType>\w+)\s+(?<name>\w+)\s*\("

// Property definitions
@"(public|private|internal|protected)?\s*(static\s+)?(?<type>\w+)\s+(?<name>\w+)\s*{\s*(get|set)"
```

### Usage Finding
```csharp
// Method calls
@"(?<target>\w+)\.(?<method>\w+)\s*\("

// Property access
@"(?<target>\w+)\.(?<property>\w+)"

// Type usage
@"new\s+(?<type>\w+)\s*\("
```

## Testing Strategy

- Strategy selection logic tests
- Fallback chain validation
- Performance comparison between strategies
- Accuracy validation for each strategy
- Integration tests with problematic codebases

## Success Metrics

- 100% tool availability (always return some result)
- <2 second fallback activation time
- Clear capability communication to users
- 90%+ accuracy for text-based fallbacks on simple queries
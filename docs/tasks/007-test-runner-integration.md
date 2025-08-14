# Task 007: Test Runner Integration

## Priority: 🚀 Feature
**Status**: Pending  
**Estimated Effort**: 8 hours  
**Dependencies**: Task 002

## Problem Description

Developers need to run tests directly from the MCP to validate changes and ensure code quality. The MCP should support discovering, executing, and reporting test results across different test frameworks.

### Current Limitations
- No test execution capabilities
- No test discovery
- No integration with popular test frameworks
- No test impact analysis

### Desired Capabilities
- Discover tests across projects
- Execute specific tests or test suites
- Support multiple test frameworks (xUnit, NUnit, MSTest)
- Provide detailed test results and coverage
- Test impact analysis (which tests are affected by changes)

## Solution Design

### 1. Test Discovery Service
```csharp
public interface ITestDiscoveryService
{
    Task<TestSuite[]> DiscoverTestsAsync(string projectPath);
    Task<TestMethod[]> FindTestsAsync(string pattern);
    Task<TestFrameworkInfo> DetectTestFrameworkAsync(string projectPath);
}

public record TestSuite
{
    public string Name { get; init; }
    public string ProjectPath { get; init; }
    public TestFramework Framework { get; init; }
    public TestMethod[] Methods { get; init; }
    public TestClass[] Classes { get; init; }
}

public record TestMethod
{
    public string Name { get; init; }
    public string FullName { get; init; }
    public string ClassName { get; init; }
    public string[] Categories { get; init; }
    public bool IsParameterized { get; init; }
    public TestAttribute[] Attributes { get; init; }
}
```

### 2. Test Execution Service
```csharp
public interface ITestExecutionService
{
    Task<TestRunResult> RunTestsAsync(TestRunRequest request);
    Task<TestRunResult> RunSpecificTestsAsync(string[] testNames);
    IAsyncEnumerable<TestResult> RunTestsStreamingAsync(TestRunRequest request);
}

public record TestRunRequest
{
    public string ProjectPath { get; init; }
    public string[] TestPatterns { get; init; }
    public string[] Categories { get; init; }
    public TestRunSettings Settings { get; init; }
    public bool CollectCoverage { get; init; }
    public bool RunInParallel { get; init; }
}

public record TestRunResult
{
    public int TotalTests { get; init; }
    public int PassedTests { get; init; }
    public int FailedTests { get; init; }
    public int SkippedTests { get; init; }
    public TimeSpan Duration { get; init; }
    public TestResult[] Results { get; init; }
    public TestCoverage Coverage { get; init; }
}
```

### 3. Test Framework Abstractions
```csharp
public interface ITestFrameworkAdapter
{
    TestFramework Framework { get; }
    bool CanHandle(string projectPath);
    Task<TestMethod[]> DiscoverTestsAsync(string projectPath);
    Task<TestRunResult> ExecuteTestsAsync(TestRunRequest request);
}

public class XUnitAdapter : ITestFrameworkAdapter
{
    public TestFramework Framework => TestFramework.XUnit;
    
    public async Task<TestRunResult> ExecuteTestsAsync(TestRunRequest request)
    {
        // Use dotnet test with xUnit-specific parameters
        var arguments = BuildXUnitArguments(request);
        return await ExecuteDotnetTestAsync(arguments);
    }
}
```

## Implementation Steps

1. **Create Test Framework Abstractions**
   ```csharp
   - ITestFrameworkAdapter interface
   - Framework-specific adapters (xUnit, NUnit, MSTest)
   - Test discovery logic
   - Test execution coordination
   ```

2. **Implement Test Discovery**
   ```csharp
   - Project scanning for test attributes
   - Test method signature analysis
   - Category and trait extraction
   - Parameterized test handling
   ```

3. **Add Test Execution**
   ```csharp
   - dotnet test integration
   - Custom test runners for advanced scenarios
   - Streaming test results
   - Coverage collection
   ```

4. **Create MCP Test Tools**
   ```csharp
   - discover_tests: Find all tests in project/solution
   - run_tests: Execute tests with filtering
   - test_impact_analysis: Find tests affected by changes
   - test_coverage: Generate coverage reports
   ```

## Test Framework Support

### xUnit Support
```csharp
public class XUnitTestDiscovery
{
    private readonly string[] _testAttributes = { "Fact", "Theory" };
    
    public async Task<TestMethod[]> DiscoverAsync(string projectPath)
    {
        var compilation = await BuildCompilationAsync(projectPath);
        var tests = new List<TestMethod>();
        
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var methods = ExtractTestMethods(syntaxTree, semanticModel);
            tests.AddRange(methods);
        }
        
        return tests.ToArray();
    }
}
```

### NUnit Support
```csharp
public class NUnitTestDiscovery
{
    private readonly string[] _testAttributes = { "Test", "TestCase", "TestCaseSource" };
    // Similar implementation with NUnit-specific logic
}
```

### MSTest Support
```csharp
public class MSTestDiscovery
{
    private readonly string[] _testAttributes = { "TestMethod", "DataTestMethod" };
    // Similar implementation with MSTest-specific logic
}
```

## MCP Tool Definitions

### 1. Discover Tests Tool
```json
{
  "name": "discover_tests",
  "description": "Discover all tests in a project or solution",
  "inputSchema": {
    "type": "object",
    "properties": {
      "projectPath": { "type": "string" },
      "framework": { "type": "string", "enum": ["auto", "xunit", "nunit", "mstest"] },
      "includeCategories": { "type": "array", "items": { "type": "string" } },
      "pattern": { "type": "string" }
    }
  }
}
```

### 2. Run Tests Tool
```json
{
  "name": "run_tests",
  "description": "Execute tests with optional filtering and coverage",
  "inputSchema": {
    "type": "object",
    "properties": {
      "projectPath": { "type": "string" },
      "testPattern": { "type": "string" },
      "categories": { "type": "array" },
      "collectCoverage": { "type": "boolean" },
      "runInParallel": { "type": "boolean" },
      "timeout": { "type": "number" }
    }
  }
}
```

### 3. Test Impact Analysis Tool
```json
{
  "name": "test_impact_analysis",
  "description": "Find tests affected by code changes",
  "inputSchema": {
    "type": "object",
    "properties": {
      "projectPath": { "type": "string" },
      "changedFiles": { "type": "array", "items": { "type": "string" } },
      "analysisDepth": { "type": "string", "enum": ["direct", "transitive", "full"] }
    }
  }
}
```

## Test Impact Analysis

### Direct Impact Analysis
```csharp
public async Task<TestImpact[]> AnalyzeDirectImpactAsync(string[] changedFiles)
{
    var impacts = new List<TestImpact>();
    
    foreach (var file in changedFiles)
    {
        // Find tests that directly reference symbols in changed file
        var symbols = await ExtractSymbolsFromFileAsync(file);
        var referencingTests = await FindTestsReferencingSymbolsAsync(symbols);
        
        impacts.Add(new TestImpact
        {
            ChangedFile = file,
            AffectedTests = referencingTests,
            ImpactType = ImpactType.Direct
        });
    }
    
    return impacts.ToArray();
}
```

### Transitive Impact Analysis
```csharp
public async Task<TestImpact[]> AnalyzeTransitiveImpactAsync(string[] changedFiles)
{
    // Build dependency graph
    var dependencyGraph = await BuildDependencyGraphAsync();
    
    // Find all files that depend on changed files
    var transitivelyAffected = new HashSet<string>();
    
    foreach (var changedFile in changedFiles)
    {
        var dependents = dependencyGraph.GetTransitiveDependents(changedFile);
        transitivelyAffected.UnionWith(dependents);
    }
    
    // Find tests that reference transitively affected files
    return await AnalyzeDirectImpactAsync(transitivelyAffected.ToArray());
}
```

## Acceptance Criteria

- [ ] Supports xUnit, NUnit, and MSTest frameworks
- [ ] Discovers tests across solution/project
- [ ] Executes tests with detailed results
- [ ] Provides test coverage information
- [ ] Streaming test execution with real-time updates
- [ ] Test impact analysis for changed files
- [ ] Performance <30 seconds for medium projects

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/Testing/`
  - `ITestDiscoveryService.cs`
  - `ITestExecutionService.cs`
  - `ITestFrameworkAdapter.cs`
  - `XUnitAdapter.cs`
  - `NUnitAdapter.cs`
  - `MSTestAdapter.cs`
  - `TestImpactAnalyzer.cs`

- `src/DotNetMcp.Core/Features/Testing/`
  - `DiscoverTestsHandler.cs`
  - `RunTestsHandler.cs`
  - `TestImpactAnalysisHandler.cs`

**Modified Files:**
- VSA service registration for test services
- MCP server to include test tools

## Testing Strategy

- Unit tests for each test framework adapter
- Integration tests with real test projects
- Performance tests with large test suites
- Cross-platform testing (Windows/Linux/macOS)
- Coverage validation tests

## Success Metrics

- 100% test discovery accuracy for supported frameworks
- <5 second test discovery for medium projects
- Real-time test result streaming
- 95%+ accuracy for test impact analysis
- Coverage collection working for all frameworks
# Task 003: Enhanced Error Reporting & Diagnostics

## Priority: 🎯 High
**Status**: Pending  
**Estimated Effort**: 3 hours  
**Dependencies**: None

## Problem Description

Current error reporting is generic and unhelpful. Users get "Tool execution failed" without understanding why or how to fix the issue. We need detailed, actionable error messages with suggestions.

### Current Behavior
```json
{
  "error": "MCP error -32603: Tool execution failed"
}
```

### Desired Behavior
```json
{
  "error": {
    "code": "DUPLICATE_FILES_DETECTED",
    "message": "Analysis failed due to duplicate file names",
    "details": {
      "duplicateFiles": [
        {
          "fileName": "GlobalUsings.cs",
          "locations": [
            "Flo.Core/GlobalUsings.cs",
            "Flo.Infrastructure/GlobalUsings.cs"
          ]
        }
      ],
      "suggestion": "Try analyzing individual projects instead of the full solution",
      "alternatives": ["analyze_project", "find_symbol_usages_text"]
    },
    "context": {
      "projectPath": "/path/to/solution",
      "analysisType": "symbol_usages",
      "filesProcessed": 157,
      "failurePoint": "compilation_creation"
    }
  }
}
```

## Solution Design

### 1. Structured Error Types
```csharp
public abstract record AnalysisError
{
    public abstract string Code { get; }
    public abstract string Message { get; }
    public abstract string Suggestion { get; }
    public abstract string[] Alternatives { get; }
}

public record DuplicateFilesError : AnalysisError
{
    public override string Code => "DUPLICATE_FILES_DETECTED";
    public DuplicateFileInfo[] DuplicateFiles { get; init; }
    // ... implementation
}

public record BuildValidationError : AnalysisError
{
    public override string Code => "BUILD_VALIDATION_FAILED";
    public BuildError[] CompilationErrors { get; init; }
    public string ErrorSummary { get; init; }
    // ... implementation
}
```

### 2. Error Context Collection
```csharp
public record AnalysisContext
{
    public string ProjectPath { get; init; }
    public string AnalysisType { get; init; }
    public int FilesProcessed { get; init; }
    public string FailurePoint { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public Dictionary<string, object> Metadata { get; init; }
}
```

### 3. Progressive Error Disclosure
```csharp
public record ErrorResponse
{
    public string QuickSummary { get; init; }
    public AnalysisError DetailedError { get; init; }
    public AnalysisContext Context { get; init; }
    public string[] RecommendedActions { get; init; }
    public bool CanRetryWithDifferentStrategy { get; init; }
}
```

## Implementation Steps

1. **Create Error Type Hierarchy**
   - Define base error types
   - Implement specific error categories
   - Add error context collection

2. **Update All Handlers**
   - Replace generic exceptions with typed errors
   - Add context collection at key points
   - Implement error transformation pipeline

3. **Enhanced MCP Error Responses**
   - Structured JSON error responses
   - Include suggestions and alternatives
   - Progressive disclosure (summary + details)

4. **Add Error Recovery Hints**
   - Alternative tool suggestions
   - Retry strategies
   - Workaround documentation

## Acceptance Criteria

- [ ] All errors have specific error codes
- [ ] Error messages include actionable suggestions
- [ ] Context information helps debugging
- [ ] Alternative tools/strategies are suggested
- [ ] Errors are categorized by severity
- [ ] Performance impact is minimal

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/Common/Errors/`
  - `AnalysisError.cs`
  - `BuildValidationError.cs` 
  - `DuplicateFilesError.cs`
  - `ProjectDiscoveryError.cs`
- `src/DotNetMcp.Core/Common/ErrorContext.cs`

**Modified Files:**
- All handler classes to use structured errors
- `Result<T>` class to support detailed errors
- MCP server response formatting

## Error Categories

### 1. Configuration Errors
- Invalid project paths
- Missing required files
- Malformed configuration

### 2. Build/Compilation Errors  
- Build validation failures
- Compilation errors
- Missing dependencies

### 3. Analysis Errors
- Duplicate file conflicts
- Symbol resolution failures
- Memory/performance limits

### 4. Infrastructure Errors
- File system access issues
- Permission problems
- Network/external service failures

## Testing Strategy

- Error injection tests for each error type
- End-to-end error flow validation
- Error message clarity validation
- Performance impact measurement

## Success Metrics

- 100% of errors have specific codes
- Users can resolve 80%+ of issues from error messages
- Support ticket reduction for "tool execution failed"
- Developer satisfaction improvement
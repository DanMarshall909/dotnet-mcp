# Task 001: Fix Duplicate File Name Handling

## Priority: 🔥 Critical
**Status**: Pending  
**Estimated Effort**: 4 hours  
**Dependencies**: None

## Problem Description

The MCP tools fail when analyzing projects with duplicate file names (e.g., multiple `GlobalUsings.cs` files across different projects in a solution). This causes Roslyn compilation to fail with duplicate key errors.

### Current Behavior
```
dotnet-mcp-vsa - find_symbol_usages (projectPath: "/path/to/solution", symbolName: "Task.Delay")
Error: MCP error -32603: Tool execution failed
```

### Root Cause
Roslyn `CSharpCompilation.Create()` uses file names as keys, causing conflicts when multiple projects have files with the same name.

## Solution Design

### 1. Implement Unique File Identification
```csharp
// Before: Using file name as key
compilation.AddSyntaxTrees(syntaxTree)

// After: Using full path with project context
var uniqueKey = $"{projectName}::{relativePath}";
compilation = compilation.AddSyntaxTrees(syntaxTree.WithFilePath(uniqueKey));
```

### 2. Project-Aware Compilation Strategy
- Analyze each project independently
- Merge results with conflict resolution
- Maintain project context throughout analysis

### 3. Enhanced Error Handling
```csharp
public record DuplicateFileError
{
    public string FileName { get; init; }
    public string[] ProjectPaths { get; init; }
    public string Suggestion { get; init; }
}
```

## Implementation Steps

1. **Update BuildValidationService**
   - Add project discovery logic
   - Implement project-isolated validation

2. **Modify Roslyn Compilation Strategy**
   - Create per-project compilations
   - Implement result merging logic

3. **Enhanced Error Reporting**
   - Detect duplicate file scenarios
   - Provide actionable error messages

4. **Add Integration Tests**
   - Test with multi-project solutions
   - Verify duplicate file handling

## Acceptance Criteria

- [ ] Can analyze solutions with duplicate file names
- [ ] Clear error messages when conflicts occur
- [ ] Performance remains acceptable for large solutions
- [ ] All existing tests continue to pass

## Files to Modify

- `src/DotNetMcp.Core/Services/BuildValidationService.cs`
- `src/DotNetMcp.Core/Features/CodeAnalysis/FindSymbolUsages/FindSymbolUsagesHandler.cs`
- `src/DotNetMcp.Core/Features/CodeAnalysis/FindSymbol/FindSymbolHandler.cs`
- `src/DotNetMcp.Core/Features/CodeAnalysis/GetClassContext/GetClassContextHandler.cs`

## Testing Strategy

- Unit tests with mock file systems containing duplicates
- Integration tests with real multi-project solutions
- Performance tests with large codebases

## Success Metrics

- Zero duplicate key compilation errors
- Successful analysis of complex solutions (like Flo project)
- Clear diagnostic information when issues occur
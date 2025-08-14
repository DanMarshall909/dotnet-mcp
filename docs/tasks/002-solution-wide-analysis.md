# Task 002: Solution-Wide Analysis Support

## Priority: 🔥 Critical
**Status**: Pending  
**Estimated Effort**: 6 hours  
**Dependencies**: Task 001

## Problem Description

Current MCP tools work well for single projects but struggle with solution-wide analysis. The tools need to intelligently handle solution files, discover projects, and provide unified analysis across multiple projects.

### Current Limitations
- No solution file (.sln) parsing
- No cross-project reference resolution
- No unified workspace management
- Tools fail on complex project structures

## Solution Design

### 1. Solution Discovery Service
```csharp
public class SolutionDiscoveryService
{
    public async Task<WorkspaceInfo> AnalyzeSolutionAsync(string solutionPath);
    public async Task<ProjectInfo[]> DiscoverProjectsAsync(string rootPath);
    public async Task<DependencyGraph> BuildDependencyGraphAsync(ProjectInfo[] projects);
}
```

### 2. Workspace Management
```csharp
public record WorkspaceInfo
{
    public string SolutionPath { get; init; }
    public ProjectInfo[] Projects { get; init; }
    public DependencyGraph Dependencies { get; init; }
    public GlobalUsingsInfo GlobalUsings { get; init; }
}

public record ProjectInfo  
{
    public string Name { get; init; }
    public string Path { get; init; }
    public string[] SourceFiles { get; init; }
    public PackageReference[] PackageReferences { get; init; }
    public ProjectReference[] ProjectReferences { get; init; }
}
```

### 3. Analysis Strategies
- **Full Solution**: Analyze entire solution as one unit
- **Project Isolation**: Analyze each project independently  
- **Dependency-Aware**: Respect project dependencies
- **Incremental**: Analyze only changed parts

## Implementation Steps

1. **Create SolutionDiscoveryService**
   ```csharp
   - Parse .sln files (MSBuild format)
   - Discover .csproj files
   - Build dependency graph
   - Handle global using files
   ```

2. **Update BuildValidationService**
   ```csharp
   - Add solution-aware validation
   - Validate project dependencies
   - Handle cross-project references
   ```

3. **Enhance Analysis Handlers**
   ```csharp
   - Add workspace context to all handlers
   - Implement cross-project symbol resolution
   - Add solution-level aggregation
   ```

4. **Add New MCP Tools**
   ```csharp
   - analyze_solution: Full solution analysis
   - discover_projects: Project discovery
   - dependency_graph: Show project dependencies
   ```

## Acceptance Criteria

- [ ] Can parse and analyze .sln files
- [ ] Discovers all projects in a solution
- [ ] Handles cross-project references correctly
- [ ] Provides unified analysis results
- [ ] Maintains performance with large solutions
- [ ] Graceful handling of missing/invalid projects

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/Services/SolutionDiscoveryService.cs`
- `src/DotNetMcp.Core/Services/WorkspaceManager.cs`
- `src/DotNetMcp.Core/Features/SolutionAnalysis/`

**Modified Files:**
- All analysis handlers to support workspace context
- `BuildValidationService.cs` for solution validation
- VSA service registration

## Testing Strategy

- Unit tests with mock solution structures
- Integration tests with real solutions (simple & complex)
- Performance tests with large solutions (50+ projects)
- Cross-project reference resolution tests

## Success Metrics

- Successful analysis of complex solutions like Flo project
- Sub-5 second analysis for medium solutions (10-20 projects)
- 100% project discovery accuracy
- Zero cross-reference resolution failures
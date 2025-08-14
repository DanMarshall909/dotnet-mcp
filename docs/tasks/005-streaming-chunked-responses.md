# Task 005: Streaming & Chunked Responses

## Priority: 🛠️ Medium
**Status**: Pending  
**Estimated Effort**: 4 hours  
**Dependencies**: None

## Problem Description

Large codebases can take significant time to analyze, leaving users without feedback. The MCP should provide streaming responses and progress updates for long-running operations.

### Current Behavior
- No progress indication for long operations
- Users wait without feedback
- Large responses sent as single blocks
- Poor UX for complex analysis

### Desired Behavior
- Real-time progress updates
- Streaming results as they're found
- Chunked responses for large datasets
- Cancellation support

## Solution Design

### 1. Streaming Response Protocol
```json
{
  "type": "progress",
  "operation": "find_symbol_usages",
  "progress": {
    "current": 45,
    "total": 120,
    "percentage": 37.5,
    "currentFile": "src/Infrastructure/Services/UserService.cs",
    "phase": "analyzing_files",
    "estimatedTimeRemaining": "00:02:15"
  }
}

{
  "type": "partial_result",
  "chunk": 1,
  "totalChunks": null,
  "data": {
    "symbols": [...],
    "metadata": { "analysisStrategy": "semantic" }
  }
}

{
  "type": "completion",
  "summary": {
    "totalResults": 47,
    "totalChunks": 3,
    "analysisTime": "00:04:23",
    "strategyUsed": "semantic"
  }
}
```

### 2. Progress Tracking System
```csharp
public interface IProgressReporter
{
    Task ReportProgressAsync(ProgressInfo progress);
    Task ReportPartialResultAsync<T>(T partialResult, int chunk);
    Task ReportCompletionAsync(CompletionSummary summary);
}

public record ProgressInfo
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string CurrentItem { get; init; }
    public string Phase { get; init; }
    public TimeSpan EstimatedTimeRemaining { get; init; }
}
```

### 3. Chunked Response Management
```csharp
public class ChunkedResponseManager<T>
{
    private readonly IProgressReporter _progressReporter;
    private readonly int _chunkSize;
    
    public async Task StreamResultsAsync(IAsyncEnumerable<T> results)
    {
        var chunk = new List<T>();
        var chunkNumber = 1;
        
        await foreach (var result in results)
        {
            chunk.Add(result);
            
            if (chunk.Count >= _chunkSize)
            {
                await _progressReporter.ReportPartialResultAsync(chunk, chunkNumber++);
                chunk.Clear();
            }
        }
        
        if (chunk.Any())
        {
            await _progressReporter.ReportPartialResultAsync(chunk, chunkNumber);
        }
    }
}
```

## Implementation Steps

1. **Create Progress Infrastructure**
   - Progress reporting interfaces
   - Chunked response managers
   - Cancellation token support

2. **Update Analysis Handlers**
   - Add progress reporting to all long operations
   - Implement streaming result generation
   - Add cancellation support

3. **Enhance MCP Protocol**
   - Support multiple response messages per request
   - Add progress message types
   - Implement proper message ordering

4. **Add Cancellation Support**
   - CancellationToken propagation
   - Graceful operation termination
   - Partial result preservation

## Streaming Scenarios

### 1. Symbol Usage Analysis
```csharp
public async IAsyncEnumerable<SymbolUsage> FindSymbolUsagesStreamingAsync(
    string symbolName, 
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var files = await DiscoverFilesAsync();
    
    for (int i = 0; i < files.Length; i++)
    {
        await _progressReporter.ReportProgressAsync(new ProgressInfo
        {
            Current = i + 1,
            Total = files.Length,
            CurrentItem = files[i],
            Phase = "analyzing_files"
        });
        
        var usages = await AnalyzeFileAsync(files[i], symbolName, cancellationToken);
        
        foreach (var usage in usages)
        {
            yield return usage;
        }
    }
}
```

### 2. Project Structure Analysis
```csharp
public async IAsyncEnumerable<ProjectStructureInfo> AnalyzeProjectStructureStreamingAsync(
    string projectPath,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // Stream results as each project is analyzed
    var projects = await DiscoverProjectsAsync(projectPath);
    
    foreach (var project in projects)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var structureInfo = await AnalyzeProjectAsync(project);
        yield return structureInfo;
    }
}
```

## Acceptance Criteria

- [ ] Progress updates for operations >2 seconds
- [ ] Chunked responses for results >50 items
- [ ] Cancellation support for all long operations
- [ ] Real-time result streaming
- [ ] Proper error handling in streams
- [ ] Performance overhead <5%

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/Common/Streaming/`
  - `IProgressReporter.cs`
  - `ChunkedResponseManager.cs`
  - `StreamingExtensions.cs`
- `src/DotNetMcp.Server/Streaming/`
  - `McpProgressReporter.cs`
  - `StreamingResponseHandler.cs`

**Modified Files:**
- All analysis handlers to support streaming
- MCP server to handle multiple response messages
- Response models to support chunking metadata

## Performance Considerations

### Chunk Size Optimization
- Small files: 10-20 results per chunk
- Large datasets: 50-100 results per chunk
- Adaptive chunking based on result size

### Progress Granularity
- File-level progress for detailed operations
- Project-level progress for solution analysis
- Avoid excessive progress updates (<10ms intervals)

### Memory Management
- Stream processing to avoid loading all results
- Dispose resources promptly
- Monitor memory usage during streaming

## Testing Strategy

- Streaming functionality with mock data
- Cancellation behavior validation
- Progress accuracy verification
- Performance impact measurement
- Large dataset streaming tests

## Success Metrics

- Users see progress within 1 second of operation start
- Cancellation works within 2 seconds
- Memory usage remains constant during streaming
- No performance degradation for small operations
# Task 006: Caching & Performance Optimization

## Priority: 🛠️ Medium
**Status**: Pending  
**Estimated Effort**: 6 hours  
**Dependencies**: Task 002

## Problem Description

Repeated analysis operations on the same codebase are inefficient. The MCP should cache build validation results, symbol indices, and analysis outcomes to improve performance for subsequent operations.

### Current Behavior
- Every operation rebuilds compilation from scratch
- Build validation runs every time
- No caching of discovered symbols or project structure
- Poor performance for repeated operations

### Desired Behavior
- Intelligent caching of expensive operations
- Cache invalidation based on file changes
- Significant performance improvement for repeated queries
- Memory-efficient caching strategies

## Solution Design

### 1. Multi-Level Caching Architecture
```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task InvalidateAsync(string pattern);
    Task InvalidateByFileChangesAsync(string[] changedFiles);
}

public class CacheManager
{
    // L1: In-memory cache (fast, small)
    private readonly IMemoryCache _memoryCache;
    
    // L2: File-based cache (persistent, larger)
    private readonly IFileCache _fileCache;
    
    // L3: Symbol index cache (searchable)
    private readonly ISymbolIndexCache _symbolCache;
}
```

### 2. Cache Categories

#### Build Validation Cache
```csharp
public record BuildValidationCacheEntry
{
    public string ProjectPath { get; init; }
    public DateTime LastAnalyzed { get; init; }
    public string[] FileHashes { get; init; }
    public BuildValidationResult Result { get; init; }
    public TimeSpan AnalysisDuration { get; init; }
}
```

#### Symbol Index Cache
```csharp
public record SymbolIndexCacheEntry
{
    public string ProjectPath { get; init; }
    public DateTime LastIndexed { get; init; }
    public Dictionary<string, SymbolLocation[]> SymbolIndex { get; init; }
    public Dictionary<string, string[]> UsageIndex { get; init; }
    public string[] FileHashes { get; init; }
}
```

#### Compilation Cache
```csharp
public record CompilationCacheEntry
{
    public string ProjectKey { get; init; }
    public DateTime CreatedAt { get; init; }
    public Compilation Compilation { get; init; }
    public string[] SourceFileHashes { get; init; }
    public TimeSpan CompilationTime { get; init; }
}
```

### 3. Cache Invalidation Strategy
```csharp
public class FileWatcherCacheInvalidator
{
    private readonly FileSystemWatcher _watcher;
    private readonly ICacheService _cacheService;
    
    private async Task OnFileChanged(string filePath)
    {
        // Invalidate related cache entries
        var affectedKeys = await GetAffectedCacheKeysAsync(filePath);
        
        foreach (var key in affectedKeys)
        {
            await _cacheService.InvalidateAsync(key);
        }
    }
}
```

## Implementation Steps

1. **Create Cache Infrastructure**
   ```csharp
   - ICacheService interface and implementations
   - Memory + file-based cache providers
   - Cache key generation strategies
   - Expiration and invalidation logic
   ```

2. **Implement File Change Monitoring**
   ```csharp
   - FileSystemWatcher integration
   - File hash calculation
   - Incremental change detection
   - Cache invalidation triggers
   ```

3. **Add Caching to Services**
   ```csharp
   - BuildValidationService caching
   - Symbol analysis result caching
   - Compilation object caching
   - Project structure caching
   ```

4. **Optimize Cache Strategies**
   ```csharp
   - LRU eviction for memory cache
   - Compressed storage for file cache
   - Background cache warming
   - Cache hit/miss metrics
   ```

## Caching Strategies by Operation

### Build Validation (High Value)
- **Cache Key**: Project path + file modification times
- **TTL**: Until file changes detected
- **Storage**: Memory (small) + File (persistent)
- **Invalidation**: File system events

### Symbol Discovery (Medium Value)
- **Cache Key**: Project path + analysis type + file hashes
- **TTL**: 1 hour or file changes
- **Storage**: File-based (searchable index)
- **Invalidation**: File changes in analyzed files

### Compilation Objects (High CPU Cost)
- **Cache Key**: Source files hash + references hash
- **TTL**: Until source changes
- **Storage**: Memory only (large objects)
- **Invalidation**: Source file changes

### Project Structure (Low Change Frequency)
- **Cache Key**: Solution/project path + modification time
- **TTL**: 24 hours or project file changes
- **Storage**: File-based (JSON)
- **Invalidation**: .csproj/.sln file changes

## Performance Optimization Techniques

### 1. Incremental Analysis
```csharp
public async Task<AnalysisResult> AnalyzeIncrementalAsync(
    string projectPath, 
    string[] changedFiles)
{
    var cachedResult = await _cache.GetAsync<AnalysisResult>(projectPath);
    
    if (cachedResult != null && !HasRelevantChanges(changedFiles, cachedResult))
    {
        return cachedResult; // Cache hit
    }
    
    // Partial reanalysis of only changed files
    var incrementalResult = await AnalyzeChangedFilesAsync(changedFiles);
    var mergedResult = MergeResults(cachedResult, incrementalResult);
    
    await _cache.SetAsync(projectPath, mergedResult);
    return mergedResult;
}
```

### 2. Background Cache Warming
```csharp
public class CacheWarmingService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var projects = await DiscoverRecentProjectsAsync();
            
            foreach (var project in projects)
            {
                if (!await _cache.ExistsAsync(project.CacheKey))
                {
                    await WarmCacheAsync(project);
                }
            }
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

### 3. Lazy Loading & Prefetching
```csharp
public async Task<T> GetOrCreateAsync<T>(
    string key, 
    Func<Task<T>> factory,
    CacheOptions options = null)
{
    // Try cache first
    var cached = await _cache.GetAsync<T>(key);
    if (cached != null) return cached;
    
    // Create and cache
    var value = await factory();
    await _cache.SetAsync(key, value, options?.Expiry);
    
    // Prefetch related data in background
    _ = Task.Run(() => PrefetchRelatedDataAsync(key, value));
    
    return value;
}
```

## Acceptance Criteria

- [ ] 80%+ cache hit rate for repeated operations
- [ ] <100ms cache lookup time
- [ ] Automatic cache invalidation on file changes
- [ ] Memory usage <500MB for typical projects
- [ ] Cache persistence across MCP restarts
- [ ] Configurable cache policies

## Files to Create/Modify

**New Files:**
- `src/DotNetMcp.Core/Caching/`
  - `ICacheService.cs`
  - `MemoryCache.cs`
  - `FileCacheService.cs`
  - `CacheManager.cs`
  - `FileWatcherCacheInvalidator.cs`
  - `CacheWarmingService.cs`

**Modified Files:**
- `BuildValidationService.cs` - Add caching
- All analysis handlers - Add result caching
- VSA service registration - Add cache services

## Cache Configuration

### Memory Cache Settings
```json
{
  "caching": {
    "memory": {
      "maxSizeMB": 256,
      "defaultTTL": "01:00:00",
      "evictionPolicy": "LRU"
    },
    "file": {
      "basePath": "./cache",
      "maxSizeMB": 1024,
      "compressionEnabled": true
    }
  }
}
```

## Testing Strategy

- Cache hit/miss ratio measurement
- Performance before/after benchmarks
- Memory usage monitoring
- Cache invalidation correctness
- Multi-project cache efficiency tests

## Success Metrics

- 2-5x performance improvement for repeated operations
- 80%+ cache hit rate after initial warm-up
- <500MB memory usage for cache
- Sub-100ms cache operations
- Accurate cache invalidation (no stale data)
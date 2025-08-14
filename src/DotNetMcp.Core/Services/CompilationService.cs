using System.IO.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Services;

/// <summary>
/// Service for creating and managing Roslyn compilations with duplicate file handling
/// </summary>
public class CompilationService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CompilationService> _logger;
    private readonly Dictionary<string, Compilation> _compilationCache = new();

    public CompilationService(IFileSystem fileSystem, ILogger<CompilationService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Creates a compilation from multiple files with proper duplicate handling
    /// </summary>
    public async Task<Compilation> CreateCompilationAsync(string[] filePaths, string assemblyName = "TempAssembly")
    {
        var cacheKey = string.Join("|", filePaths.OrderBy(f => f)) + "|" + assemblyName;
        
        if (_compilationCache.TryGetValue(cacheKey, out var cachedCompilation))
        {
            return cachedCompilation;
        }

        var syntaxTrees = new List<SyntaxTree>();
        var processedFiles = new HashSet<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                // Create unique identifier for files with same name
                var uniqueFilePath = CreateUniqueFilePath(filePath, processedFiles);
                
                if (!_fileSystem.File.Exists(filePath))
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                    continue;
                }

                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content, path: uniqueFilePath);
                
                syntaxTrees.Add(syntaxTree);
                processedFiles.Add(uniqueFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
                // Continue with other files
            }
        }

        if (!syntaxTrees.Any())
        {
            throw new InvalidOperationException("No valid syntax trees were created from the provided files");
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: GetBasicReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        _compilationCache[cacheKey] = compilation;
        return compilation;
    }

    /// <summary>
    /// Creates a compilation from a single file
    /// </summary>
    public async Task<Compilation> CreateSingleFileCompilationAsync(string filePath, string assemblyName = "TempAssembly")
    {
        return await CreateCompilationAsync(new[] { filePath }, assemblyName);
    }

    /// <summary>
    /// Creates unique file path to avoid duplicate key issues
    /// </summary>
    private string CreateUniqueFilePath(string originalPath, HashSet<string> processedFiles)
    {
        var fileName = _fileSystem.Path.GetFileName(originalPath);
        var directory = _fileSystem.Path.GetDirectoryName(originalPath);
        
        // If filename is unique, use as-is
        var basePath = originalPath;
        if (!processedFiles.Any(p => _fileSystem.Path.GetFileName(p) == fileName))
        {
            return basePath;
        }

        // Create unique path by including parent directory info
        var parentDir = _fileSystem.Path.GetFileName(directory);
        var uniquePath = $"{parentDir}_{fileName}";
        
        // If still not unique, add more context
        var counter = 1;
        var candidatePath = uniquePath;
        while (processedFiles.Contains(candidatePath))
        {
            candidatePath = $"{uniquePath}_{counter++}";
        }

        _logger.LogDebug("Resolved duplicate file name: {Original} -> {Unique}", originalPath, candidatePath);
        return candidatePath;
    }

    /// <summary>
    /// Gets the semantic model for a specific file from a compilation
    /// </summary>
    public SemanticModel? GetSemanticModel(Compilation compilation, string originalFilePath)
    {
        var fileName = _fileSystem.Path.GetFileName(originalFilePath);
        
        // Find the syntax tree that corresponds to this file
        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(tree => 
            tree.FilePath.EndsWith(fileName) || 
            tree.FilePath == originalFilePath ||
            _fileSystem.Path.GetFileName(tree.FilePath) == fileName);

        if (syntaxTree == null)
        {
            _logger.LogWarning("Could not find syntax tree for file: {FilePath}", originalFilePath);
            return null;
        }

        return compilation.GetSemanticModel(syntaxTree);
    }

    /// <summary>
    /// Gets the syntax tree for a specific file from a compilation
    /// </summary>
    public SyntaxTree? GetSyntaxTree(Compilation compilation, string originalFilePath)
    {
        var fileName = _fileSystem.Path.GetFileName(originalFilePath);
        
        return compilation.SyntaxTrees.FirstOrDefault(tree => 
            tree.FilePath.EndsWith(fileName) || 
            tree.FilePath == originalFilePath ||
            _fileSystem.Path.GetFileName(tree.FilePath) == fileName);
    }

    /// <summary>
    /// Discovers and groups files by project to avoid cross-project conflicts
    /// </summary>
    public async Task<Dictionary<string, string[]>> GroupFilesByProjectAsync(string[] filePaths)
    {
        var groupedFiles = new Dictionary<string, List<string>>();

        foreach (var filePath in filePaths)
        {
            var projectPath = await FindProjectPathAsync(filePath);
            var projectKey = projectPath ?? "Unknown";

            if (!groupedFiles.ContainsKey(projectKey))
            {
                groupedFiles[projectKey] = new List<string>();
            }
            
            groupedFiles[projectKey].Add(filePath);
        }

        return groupedFiles.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    /// <summary>
    /// Finds the project file (.csproj) that contains the given source file
    /// </summary>
    private async Task<string?> FindProjectPathAsync(string sourceFilePath)
    {
        var directory = _fileSystem.Path.GetDirectoryName(sourceFilePath);
        
        while (!string.IsNullOrEmpty(directory))
        {
            var projectFiles = _fileSystem.Directory.GetFiles(directory, "*.csproj");
            if (projectFiles.Any())
            {
                return projectFiles.First();
            }

            var parentDirectory = _fileSystem.Path.GetDirectoryName(directory);
            if (parentDirectory == directory) break; // Reached root
            directory = parentDirectory;
        }

        return null;
    }

    /// <summary>
    /// Clears the compilation cache
    /// </summary>
    public void ClearCache()
    {
        _compilationCache.Clear();
    }

    /// <summary>
    /// Gets basic .NET references for compilation
    /// </summary>
    private static MetadataReference[] GetBasicReferences()
    {
        return new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "System.Runtime").Location),
            MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "System.Collections").Location)
        };
    }
}

/// <summary>
/// Result of compilation with error handling
/// </summary>
public record CompilationResult
{
    public Compilation? Compilation { get; init; }
    public bool Success { get; init; }
    public string[] Errors { get; init; } = Array.Empty<string>();
    public string[] Warnings { get; init; } = Array.Empty<string>();
    public Dictionary<string, string> FileMapping { get; init; } = new();
}

/// <summary>
/// Options for compilation creation
/// </summary>
public record CompilationOptions
{
    public bool IgnoreDuplicateFiles { get; init; } = true;
    public bool GroupByProject { get; init; } = true;
    public string AssemblyName { get; init; } = "TempAssembly";
    public bool CacheResults { get; init; } = true;
}
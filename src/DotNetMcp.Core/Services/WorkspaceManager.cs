using System.IO.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Services;

/// <summary>
/// Manages workspace-wide operations and analysis strategies
/// </summary>
public class WorkspaceManager
{
    private readonly SolutionDiscoveryService _solutionDiscovery;
    private readonly CompilationService _compilationService;
    private readonly IBuildValidationService _buildValidation;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<WorkspaceManager> _logger;

    private WorkspaceInfo? _currentWorkspace;
    private readonly Dictionary<string, Compilation> _projectCompilations = new();

    public WorkspaceManager(
        SolutionDiscoveryService solutionDiscovery,
        CompilationService compilationService,
        IBuildValidationService buildValidation,
        IFileSystem fileSystem,
        ILogger<WorkspaceManager> logger)
    {
        _solutionDiscovery = solutionDiscovery;
        _compilationService = compilationService;
        _buildValidation = buildValidation;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Loads and analyzes a workspace from a path (solution or directory)
    /// </summary>
    public async Task<WorkspaceAnalysisResult> LoadWorkspaceAsync(string path, WorkspaceAnalysisOptions? options = null)
    {
        options ??= new WorkspaceAnalysisOptions();
        
        try
        {
            _logger.LogInformation("Loading workspace: {Path}", path);

            // Determine if path is solution file or directory
            WorkspaceInfo workspace;
            if (_fileSystem.Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                workspace = await _solutionDiscovery.AnalyzeSolutionAsync(path);
            }
            else if (_fileSystem.Directory.Exists(path))
            {
                // Look for solution file in directory first
                var solutionFiles = _fileSystem.Directory.GetFiles(path, "*.sln");
                if (solutionFiles.Length > 0)
                {
                    workspace = await _solutionDiscovery.AnalyzeSolutionAsync(solutionFiles.First());
                }
                else
                {
                    // Discover projects directly
                    var projects = await _solutionDiscovery.DiscoverProjectsAsync(path);
                    var dependencies = _solutionDiscovery.BuildDependencyGraphAsync(projects);
                    
                    workspace = new WorkspaceInfo
                    {
                        SolutionPath = path,
                        Projects = projects,
                        Dependencies = dependencies,
                        GlobalUsings = new GlobalUsingsInfo()
                    };
                }
            }
            else
            {
                return WorkspaceAnalysisResult.CreateFailure($"Path not found: {path}");
            }

            _currentWorkspace = workspace;

            // Validate build if requested
            var buildResults = new List<BuildValidationResult>();
            if (options.ValidateBuild)
            {
                foreach (var project in workspace.Projects)
                {
                    var buildResult = await _buildValidation.ValidateBuildAsync(project.Directory);
                    buildResults.Add(buildResult);
                }
            }

            // Pre-compile projects if requested
            if (options.PrecompileProjects)
            {
                await PrecompileProjectsAsync(workspace);
            }

            return WorkspaceAnalysisResult.CreateSuccess(workspace, buildResults.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace: {Path}", path);
            return WorkspaceAnalysisResult.CreateFailure($"Failed to load workspace: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets compilation for a specific project, creating if needed
    /// </summary>
    public async Task<Compilation?> GetProjectCompilationAsync(string projectName)
    {
        if (_currentWorkspace == null)
        {
            throw new InvalidOperationException("No workspace loaded");
        }

        if (_projectCompilations.TryGetValue(projectName, out var existingCompilation))
        {
            return existingCompilation;
        }

        var project = _currentWorkspace.Projects.FirstOrDefault(p => p.Name == projectName);
        if (project == null)
        {
            _logger.LogWarning("Project not found: {ProjectName}", projectName);
            return null;
        }

        try
        {
            var compilation = await _compilationService.CreateCompilationAsync(
                project.SourceFiles, 
                $"{projectName}_Assembly");

            _projectCompilations[projectName] = compilation;
            return compilation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create compilation for project: {ProjectName}", projectName);
            return null;
        }
    }

    /// <summary>
    /// Gets compilation for the entire workspace
    /// </summary>
    public async Task<Compilation?> GetWorkspaceCompilationAsync()
    {
        if (_currentWorkspace == null)
        {
            throw new InvalidOperationException("No workspace loaded");
        }

        try
        {
            var allSourceFiles = _currentWorkspace.Projects
                .SelectMany(p => p.SourceFiles)
                .ToArray();

            var compilation = await _compilationService.CreateCompilationAsync(
                allSourceFiles, 
                "WorkspaceAssembly");

            return compilation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace compilation");
            return null;
        }
    }

    /// <summary>
    /// Finds the best analysis strategy for given requirements
    /// </summary>
    public AnalysisStrategy DetermineAnalysisStrategy(AnalysisRequirements requirements)
    {
        if (_currentWorkspace == null)
        {
            return AnalysisStrategy.TextBased;
        }

        // Check if all projects can build
        var hasComplexStructure = _currentWorkspace.Projects.Length > 10;
        var hasCircularDependencies = _currentWorkspace.Dependencies.HasCircularDependencies();
        var hasGlobalUsingConflicts = _currentWorkspace.GlobalUsings.ConflictingUsings.Any();

        if (requirements.RequireSemanticAnalysis && !hasCircularDependencies && !hasGlobalUsingConflicts)
        {
            return hasComplexStructure ? AnalysisStrategy.ProjectIsolated : AnalysisStrategy.SemanticWorkspace;
        }

        if (requirements.RequireSymbolResolution)
        {
            return AnalysisStrategy.SyntaxOnly;
        }

        return AnalysisStrategy.TextBased;
    }

    /// <summary>
    /// Executes analysis using the appropriate strategy
    /// </summary>
    public async Task<TResult> ExecuteAnalysisAsync<TRequest, TResult>(
        TRequest request, 
        Func<TRequest, Compilation, Task<TResult>> semanticAnalysis,
        Func<TRequest, string[], Task<TResult>> textAnalysis,
        AnalysisRequirements? requirements = null)
    {
        requirements ??= new AnalysisRequirements();
        var strategy = DetermineAnalysisStrategy(requirements);

        _logger.LogDebug("Using analysis strategy: {Strategy}", strategy);

        try
        {
            switch (strategy)
            {
                case AnalysisStrategy.SemanticWorkspace:
                    var workspaceCompilation = await GetWorkspaceCompilationAsync();
                    if (workspaceCompilation != null)
                    {
                        return await semanticAnalysis(request, workspaceCompilation);
                    }
                    goto case AnalysisStrategy.TextBased;

                case AnalysisStrategy.ProjectIsolated:
                    // For complex solutions, analyze each project separately
                    // This is a simplified implementation - would need request-specific logic
                    var firstProject = _currentWorkspace?.Projects.FirstOrDefault();
                    if (firstProject != null)
                    {
                        var projectCompilation = await GetProjectCompilationAsync(firstProject.Name);
                        if (projectCompilation != null)
                        {
                            return await semanticAnalysis(request, projectCompilation);
                        }
                    }
                    goto case AnalysisStrategy.TextBased;

                case AnalysisStrategy.SyntaxOnly:
                    // Use syntax-only analysis (would need implementation)
                    goto case AnalysisStrategy.TextBased;

                case AnalysisStrategy.TextBased:
                default:
                    var allFiles = _currentWorkspace?.Projects.SelectMany(p => p.SourceFiles).ToArray() 
                                   ?? Array.Empty<string>();
                    return await textAnalysis(request, allFiles);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed with strategy {Strategy}, falling back to text analysis", strategy);
            
            var allFiles = _currentWorkspace?.Projects.SelectMany(p => p.SourceFiles).ToArray() 
                           ?? Array.Empty<string>();
            return await textAnalysis(request, allFiles);
        }
    }

    /// <summary>
    /// Gets current workspace information
    /// </summary>
    public WorkspaceInfo? GetCurrentWorkspace() => _currentWorkspace;

    /// <summary>
    /// Clears the current workspace and all cached data
    /// </summary>
    public void ClearWorkspace()
    {
        _currentWorkspace = null;
        _projectCompilations.Clear();
        _compilationService.ClearCache();
        _logger.LogInformation("Workspace cleared");
    }

    /// <summary>
    /// Pre-compiles all projects for better performance
    /// </summary>
    private async Task PrecompileProjectsAsync(WorkspaceInfo workspace)
    {
        _logger.LogInformation("Pre-compiling {Count} projects", workspace.Projects.Length);

        var tasks = workspace.Projects.Select(async project =>
        {
            try
            {
                await GetProjectCompilationAsync(project.Name);
                _logger.LogDebug("Pre-compiled project: {ProjectName}", project.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pre-compile project: {ProjectName}", project.Name);
            }
        });

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Options for workspace analysis
/// </summary>
public record WorkspaceAnalysisOptions
{
    public bool ValidateBuild { get; init; } = true;
    public bool PrecompileProjects { get; init; } = false;
    public bool AnalyzeDependencies { get; init; } = true;
    public bool DiscoverGlobalUsings { get; init; } = true;
    public int MaxProjects { get; init; } = 100;
}

/// <summary>
/// Result of workspace analysis
/// </summary>
public record WorkspaceAnalysisResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public WorkspaceInfo? Workspace { get; init; }
    public BuildValidationResult[] BuildResults { get; init; } = Array.Empty<BuildValidationResult>();

    public static WorkspaceAnalysisResult CreateSuccess(WorkspaceInfo workspace, BuildValidationResult[] buildResults)
        => new() { Success = true, Workspace = workspace, BuildResults = buildResults };

    public static WorkspaceAnalysisResult CreateFailure(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Analysis strategy enumeration
/// </summary>
public enum AnalysisStrategy
{
    SemanticWorkspace,  // Full semantic analysis across entire workspace
    ProjectIsolated,    // Semantic analysis per project
    SyntaxOnly,         // Syntax-only analysis without compilation
    TextBased          // Text/regex-based analysis
}

/// <summary>
/// Requirements for analysis
/// </summary>
public record AnalysisRequirements
{
    public bool RequireSemanticAnalysis { get; init; } = false;
    public bool RequireSymbolResolution { get; init; } = false;
    public bool RequireCrossProjectAnalysis { get; init; } = false;
    public bool AllowFallback { get; init; } = true;
}
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Services;

/// <summary>
/// Service for discovering and analyzing solution structures
/// </summary>
public class SolutionDiscoveryService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SolutionDiscoveryService> _logger;

    public SolutionDiscoveryService(IFileSystem fileSystem, ILogger<SolutionDiscoveryService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a solution file and returns comprehensive workspace information
    /// </summary>
    public async Task<WorkspaceInfo> AnalyzeSolutionAsync(string solutionPath)
    {
        if (!_fileSystem.File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");
        }

        _logger.LogInformation("Analyzing solution: {SolutionPath}", solutionPath);

        var solutionContent = await _fileSystem.File.ReadAllTextAsync(solutionPath);
        var projects = ParseSolutionFile(solutionContent, solutionPath);
        var projectInfos = new List<ProjectInfo>();

        foreach (var projectPath in projects)
        {
            try
            {
                var projectInfo = await AnalyzeProjectAsync(projectPath);
                projectInfos.Add(projectInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze project: {ProjectPath}", projectPath);
            }
        }

        var dependencyGraph = BuildDependencyGraphAsync(projectInfos.ToArray());
        var globalUsings = await DiscoverGlobalUsingsAsync(projectInfos);

        return new WorkspaceInfo
        {
            SolutionPath = solutionPath,
            Projects = projectInfos.ToArray(),
            Dependencies = dependencyGraph,
            GlobalUsings = globalUsings
        };
    }

    /// <summary>
    /// Discovers projects in a directory recursively
    /// </summary>
    public async Task<ProjectInfo[]> DiscoverProjectsAsync(string rootPath)
    {
        if (!_fileSystem.Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");
        }

        _logger.LogInformation("Discovering projects in: {RootPath}", rootPath);

        var projectFiles = _fileSystem.Directory
            .GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToArray();

        var projects = new List<ProjectInfo>();

        foreach (var projectFile in projectFiles)
        {
            try
            {
                var projectInfo = await AnalyzeProjectAsync(projectFile);
                projects.Add(projectInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze project: {ProjectFile}", projectFile);
            }
        }

        return projects.ToArray();
    }

    /// <summary>
    /// Analyzes a single project file
    /// </summary>
    public async Task<ProjectInfo> AnalyzeProjectAsync(string projectPath)
    {
        if (!_fileSystem.File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }

        var projectContent = await _fileSystem.File.ReadAllTextAsync(projectPath);
        var projectDirectory = _fileSystem.Path.GetDirectoryName(projectPath)!;
        var projectName = _fileSystem.Path.GetFileNameWithoutExtension(projectPath);

        // Find source files
        var sourceFiles = _fileSystem.Directory
            .GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToArray();

        // Parse project references
        var projectReferences = ParseProjectReferences(projectContent, projectDirectory);
        var packageReferences = ParsePackageReferences(projectContent);

        return new ProjectInfo
        {
            Name = projectName,
            Path = projectPath,
            Directory = projectDirectory,
            SourceFiles = sourceFiles,
            ProjectReferences = projectReferences,
            PackageReferences = packageReferences,
            TargetFramework = ParseTargetFramework(projectContent),
            ProjectType = DetermineProjectType(projectContent, sourceFiles)
        };
    }

    /// <summary>
    /// Builds dependency graph between projects
    /// </summary>
    public DependencyGraph BuildDependencyGraphAsync(ProjectInfo[] projects)
    {
        var graph = new DependencyGraph();

        foreach (var project in projects)
        {
            graph.AddProject(project);

            foreach (var reference in project.ProjectReferences)
            {
                var referencedProject = projects.FirstOrDefault(p => 
                    _fileSystem.Path.GetFullPath(p.Path) == _fileSystem.Path.GetFullPath(reference.ProjectPath));

                if (referencedProject != null)
                {
                    graph.AddDependency(project, referencedProject);
                }
            }
        }

        return graph;
    }

    /// <summary>
    /// Parses solution file to extract project paths
    /// </summary>
    private string[] ParseSolutionFile(string content, string solutionPath)
    {
        var solutionDirectory = _fileSystem.Path.GetDirectoryName(solutionPath)!;
        var projectPattern = @"Project\(""{[^}]+}""\)\s*=\s*""[^""]+"",\s*""([^""]+\.csproj)""";
        var matches = Regex.Matches(content, projectPattern);

        var projects = new List<string>();

        foreach (Match match in matches)
        {
            var relativePath = match.Groups[1].Value;
            var absolutePath = _fileSystem.Path.Combine(solutionDirectory, relativePath);
            var normalizedPath = _fileSystem.Path.GetFullPath(absolutePath);

            if (_fileSystem.File.Exists(normalizedPath))
            {
                projects.Add(normalizedPath);
            }
            else
            {
                _logger.LogWarning("Project file not found: {ProjectPath}", normalizedPath);
            }
        }

        return projects.ToArray();
    }

    /// <summary>
    /// Parses project references from project file content
    /// </summary>
    private ProjectReference[] ParseProjectReferences(string projectContent, string projectDirectory)
    {
        var pattern = @"<ProjectReference\s+Include\s*=\s*""([^""]+)""\s*/>";
        var matches = Regex.Matches(projectContent, pattern);

        var references = new List<ProjectReference>();

        foreach (Match match in matches)
        {
            var relativePath = match.Groups[1].Value;
            var absolutePath = _fileSystem.Path.Combine(projectDirectory, relativePath);
            var normalizedPath = _fileSystem.Path.GetFullPath(absolutePath);

            references.Add(new ProjectReference
            {
                ProjectPath = normalizedPath,
                RelativePath = relativePath
            });
        }

        return references.ToArray();
    }

    /// <summary>
    /// Parses package references from project file content
    /// </summary>
    private PackageReference[] ParsePackageReferences(string projectContent)
    {
        var pattern = @"<PackageReference\s+Include\s*=\s*""([^""]+)""\s+Version\s*=\s*""([^""]+)""[^>]*/?>";
        var matches = Regex.Matches(projectContent, pattern);

        var references = new List<PackageReference>();

        foreach (Match match in matches)
        {
            references.Add(new PackageReference
            {
                PackageName = match.Groups[1].Value,
                Version = match.Groups[2].Value
            });
        }

        return references.ToArray();
    }

    /// <summary>
    /// Parses target framework from project file
    /// </summary>
    private string ParseTargetFramework(string projectContent)
    {
        var singleFrameworkPattern = @"<TargetFramework>([^<]+)</TargetFramework>";
        var multiFrameworkPattern = @"<TargetFrameworks>([^<]+)</TargetFrameworks>";

        var singleMatch = Regex.Match(projectContent, singleFrameworkPattern);
        if (singleMatch.Success)
        {
            return singleMatch.Groups[1].Value;
        }

        var multiMatch = Regex.Match(projectContent, multiFrameworkPattern);
        if (multiMatch.Success)
        {
            return multiMatch.Groups[1].Value.Split(';').First();
        }

        return "net9.0"; // Default
    }

    /// <summary>
    /// Determines project type based on content and files
    /// </summary>
    private ProjectType DetermineProjectType(string projectContent, string[] sourceFiles)
    {
        if (projectContent.Contains("<OutputType>Exe</OutputType>"))
            return ProjectType.Console;

        if (projectContent.Contains("Microsoft.NET.Test.Sdk") || 
            sourceFiles.Any(f => _fileSystem.Path.GetFileName(f).Contains("Test")))
            return ProjectType.Test;

        if (projectContent.Contains("Microsoft.AspNetCore"))
            return ProjectType.Web;

        return ProjectType.Library;
    }

    /// <summary>
    /// Discovers global using files across projects
    /// </summary>
    private async Task<GlobalUsingsInfo> DiscoverGlobalUsingsAsync(List<ProjectInfo> projects)
    {
        var globalUsingsFiles = new List<GlobalUsingsFile>();

        foreach (var project in projects)
        {
            var globalUsingsPath = _fileSystem.Path.Combine(project.Directory, "GlobalUsings.cs");
            
            if (_fileSystem.File.Exists(globalUsingsPath))
            {
                var content = await _fileSystem.File.ReadAllTextAsync(globalUsingsPath);
                var usings = ParseGlobalUsings(content);

                globalUsingsFiles.Add(new GlobalUsingsFile
                {
                    ProjectName = project.Name,
                    FilePath = globalUsingsPath,
                    Usings = usings
                });
            }
        }

        return new GlobalUsingsInfo
        {
            Files = globalUsingsFiles.ToArray(),
            ConflictingUsings = FindConflictingUsings(globalUsingsFiles)
        };
    }

    /// <summary>
    /// Parses global using statements from content
    /// </summary>
    private string[] ParseGlobalUsings(string content)
    {
        var pattern = @"global\s+using\s+([^;]+);";
        var matches = Regex.Matches(content, pattern);

        return matches.Select(m => m.Groups[1].Value.Trim()).ToArray();
    }

    /// <summary>
    /// Finds conflicting global using statements
    /// </summary>
    private GlobalUsingConflict[] FindConflictingUsings(List<GlobalUsingsFile> files)
    {
        var usingsByNamespace = files
            .SelectMany(f => f.Usings.Select(u => new { File = f, Using = u }))
            .GroupBy(x => x.Using)
            .Where(g => g.Count() > 1)
            .Select(g => new GlobalUsingConflict
            {
                UsingStatement = g.Key,
                ConflictingProjects = g.Select(x => x.File.ProjectName).ToArray()
            })
            .ToArray();

        return usingsByNamespace;
    }
}

/// <summary>
/// Information about a complete workspace/solution
/// </summary>
public record WorkspaceInfo
{
    public string SolutionPath { get; init; } = "";
    public ProjectInfo[] Projects { get; init; } = Array.Empty<ProjectInfo>();
    public DependencyGraph Dependencies { get; init; } = new();
    public GlobalUsingsInfo GlobalUsings { get; init; } = new();
}

/// <summary>
/// Information about a single project
/// </summary>
public record ProjectInfo
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Directory { get; init; } = "";
    public string[] SourceFiles { get; init; } = Array.Empty<string>();
    public ProjectReference[] ProjectReferences { get; init; } = Array.Empty<ProjectReference>();
    public PackageReference[] PackageReferences { get; init; } = Array.Empty<PackageReference>();
    public string TargetFramework { get; init; } = "";
    public ProjectType ProjectType { get; init; } = ProjectType.Library;
}

/// <summary>
/// Project reference information
/// </summary>
public record ProjectReference
{
    public string ProjectPath { get; init; } = "";
    public string RelativePath { get; init; } = "";
}

/// <summary>
/// Package reference information
/// </summary>
public record PackageReference
{
    public string PackageName { get; init; } = "";
    public string Version { get; init; } = "";
}

/// <summary>
/// Project type enumeration
/// </summary>
public enum ProjectType
{
    Library,
    Console,
    Web,
    Test,
    Unknown
}

/// <summary>
/// Dependency graph for projects
/// </summary>
public class DependencyGraph
{
    private readonly Dictionary<string, ProjectInfo> _projects = new();
    private readonly Dictionary<string, HashSet<string>> _dependencies = new();

    public void AddProject(ProjectInfo project)
    {
        _projects[project.Name] = project;
        if (!_dependencies.ContainsKey(project.Name))
        {
            _dependencies[project.Name] = new HashSet<string>();
        }
    }

    public void AddDependency(ProjectInfo from, ProjectInfo to)
    {
        if (!_dependencies.ContainsKey(from.Name))
        {
            _dependencies[from.Name] = new HashSet<string>();
        }
        
        _dependencies[from.Name].Add(to.Name);
    }

    public string[] GetDependencies(string projectName)
    {
        return _dependencies.ContainsKey(projectName) 
            ? _dependencies[projectName].ToArray() 
            : Array.Empty<string>();
    }

    public string[] GetTransitiveDependencies(string projectName)
    {
        var visited = new HashSet<string>();
        var result = new HashSet<string>();
        GetTransitiveDependenciesRecursive(projectName, visited, result);
        return result.ToArray();
    }

    private void GetTransitiveDependenciesRecursive(string projectName, HashSet<string> visited, HashSet<string> result)
    {
        if (visited.Contains(projectName)) return;
        visited.Add(projectName);

        if (_dependencies.ContainsKey(projectName))
        {
            foreach (var dependency in _dependencies[projectName])
            {
                result.Add(dependency);
                GetTransitiveDependenciesRecursive(dependency, visited, result);
            }
        }
    }

    public bool HasCircularDependencies()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var project in _projects.Keys)
        {
            if (HasCircularDependenciesRecursive(project, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCircularDependenciesRecursive(string projectName, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(projectName)) return true;
        if (visited.Contains(projectName)) return false;

        visited.Add(projectName);
        recursionStack.Add(projectName);

        if (_dependencies.ContainsKey(projectName))
        {
            foreach (var dependency in _dependencies[projectName])
            {
                if (HasCircularDependenciesRecursive(dependency, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(projectName);
        return false;
    }
}

/// <summary>
/// Information about global using files
/// </summary>
public record GlobalUsingsInfo
{
    public GlobalUsingsFile[] Files { get; init; } = Array.Empty<GlobalUsingsFile>();
    public GlobalUsingConflict[] ConflictingUsings { get; init; } = Array.Empty<GlobalUsingConflict>();
}

/// <summary>
/// Information about a global using file
/// </summary>
public record GlobalUsingsFile
{
    public string ProjectName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string[] Usings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Conflicting global using statement
/// </summary>
public record GlobalUsingConflict
{
    public string UsingStatement { get; init; } = "";
    public string[] ConflictingProjects { get; init; } = Array.Empty<string>();
}
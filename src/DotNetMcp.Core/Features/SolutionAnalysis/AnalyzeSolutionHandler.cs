using System.IO.Abstractions;
using DotNetMcp.Core.Common;
using DotNetMcp.Core.Services;
using DotNetMcp.Core.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.SolutionAnalysis;

/// <summary>
/// Handler for analyzing solution structure and dependencies
/// </summary>
public class AnalyzeSolutionHandler : BaseHandler<AnalyzeSolutionCommand, AnalyzeSolutionResponse>
{
    private readonly WorkspaceManager _workspaceManager;
    private readonly IFileSystem _fileSystem;

    public AnalyzeSolutionHandler(
        ILogger<AnalyzeSolutionHandler> logger,
        WorkspaceManager workspaceManager,
        IFileSystem fileSystem) 
        : base(logger)
    {
        _workspaceManager = workspaceManager;
        _fileSystem = fileSystem;
    }

    protected override async Task<Result<AnalyzeSolutionResponse>> HandleAsync(AnalyzeSolutionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Analyzing solution: {SolutionPath}", request.SolutionPath);

            // Load workspace
            var workspaceOptions = new WorkspaceAnalysisOptions
            {
                ValidateBuild = request.ValidateBuilds,
                AnalyzeDependencies = request.IncludeDependencyGraph
            };

            var workspaceResult = await _workspaceManager.LoadWorkspaceAsync(request.SolutionPath, workspaceOptions);
            
            if (!workspaceResult.Success)
            {
                return Result<AnalyzeSolutionResponse>.Failure($"Failed to load workspace: {workspaceResult.ErrorMessage}");
            }

            var workspace = workspaceResult.Workspace!;

            // Create solution summary
            var summary = CreateSolutionSummary(workspace);

            // Analyze projects
            var projectAnalyses = Array.Empty<ProjectAnalysis>();
            if (request.IncludeProjectDetails)
            {
                projectAnalyses = await AnalyzeProjectsAsync(workspace.Projects);
            }

            // Analyze dependencies
            var dependencyAnalysis = new DependencyAnalysis();
            if (request.IncludeDependencyGraph)
            {
                dependencyAnalysis = AnalyzeDependencies(workspace.Dependencies);
            }

            // Detect issues
            var issues = Array.Empty<SolutionIssue>();
            if (request.DetectIssues)
            {
                issues = DetectSolutionIssues(workspace, dependencyAnalysis);
            }

            // Create build summary
            BuildSummary? buildSummary = null;
            if (request.ValidateBuilds && workspaceResult.BuildResults.Any())
            {
                buildSummary = CreateBuildSummary(workspace.Projects, workspaceResult.BuildResults);
            }

            var response = new AnalyzeSolutionResponse
            {
                Summary = summary,
                Projects = projectAnalyses,
                Dependencies = dependencyAnalysis,
                Issues = issues,
                BuildSummary = buildSummary
            };

            Logger.LogInformation("Solution analysis completed: {ProjectCount} projects, {IssueCount} issues", 
                workspace.Projects.Length, issues.Length);

            return Result<AnalyzeSolutionResponse>.Success(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error analyzing solution: {SolutionPath}", request.SolutionPath);
            return Result<AnalyzeSolutionResponse>.Failure($"Error analyzing solution: {ex.Message}", ex);
        }
    }

    private SolutionSummary CreateSolutionSummary(WorkspaceInfo workspace)
    {
        var targetFrameworks = workspace.Projects
            .Select(p => p.TargetFramework)
            .Distinct()
            .ToArray();

        var projectTypes = workspace.Projects
            .GroupBy(p => p.ProjectType)
            .Select(g => new ProjectTypeSummary { Type = g.Key, Count = g.Count() })
            .ToArray();

        var totalSourceFiles = workspace.Projects.Sum(p => p.SourceFiles.Length);

        return new SolutionSummary
        {
            SolutionPath = workspace.SolutionPath,
            ProjectCount = workspace.Projects.Length,
            SourceFileCount = totalSourceFiles,
            TargetFrameworks = targetFrameworks,
            ProjectTypes = projectTypes
        };
    }

    private async Task<ProjectAnalysis[]> AnalyzeProjectsAsync(ProjectInfo[] projects)
    {
        var analyses = new List<ProjectAnalysis>();

        foreach (var project in projects)
        {
            try
            {
                var metrics = await CalculateProjectMetricsAsync(project);
                
                var analysis = new ProjectAnalysis
                {
                    Name = project.Name,
                    Path = project.Path,
                    Type = project.ProjectType,
                    TargetFramework = project.TargetFramework,
                    SourceFileCount = project.SourceFiles.Length,
                    ProjectReferenceCount = project.ProjectReferences.Length,
                    PackageReferenceCount = project.PackageReferences.Length,
                    Dependencies = project.ProjectReferences.Select(r => _fileSystem.Path.GetFileNameWithoutExtension(r.ProjectPath)).ToArray(),
                    Metrics = metrics
                };

                analyses.Add(analysis);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to analyze project: {ProjectName}", project.Name);
            }
        }

        return analyses.ToArray();
    }

    private async Task<ProjectMetrics> CalculateProjectMetricsAsync(ProjectInfo project)
    {
        int linesOfCode = 0;
        int classCount = 0;
        int interfaceCount = 0;
        int testCount = 0;

        foreach (var sourceFile in project.SourceFiles)
        {
            try
            {
                if (_fileSystem.File.Exists(sourceFile))
                {
                    var content = await _fileSystem.File.ReadAllTextAsync(sourceFile);
                    var lines = content.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line.Trim())).Count();
                    linesOfCode += lines;

                    // Simple pattern matching for counting types
                    classCount += CountPattern(content, @"\bclass\s+\w+");
                    interfaceCount += CountPattern(content, @"\binterface\s+\w+");
                    
                    // Count test methods
                    if (sourceFile.Contains("Test", StringComparison.OrdinalIgnoreCase))
                    {
                        testCount += CountPattern(content, @"\[Test\]|\[Fact\]|\[TestMethod\]");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to analyze source file: {SourceFile}", sourceFile);
            }
        }

        return new ProjectMetrics
        {
            LinesOfCode = linesOfCode,
            ClassCount = classCount,
            InterfaceCount = interfaceCount,
            TestCount = testCount
        };
    }

    private int CountPattern(string content, string pattern)
    {
        return System.Text.RegularExpressions.Regex.Matches(content, pattern).Count;
    }

    private DependencyAnalysis AnalyzeDependencies(DependencyGraph dependencyGraph)
    {
        var nodes = new List<DependencyNode>();
        var circularDependencies = new List<string>();

        // Check for circular dependencies
        if (dependencyGraph.HasCircularDependencies())
        {
            circularDependencies.Add("Circular dependencies detected in solution");
        }

        // Create dependency nodes (simplified implementation)
        // In a full implementation, you'd iterate through all projects in the graph

        return new DependencyAnalysis
        {
            Graph = nodes.ToArray(),
            CircularDependencies = circularDependencies.ToArray(),
            LayerViolations = Array.Empty<LayerViolation>(), // Would need architecture rules
            MaxDepth = 0 // Would calculate based on dependency chain
        };
    }

    private SolutionIssue[] DetectSolutionIssues(WorkspaceInfo workspace, DependencyAnalysis dependencies)
    {
        var issues = new List<SolutionIssue>();

        // Check for circular dependencies
        if (dependencies.CircularDependencies.Any())
        {
            issues.Add(new SolutionIssue
            {
                Type = SolutionIssueType.CircularDependency,
                Title = "Circular Dependencies Detected",
                Description = "The solution contains circular dependencies that may cause build issues",
                Severity = IssueSeverity.Error,
                Recommendation = "Review project references and break circular dependencies"
            });
        }

        // Check for framework inconsistencies
        var frameworks = workspace.Projects.Select(p => p.TargetFramework).Distinct().ToArray();
        if (frameworks.Length > 3)
        {
            issues.Add(new SolutionIssue
            {
                Type = SolutionIssueType.FrameworkInconsistency,
                Title = "Multiple Target Frameworks",
                Description = $"Solution uses {frameworks.Length} different target frameworks: {string.Join(", ", frameworks)}",
                Severity = IssueSeverity.Warning,
                Recommendation = "Consider standardizing on fewer target frameworks for easier maintenance"
            });
        }

        // Check for duplicate global usings
        if (workspace.GlobalUsings.ConflictingUsings.Any())
        {
            foreach (var conflict in workspace.GlobalUsings.ConflictingUsings)
            {
                issues.Add(new SolutionIssue
                {
                    Type = SolutionIssueType.DuplicateGlobalUsings,
                    Title = "Duplicate Global Using",
                    Description = $"Global using '{conflict.UsingStatement}' is defined in multiple projects",
                    Severity = IssueSeverity.Warning,
                    AffectedProjects = conflict.ConflictingProjects,
                    Recommendation = "Consolidate global usings or use project-specific using statements"
                });
            }
        }

        // Check for projects with no source files
        var emptyProjects = workspace.Projects.Where(p => !p.SourceFiles.Any()).ToArray();
        if (emptyProjects.Any())
        {
            issues.Add(new SolutionIssue
            {
                Type = SolutionIssueType.UnusedProjects,
                Title = "Empty Projects",
                Description = $"{emptyProjects.Length} projects contain no source files",
                Severity = IssueSeverity.Info,
                AffectedProjects = emptyProjects.Select(p => p.Name).ToArray(),
                Recommendation = "Consider removing unused projects or adding source files"
            });
        }

        return issues.ToArray();
    }

    private BuildSummary CreateBuildSummary(ProjectInfo[] projects, BuildValidationResult[] buildResults)
    {
        var projectResults = new List<ProjectBuildResult>();

        for (int i = 0; i < Math.Min(projects.Length, buildResults.Length); i++)
        {
            var project = projects[i];
            var buildResult = buildResults[i];

            projectResults.Add(new ProjectBuildResult
            {
                ProjectName = project.Name,
                Success = buildResult.IsSuccess,
                ErrorCount = buildResult.Errors?.Count ?? 0,
                WarningCount = 0, // Would need to extract from build output
                BuildTime = TimeSpan.Zero // Would need to measure
            });
        }

        var successful = projectResults.Count(r => r.Success);
        var failed = projectResults.Count(r => !r.Success);

        return new BuildSummary
        {
            TotalProjects = projects.Length,
            SuccessfulBuilds = successful,
            FailedBuilds = failed,
            Results = projectResults.ToArray()
        };
    }
}
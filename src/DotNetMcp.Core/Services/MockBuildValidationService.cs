using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Services;

/// <summary>
/// Mock implementation of build validation service for testing scenarios
/// </summary>
public class MockBuildValidationService : IBuildValidationService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<MockBuildValidationService> _logger;

    public MockBuildValidationService(IFileSystem fileSystem, ILogger<MockBuildValidationService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Mock implementation that performs basic file system validation without actual build commands
    /// </summary>
    public Task<BuildValidationResult> ValidateBuildAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic validation - check if directory exists
            if (!_fileSystem.Directory.Exists(projectPath))
            {
                return Task.FromResult(BuildValidationResult.Warning($"Project directory not found: {projectPath}. Analysis may have limited accuracy."));
            }

            // Look for solution or project files
            var buildTarget = FindBuildTarget(projectPath);
            if (buildTarget == null)
            {
                return Task.FromResult(BuildValidationResult.Warning("No solution or project files found. Analysis may have limited accuracy."));
            }

            _logger.LogInformation("Mock build validation passed for {BuildTarget}", buildTarget);

            // In test scenarios, always return success to allow Roslyn analysis to proceed
            return Task.FromResult(BuildValidationResult.Success($"Mock build validation passed for {_fileSystem.Path.GetFileName(buildTarget)}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during mock build validation for {ProjectPath}", projectPath);
            return Task.FromResult(BuildValidationResult.Warning($"Could not validate build: {ex.Message}. Analysis will continue but may have issues."));
        }
    }

    private string? FindBuildTarget(string projectPath)
    {
        try
        {
            // Look for solution file first (preferred)
            var solutionFiles = _fileSystem.Directory
                .GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly);
            
            if (solutionFiles.Any())
            {
                return solutionFiles.First();
            }

            // Look for project files
            var projectFiles = _fileSystem.Directory
                .GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .ToArray();

            // Prefer main project files (not test projects)
            var mainProject = projectFiles.FirstOrDefault(p => 
                !p.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("Spec", StringComparison.OrdinalIgnoreCase));

            return mainProject ?? projectFiles.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding build target in {ProjectPath}", projectPath);
            return null;
        }
    }
}
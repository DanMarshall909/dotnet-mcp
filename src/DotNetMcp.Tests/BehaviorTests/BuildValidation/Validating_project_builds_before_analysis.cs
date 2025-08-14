using DotNetMcp.Core.Services;
using FluentAssertions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetMcp.Tests.BehaviorTests.BuildValidation;

/// <summary>
/// Behavior tests for build validation before Roslyn analysis
/// </summary>
public class Validating_project_builds_before_analysis
{
    private readonly BuildValidationService _buildValidationService;
    private readonly MockFileSystem _fileSystem;

    public Validating_project_builds_before_analysis()
    {
        _fileSystem = new MockFileSystem();
        _buildValidationService = new BuildValidationService(_fileSystem, NullLogger<BuildValidationService>.Instance);
    }

    [Fact]
    public async Task Returns_warning_when_no_project_or_solution_files_found()
    {
        // Arrange
        var projectPath = "/empty/project";
        _fileSystem.AddDirectory(projectPath);

        // Act
        var result = await _buildValidationService.ValidateBuildAsync(projectPath);

        // Assert
        result.IsWarning.Should().BeTrue();
        result.Message.Should().Contain("No solution or project files found");
    }

    [Fact]
    public async Task Prefers_solution_file_over_project_files()
    {
        // Arrange
        var projectPath = "/test/project";
        _fileSystem.AddFile($"{projectPath}/Test.sln", "solution content");
        _fileSystem.AddFile($"{projectPath}/src/Project.csproj", "project content");

        // Act
        var result = await _buildValidationService.ValidateBuildAsync(projectPath);

        // Assert - Should prefer solution file
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Test.sln");
    }

    [Fact]
    public async Task Finds_main_project_when_no_solution_exists()
    {
        // Arrange
        var projectPath = "/test/project";
        _fileSystem.AddFile($"{projectPath}/src/MainProject.csproj", "main project");
        _fileSystem.AddFile($"{projectPath}/test/TestProject.Tests.csproj", "test project");

        // Act
        var result = await _buildValidationService.ValidateBuildAsync(projectPath);

        // Assert - Should prefer non-test project
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("MainProject.csproj");
    }

    [Fact]
    public async Task Returns_success_when_project_file_found()
    {
        // Arrange
        var projectPath = "/test/project";
        _fileSystem.AddFile($"{projectPath}/Project.csproj", "valid project");

        // Act
        var result = await _buildValidationService.ValidateBuildAsync(projectPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Found build target: Project.csproj");
    }

    // TODO: Add build execution tests once we implement actual build validation

    [Fact]
    public async Task Excludes_bin_and_obj_directories_from_project_search()
    {
        // Arrange
        var projectPath = "/test/project";
        _fileSystem.AddFile($"{projectPath}/src/MainProject.csproj", "main project");
        _fileSystem.AddFile($"{projectPath}/bin/Debug/TempProject.csproj", "temp project");
        _fileSystem.AddFile($"{projectPath}/obj/TempProject.csproj", "temp project");

        // Act
        var result = await _buildValidationService.ValidateBuildAsync(projectPath);

        // Assert - Should only find the main project, not the temp ones
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("MainProject.csproj");
    }
}

/// <summary>
/// Integration behavior tests for build validation with real scenarios
/// </summary>
public class Build_validation_integration_scenarios
{
    [Fact]
    public async Task Should_validate_successful_build_before_roslyn_analysis()
    {
        // This test would verify that when a project builds successfully,
        // the Roslyn-based code analysis tools proceed normally
    }

    [Fact] 
    public async Task Should_provide_helpful_error_message_when_build_fails()
    {
        // This test would verify that when a project has compilation errors,
        // the MCP tools return a clear error message explaining why analysis failed
        // and suggest fixing the build errors first
    }

    [Fact]
    public async Task Should_fall_back_to_text_based_analysis_when_build_fails()
    {
        // This test would verify that we can offer alternative analysis methods
        // (like grep-based search) when Roslyn analysis isn't possible
    }

    [Fact]
    public async Task Should_cache_build_validation_results_for_performance()
    {
        // This test would verify that we don't rebuild the same project repeatedly
        // during a single analysis session
    }
}
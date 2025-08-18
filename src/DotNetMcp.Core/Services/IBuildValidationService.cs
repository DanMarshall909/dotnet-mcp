namespace DotNetMcp.Core.Services;

/// <summary>
/// Interface for build validation service
/// </summary>
public interface IBuildValidationService
{
    /// <summary>
    /// Validates that the project can build successfully
    /// </summary>
    Task<BuildValidationResult> ValidateBuildAsync(string projectPath, CancellationToken cancellationToken = default);
}
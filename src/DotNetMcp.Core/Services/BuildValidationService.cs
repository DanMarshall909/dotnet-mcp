using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Services;

/// <summary>
/// Service for validating that projects can build before Roslyn analysis
/// </summary>
public class BuildValidationService : IBuildValidationService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BuildValidationService> _logger;

    public BuildValidationService(IFileSystem fileSystem, ILogger<BuildValidationService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the project can build successfully
    /// </summary>
    public async Task<BuildValidationResult> ValidateBuildAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find solution or project files
            var buildTarget = FindBuildTarget(projectPath);
            if (buildTarget == null)
            {
                return BuildValidationResult.Warning("No solution or project files found. Analysis may have limited accuracy.");
            }

            _logger.LogInformation("Validating build for {BuildTarget}", buildTarget);

            // Run actual build validation
            var buildResult = await RunBuildCommand(buildTarget, cancellationToken);
            
            if (buildResult.Success)
            {
                return BuildValidationResult.Success($"Build validation passed for {_fileSystem.Path.GetFileName(buildTarget)}");
            }
            else
            {
                var errorSummary = CreateErrorSummary(buildResult.Errors);
                return BuildValidationResult.Failure(
                    $"Build failed with {buildResult.ErrorCount} errors. Please fix compilation errors before running analysis.",
                    buildResult.Errors,
                    errorSummary
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during build validation for {ProjectPath}", projectPath);
            return BuildValidationResult.Warning($"Could not validate build: {ex.Message}. Analysis will continue but may have issues.");
        }
    }

    private string? FindBuildTarget(string projectPath)
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

    private async Task<BuildCommandResult> RunBuildCommand(string buildTarget, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"build \"{buildTarget}\" --verbosity quiet --nologo";
            process.StartInfo.WorkingDirectory = _fileSystem.Path.GetDirectoryName(buildTarget);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) => {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            
            process.ErrorDataReceived += (_, e) => {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();
            var success = process.ExitCode == 0;

            var errors = ParseBuildErrors(output + error);

            return new BuildCommandResult
            {
                Success = success,
                ExitCode = process.ExitCode,
                Output = output,
                Error = error,
                Errors = errors,
                ErrorCount = errors.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run build command for {BuildTarget}", buildTarget);
            throw;
        }
    }

    private List<BuildError> ParseBuildErrors(string buildOutput)
    {
        var errors = new List<BuildError>();
        var lines = buildOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Parse MSBuild error format: path(line,col): error CODE: message
            if (line.Contains(": error ") && line.Contains("("))
            {
                try
                {
                    var errorMatch = System.Text.RegularExpressions.Regex.Match(
                        line, 
                        @"^(.+?)\((\d+),(\d+)\):\s+error\s+([^:]+):\s+(.+)$");

                    if (errorMatch.Success)
                    {
                        errors.Add(new BuildError
                        {
                            File = errorMatch.Groups[1].Value,
                            Line = int.Parse(errorMatch.Groups[2].Value),
                            Column = int.Parse(errorMatch.Groups[3].Value),
                            Code = errorMatch.Groups[4].Value.Trim(),
                            Message = errorMatch.Groups[5].Value.Trim(),
                            Severity = "error"
                        });
                    }
                }
                catch
                {
                    // If parsing fails, create a generic error
                    errors.Add(new BuildError
                    {
                        File = "unknown",
                        Line = 0,
                        Column = 0,
                        Code = "unknown",
                        Message = line.Trim(),
                        Severity = "error"
                    });
                }
            }
        }

        return errors;
    }

    private string CreateErrorSummary(List<BuildError> errors)
    {
        if (!errors.Any()) return "No errors found.";

        var errorGroups = errors
            .GroupBy(e => e.Code)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var summary = $"Top error types:\n";
        foreach (var group in errorGroups)
        {
            summary += $"  • {group.Key}: {group.Count()} occurrences\n";
        }

        if (errors.Count > 20)
        {
            summary += $"\nTotal: {errors.Count} errors (showing top 5 types)";
        }

        return summary;
    }
}

/// <summary>
/// Result of build validation
/// </summary>
public record BuildValidationResult
{
    public bool IsSuccess { get; init; }
    public bool IsWarning { get; init; }
    public string Message { get; init; } = "";
    public List<BuildError> Errors { get; init; } = new();
    public string? ErrorSummary { get; init; }

    public static BuildValidationResult Success(string message) => new()
    {
        IsSuccess = true,
        Message = message
    };

    public static BuildValidationResult Warning(string message) => new()
    {
        IsWarning = true,
        Message = message
    };

    public static BuildValidationResult Failure(string message, List<BuildError> errors, string errorSummary) => new()
    {
        IsSuccess = false,
        Message = message,
        Errors = errors,
        ErrorSummary = errorSummary
    };
}

/// <summary>
/// Result of running build command
/// </summary>
public record BuildCommandResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public List<BuildError> Errors { get; init; } = new();
    public int ErrorCount { get; init; }
}

/// <summary>
/// Represents a build error
/// </summary>
public record BuildError
{
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Severity { get; init; } = "";
}
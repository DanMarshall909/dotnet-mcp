using System.Text.Json;
using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.Services;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server;

public class RenameSymbolMultiFileTool(ILogger<RenameSymbolMultiFileTool> logger)
{
    private readonly RenameSymbolRefactorer _refactorer = new();

    public async Task<string> RenameSymbolMultiFile(string solutionPath, string symbolName, string newName, string? targetFilePath = null)
    {
        try
        {
            logger.LogInformation("Starting multi-file rename: {SymbolName} -> {NewName} in {SolutionPath}", 
                symbolName, newName, solutionPath);

            using var engine = new MultiFileRefactoringEngine();
            
            bool loaded;
            if (Directory.Exists(solutionPath))
            {
                // Load all .cs files in directory
                var csFiles = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);
                loaded = await engine.LoadFilesAsync(csFiles);
            }
            else if (File.Exists(solutionPath))
            {
                // Load solution or project
                loaded = await engine.LoadSolutionAsync(solutionPath);
            }
            else
            {
                logger.LogError("Path not found: {SolutionPath}", solutionPath);
                return JsonSerializer.Serialize(new { success = false, error = $"Path not found: {solutionPath}" });
            }

            if (!loaded)
            {
                logger.LogError("Failed to load solution/project: {SolutionPath}", solutionPath);
                return JsonSerializer.Serialize(new { success = false, error = "Failed to load solution/project" });
            }

            var result = await _refactorer.RenameSymbolMultiFileAsync(engine, symbolName, newName, targetFilePath);

            if (result.Success)
            {
                logger.LogInformation("Multi-file rename completed. Affected {FileCount} files with {ChangeCount} changes",
                    result.Deltas.Select(d => d.FilePath).Distinct().Count(),
                    result.Deltas.Sum(d => d.Changes.Count));

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    deltas = result.Deltas.Select(d => new
                    {
                        filePath = d.FilePath,
                        changes = d.Changes.Select(c => new
                        {
                            startLine = c.StartLine,
                            endLine = c.EndLine,
                            originalText = c.OriginalText,
                            newText = c.NewText,
                            type = c.Type.ToString()
                        }),
                        affectedVariables = d.AffectedVariables
                    }),
                    summary = result.Summary == null ? null : new
                    {
                        operation = result.Summary.MethodName,
                        parameters = result.Summary.Parameters,
                        changesCount = result.Summary.TokensSaved,
                        affectedFiles = result.Deltas.Select(d => d.FilePath).Distinct().Count()
                    }
                });
            }
            else
            {
                logger.LogError("Multi-file rename failed: {Error}", result.ErrorMessage);
                return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during multi-file rename operation");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
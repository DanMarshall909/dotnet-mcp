using System.Text.Json;
using DotNetMcp.Core.Refactoring;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server;

public class ExtractMethodCompactTool(ILogger<ExtractMethodCompactTool> logger)
{
    private readonly ExtractMethodRefactorer _refactorer = new();

    public async Task<string> ExtractMethodCompact(string filePath, string selectedCode, string methodName)
    {
        try
        {
            logger.LogInformation("Starting compact extract method refactoring for {MethodName} in {FilePath}", methodName, filePath);
            
            if (!File.Exists(filePath))
            {
                logger.LogError("File not found: {FilePath}", filePath);
                return JsonSerializer.Serialize(new { success = false, error = $"File not found: {filePath}" });
            }

            var code = await File.ReadAllTextAsync(filePath);
            var result = await _refactorer.ExtractMethodCompactAsync(code, selectedCode, methodName, filePath);

            if (result.Success)
            {
                logger.LogInformation("Extract method refactoring completed successfully");
                
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
                        newMethodSignature = d.NewMethodSignature,
                        affectedVariables = d.AffectedVariables
                    }),
                    summary = result.Summary == null ? null : new
                    {
                        methodName = result.Summary.MethodName,
                        returnType = result.Summary.ReturnType,
                        parameters = result.Summary.Parameters,
                        tokensSaved = result.Summary.TokensSaved
                    }
                });
            }
            else
            {
                logger.LogError("Extract method refactoring failed: {Error}", result.ErrorMessage);
                return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during extract method refactoring");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
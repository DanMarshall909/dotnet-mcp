using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core;
using ModelContextProtocol.Core.Protocol;
using System.ComponentModel;
using System.Text.Json;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Server;

[McpServerTool]
public class ExtractMethodTool(ILogger<ExtractMethodTool> logger)
{
    [McpServerTool]
    [Description("Extract a block of code into a new method")]
    public async Task<string> ExtractMethod(
        [Description("The path to the C# file")] string filePath,
        [Description("The selected code to extract")] string selectedCode,
        [Description("The name for the new method")] string methodName)
    {
        logger.LogInformation("Extracting method '{MethodName}' from file '{FilePath}'", methodName, filePath);

        try
        {
            var extractor = new ExtractMethodRefactorer();
            var result = await extractor.ExtractMethodAsync(filePath, selectedCode, methodName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modifiedContent = result.ModifiedContent,
                extractedMethodSignature = result.ExtractedMethodSignature,
                parameters = result.Parameters,
                returnType = result.ReturnType,
                affectedFiles = new[] { filePath }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract method");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Server;

public class ExtractMethodTool(ILogger<ExtractMethodTool> logger)
{
    [Description("Extract a block of code into a new method")]
    public async Task<string> ExtractMethod(
        [Description("The path to the C# file")] string filePath,
        [Description("The selected code to extract")] string selectedCode,
        [Description("The name for the new method")] string methodName)
    {
        logger.LogInformation("Extracting method '{MethodName}' from file '{FilePath}'", methodName, filePath);

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath);
            var extractor = new ExtractMethodRefactorer();
            var result = await extractor.ExtractMethodAsync(sourceCode, selectedCode, methodName);

            // Write the modified content back to the file
            await File.WriteAllTextAsync(filePath, result.ModifiedCode);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modifiedContent = result.ModifiedCode,
                extractedMethodSignature = result.ExtractedMethod,
                parameters = result.UsedVariables,
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
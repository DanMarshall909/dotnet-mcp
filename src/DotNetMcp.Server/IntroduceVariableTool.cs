using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Server;

public class IntroduceVariableTool(ILogger<IntroduceVariableTool> logger)
{
    [Description("Introduce a variable for an expression or literal value")]
    public async Task<string> IntroduceVariable(
        [Description("The path to the C# file")] string filePath,
        [Description("The expression to extract into a variable")] string expression,
        [Description("The name for the new variable")] string variableName,
        [Description("The scope for the variable (local, field, property)")] string scope = "local",
        [Description("Whether to replace all occurrences of the expression")] bool replaceAll = true)
    {
        logger.LogInformation("Introducing variable '{VariableName}' for expression in file '{FilePath}'", 
            variableName, filePath);

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath);
            var introducer = new IntroduceVariableRefactorer();
            var result = await introducer.IntroduceVariableAsync(sourceCode, expression, variableName, scope, replaceAll);

            // Write the modified content back to the file
            await File.WriteAllTextAsync(filePath, result.ModifiedCode);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modifiedContent = result.ModifiedCode,
                variableDeclaration = result.VariableDeclaration,
                variableType = result.VariableType,
                scope = result.Scope,
                replacementCount = result.ReplacementCount,
                affectedFiles = new[] { filePath }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to introduce variable");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
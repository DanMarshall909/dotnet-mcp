using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Server;

public class RenameSymbolTool(ILogger<RenameSymbolTool> logger)
{
    [Description("Rename a symbol (variable, method, class, etc.) throughout the solution")]
    public async Task<string> RenameSymbol(
        [Description("The path to the solution or project file")] string solutionPath,
        [Description("The original symbol name")] string originalName,
        [Description("The new symbol name")] string newName,
        [Description("The symbol kind (variable, method, class, interface, property)")] string symbolKind = "auto")
    {
        logger.LogInformation("Renaming symbol '{OriginalName}' to '{NewName}' in solution '{SolutionPath}'", 
            originalName, newName, solutionPath);

        try
        {
            var sourceCode = await File.ReadAllTextAsync(solutionPath);
            var renamer = new SimpleRenameSymbolRefactorer();
            var result = await renamer.RenameSymbolAsync(sourceCode, originalName, newName, symbolKind);

            // Write the modified content back to the file
            await File.WriteAllTextAsync(solutionPath, result.ModifiedCode);

            return JsonSerializer.Serialize(new
            {
                success = true,
                affectedFiles = new[] { solutionPath },
                totalChanges = result.TotalChanges,
                symbolType = result.SymbolType,
                conflicts = result.Conflicts
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename symbol");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
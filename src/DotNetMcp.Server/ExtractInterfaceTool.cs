using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Server;

public class ExtractInterfaceTool(ILogger<ExtractInterfaceTool> logger)
{
    [Description("Extract an interface from an existing class")]
    public async Task<string> ExtractInterface(
        [Description("The path to the C# file containing the class")] string filePath,
        [Description("The name of the class to extract interface from")] string className,
        [Description("The name for the new interface")] string interfaceName,
        [Description("Comma-separated list of member names to include (optional - includes all public members if not specified)")] string? memberNames = null)
    {
        logger.LogInformation("Extracting interface '{InterfaceName}' from class '{ClassName}' in file '{FilePath}'", 
            interfaceName, className, filePath);

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath);
            var extractor = new SimpleExtractInterfaceRefactorer();
            var members = memberNames?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            var result = await extractor.ExtractInterfaceAsync(sourceCode, className, interfaceName, members);

            // Write the modified content back to the file
            await File.WriteAllTextAsync(filePath, result.ModifiedCode);

            return JsonSerializer.Serialize(new
            {
                success = true,
                interfaceContent = result.ExtractedInterface,
                modifiedClassContent = result.ModifiedCode,
                interfaceFilePath = Path.ChangeExtension(filePath, $".{interfaceName}.cs"),
                extractedMembers = result.ExtractedMembers,
                affectedFiles = new[] { filePath }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract interface");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core;
using ModelContextProtocol.Core.Protocol;
using System.ComponentModel;
using System.Text.Json;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Server;

[McpServerTool]
public class ExtractInterfaceTool(ILogger<ExtractInterfaceTool> logger)
{
    [McpServerTool]
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
            var extractor = new ExtractInterfaceRefactorer();
            var members = memberNames?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            var result = await extractor.ExtractInterfaceAsync(filePath, className, interfaceName, members);

            return JsonSerializer.Serialize(new
            {
                success = true,
                interfaceContent = result.InterfaceContent,
                modifiedClassContent = result.ModifiedClassContent,
                interfaceFilePath = result.InterfaceFilePath,
                extractedMembers = result.ExtractedMembers,
                affectedFiles = result.AffectedFiles
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
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server;

public class McpServer(IServiceProvider services)
{
    private readonly ILogger<McpServer> _logger = services.GetRequiredService<ILogger<McpServer>>();
    private readonly ExtractMethodTool _extractMethod = services.GetRequiredService<ExtractMethodTool>();
    private readonly RenameSymbolTool _renameSymbol = services.GetRequiredService<RenameSymbolTool>();
    private readonly ExtractInterfaceTool _extractInterface = services.GetRequiredService<ExtractInterfaceTool>();
    private readonly IntroduceVariableTool _introduceVariable = services.GetRequiredService<IntroduceVariableTool>();

    public async Task RunAsync()
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            try
            {
                var request = JsonNode.Parse(line);
                var response = await ProcessRequest(request);
                if (response != null)
                {
                    await writer.WriteLineAsync(response.ToJsonString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request: {Request}", line);
                var errorResponse = CreateErrorResponse(-1, "Internal error", ex.Message);
                await writer.WriteLineAsync(errorResponse.ToJsonString());
            }
        }
    }

    private async Task<JsonObject?> ProcessRequest(JsonNode? request)
    {
        if (request is not JsonObject obj) return null;

        var method = obj["method"]?.GetValue<string>();
        var id = obj["id"]?.GetValue<int>() ?? -1;
        var paramsObj = obj["params"] as JsonObject;

        return method switch
        {
            "initialize" => CreateSuccessResponse(id, new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject()
                },
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = "dotnet-mcp",
                    ["version"] = "1.0.0"
                }
            }),
            "tools/list" => CreateSuccessResponse(id, new JsonObject
            {
                ["tools"] = new JsonArray
                {
                    CreateTool("extract_method", "Extract selected code into a new method"),
                    CreateTool("rename_symbol", "Rename a symbol throughout the codebase"),
                    CreateTool("extract_interface", "Extract an interface from a class"),
                    CreateTool("introduce_variable", "Introduce a variable for an expression")
                }
            }),
            "tools/call" => await HandleToolCall(id, paramsObj),
            _ => CreateErrorResponse(id, "Method not found", $"Unknown method: {method}")
        };
    }

    private async Task<JsonObject> HandleToolCall(int id, JsonObject? paramsObj)
    {
        if (paramsObj == null)
            return CreateErrorResponse(id, "Invalid params", "Missing parameters");

        var toolName = paramsObj["name"]?.GetValue<string>();
        var arguments = paramsObj["arguments"] as JsonObject;

        if (arguments == null)
            return CreateErrorResponse(id, "Invalid arguments", "Missing tool arguments");

        try
        {
            var result = toolName switch
            {
                "extract_method" => await _extractMethod.ExtractMethod(
                    arguments["filePath"]?.GetValue<string>() ?? "",
                    arguments["selectedCode"]?.GetValue<string>() ?? "",
                    arguments["methodName"]?.GetValue<string>() ?? ""),
                "rename_symbol" => await _renameSymbol.RenameSymbol(
                    arguments["filePath"]?.GetValue<string>() ?? "",
                    arguments["oldName"]?.GetValue<string>() ?? "",
                    arguments["newName"]?.GetValue<string>() ?? "",
                    arguments["symbolType"]?.GetValue<string>() ?? "auto"),
                "extract_interface" => await _extractInterface.ExtractInterface(
                    arguments["filePath"]?.GetValue<string>() ?? "",
                    arguments["className"]?.GetValue<string>() ?? "",
                    arguments["interfaceName"]?.GetValue<string>() ?? ""),
                "introduce_variable" => await _introduceVariable.IntroduceVariable(
                    arguments["filePath"]?.GetValue<string>() ?? "",
                    arguments["expression"]?.GetValue<string>() ?? "",
                    arguments["variableName"]?.GetValue<string>() ?? "",
                    arguments["scope"]?.GetValue<string>() ?? "local",
                    arguments["replaceAll"]?.GetValue<bool>() ?? true),
                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };

            return CreateSuccessResponse(id, new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = result
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(id, "Tool execution failed", ex.Message);
        }
    }

    private static JsonObject CreateTool(string name, string description)
    {
        return name switch
        {
            "extract_method" => new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["filePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The path to the C# file to refactor"
                        },
                        ["selectedCode"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The code block to extract into a new method"
                        },
                        ["methodName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name for the extracted method"
                        }
                    },
                    ["required"] = new JsonArray { "filePath", "selectedCode", "methodName" }
                }
            },
            "rename_symbol" => new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["filePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The path to the C# file containing the symbol"
                        },
                        ["oldName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The current name of the symbol to rename"
                        },
                        ["newName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The new name for the symbol"
                        },
                        ["symbolType"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The type of symbol: 'class', 'method', 'variable', or 'auto'",
                            ["enum"] = new JsonArray { "class", "method", "variable", "auto" },
                            ["default"] = "auto"
                        }
                    },
                    ["required"] = new JsonArray { "filePath", "oldName", "newName" }
                }
            },
            "extract_interface" => new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["filePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The path to the C# file containing the class"
                        },
                        ["className"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name of the class to extract interface from"
                        },
                        ["interfaceName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name for the extracted interface"
                        }
                    },
                    ["required"] = new JsonArray { "filePath", "className", "interfaceName" }
                }
            },
            "introduce_variable" => new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["filePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The path to the C# file to modify"
                        },
                        ["expression"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The expression to extract into a variable"
                        },
                        ["variableName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name for the new variable"
                        },
                        ["scope"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The scope for the variable: 'local' or 'field'",
                            ["enum"] = new JsonArray { "local", "field" },
                            ["default"] = "local"
                        },
                        ["replaceAll"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to replace all occurrences of the expression",
                            ["default"] = true
                        }
                    },
                    ["required"] = new JsonArray { "filePath", "expression", "variableName" }
                }
            },
            _ => new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject(),
                    ["required"] = new JsonArray()
                }
            }
        };
    }

    private static JsonObject CreateSuccessResponse(int id, JsonObject result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
    }

    private static JsonObject CreateErrorResponse(int id, string error, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = -32603,
                ["message"] = error,
                ["data"] = message
            }
        };
    }
}
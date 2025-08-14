using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.Features.ExtractInterface;
using DotNetMcp.Core.Features.ExtractMethod;
using DotNetMcp.Core.Features.RenameSymbol;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.VSA;

/// <summary>
/// MCP Server implementation using Vertical Slice Architecture
/// </summary>
public class McpServerWithVSA
{
    private readonly ILogger<McpServerWithVSA> _logger;
    private readonly IMediator _mediator;

    public McpServerWithVSA(IServiceProvider services)
    {
        _logger = services.GetRequiredService<ILogger<McpServerWithVSA>>();
        _mediator = services.GetRequiredService<IMediator>();
    }

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
                    ["name"] = "dotnet-mcp-vsa",
                    ["version"] = "2.0.0"
                }
            }),
            "tools/list" => CreateSuccessResponse(id, new JsonObject
            {
                ["tools"] = new JsonArray
                {
                    CreateTool("extract_method", "Extract selected code into a new method using VSA"),
                    CreateTool("rename_symbol", "Rename a symbol throughout the codebase using VSA"),
                    CreateTool("extract_interface", "Extract an interface from a class using VSA"),
                    CreateTool("find_symbol", "Find symbols in the codebase with advanced filtering and token optimization"),
                    CreateTool("get_class_context", "Get comprehensive context for a class including dependencies, usages, and inheritance"),
                    CreateTool("analyze_project_structure", "Analyze project structure, architecture, and metrics"),
                    CreateTool("find_symbol_usages", "Find all usages of a symbol across the codebase with impact analysis")
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
                "extract_method" => await HandleExtractMethod(arguments),
                "rename_symbol" => await HandleRenameSymbol(arguments),
                "extract_interface" => await HandleExtractInterface(arguments),
                "find_symbol" => await HandleFindSymbol(arguments),
                "get_class_context" => await HandleGetClassContext(arguments),
                "analyze_project_structure" => await HandleAnalyzeProjectStructure(arguments),
                "find_symbol_usages" => await HandleFindSymbolUsages(arguments),
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
            _logger.LogError(ex, "Tool execution failed for {ToolName}", toolName);
            return CreateErrorResponse(id, "Tool execution failed", ex.Message);
        }
    }

    private async Task<string> HandleExtractMethod(JsonObject arguments)
    {
        var command = new ExtractMethodCommand
        {
            Code = arguments["code"]?.GetValue<string>() ?? "",
            SelectedCode = arguments["selectedCode"]?.GetValue<string>() ?? "",
            MethodName = arguments["methodName"]?.GetValue<string>() ?? "",
            FilePath = arguments["filePath"]?.GetValue<string>(),
            ReturnDelta = arguments["returnDelta"]?.GetValue<bool>() ?? false
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleRenameSymbol(JsonObject arguments)
    {
        var command = new RenameSymbolCommand
        {
            FilePath = arguments["filePath"]?.GetValue<string>() ?? "",
            OldName = arguments["oldName"]?.GetValue<string>() ?? "",
            NewName = arguments["newName"]?.GetValue<string>() ?? "",
            SymbolType = arguments["symbolType"]?.GetValue<string>() ?? "auto",
            MultiFile = arguments["multiFile"]?.GetValue<bool>() ?? false,
            SolutionPath = arguments["solutionPath"]?.GetValue<string>()
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleExtractInterface(JsonObject arguments)
    {
        var memberNames = arguments["memberNames"]?.AsArray()?.Select(x => x?.GetValue<string>())
            .Where(x => x != null).Cast<string>().ToArray();

        var command = new ExtractInterfaceCommand
        {
            Code = arguments["code"]?.GetValue<string>() ?? "",
            ClassName = arguments["className"]?.GetValue<string>() ?? "",
            InterfaceName = arguments["interfaceName"]?.GetValue<string>() ?? "",
            MemberNames = memberNames
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleFindSymbol(JsonObject arguments)
    {
        var symbolType = Enum.TryParse<SymbolType>(arguments["symbolType"]?.GetValue<string>() ?? "Any", out var type) 
            ? type 
            : SymbolType.Any;

        var command = new FindSymbolCommand
        {
            ProjectPath = arguments["projectPath"]?.GetValue<string>() ?? "",
            SymbolName = arguments["symbolName"]?.GetValue<string>() ?? "",
            SymbolType = symbolType,
            IncludeImplementations = arguments["includeImplementations"]?.GetValue<bool>() ?? false,
            OptimizeForTokens = arguments["optimizeForTokens"]?.GetValue<bool>() ?? false,
            MaxTokens = arguments["maxTokens"]?.GetValue<int>() ?? 2000,
            MaxResults = arguments["maxResults"]?.GetValue<int>() ?? 50
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleGetClassContext(JsonObject arguments)
    {
        var command = new GetClassContextCommand
        {
            ProjectPath = arguments["projectPath"]?.GetValue<string>() ?? "",
            ClassName = arguments["className"]?.GetValue<string>() ?? "",
            IncludeDependencies = arguments["includeDependencies"]?.GetValue<bool>() ?? true,
            IncludeUsages = arguments["includeUsages"]?.GetValue<bool>() ?? true,
            IncludeInheritance = arguments["includeInheritance"]?.GetValue<bool>() ?? true,
            IncludeTestContext = arguments["includeTestContext"]?.GetValue<bool>() ?? false,
            MaxDepth = arguments["maxDepth"]?.GetValue<int>() ?? 2,
            OptimizeForTokens = arguments["optimizeForTokens"]?.GetValue<bool>() ?? false,
            MaxTokens = arguments["maxTokens"]?.GetValue<int>() ?? 2000
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleAnalyzeProjectStructure(JsonObject arguments)
    {
        var command = new AnalyzeProjectStructureCommand
        {
            ProjectPath = arguments["projectPath"]?.GetValue<string>() ?? "",
            IncludeDependencies = arguments["includeDependencies"]?.GetValue<bool>() ?? true,
            IncludeMetrics = arguments["includeMetrics"]?.GetValue<bool>() ?? true,
            IncludeArchitecture = arguments["includeArchitecture"]?.GetValue<bool>() ?? true,
            IncludeTestStructure = arguments["includeTestStructure"]?.GetValue<bool>() ?? false,
            OptimizeForTokens = arguments["optimizeForTokens"]?.GetValue<bool>() ?? false,
            MaxTokens = arguments["maxTokens"]?.GetValue<int>() ?? 3000,
            MaxDepth = arguments["maxDepth"]?.GetValue<int>() ?? 3
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleFindSymbolUsages(JsonObject arguments)
    {
        var symbolType = Enum.TryParse<SymbolType>(arguments["symbolType"]?.GetValue<string>() ?? "Any", out var type) 
            ? type 
            : SymbolType.Any;

        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = arguments["projectPath"]?.GetValue<string>() ?? "",
            SymbolName = arguments["symbolName"]?.GetValue<string>() ?? "",
            SymbolType = symbolType,
            SymbolNamespace = arguments["symbolNamespace"]?.GetValue<string>(),
            IncludeDeclaration = arguments["includeDeclaration"]?.GetValue<bool>() ?? true,
            IncludeReferences = arguments["includeReferences"]?.GetValue<bool>() ?? true,
            IncludeImplementations = arguments["includeImplementations"]?.GetValue<bool>() ?? false,
            IncludeInheritance = arguments["includeInheritance"]?.GetValue<bool>() ?? false,
            GroupByFile = arguments["groupByFile"]?.GetValue<bool>() ?? true,
            OptimizeForTokens = arguments["optimizeForTokens"]?.GetValue<bool>() ?? false,
            MaxTokens = arguments["maxTokens"]?.GetValue<int>() ?? 2500,
            MaxResults = arguments["maxResults"]?.GetValue<int>() ?? 100
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private static JsonObject CreateTool(string name, string description)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["required"] = new JsonArray()
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
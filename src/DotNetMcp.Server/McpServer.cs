using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetMcp.Core.Common.Errors;
using DotNetMcp.Core.Features.AutoFix;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.Features.ExtractInterface;
using DotNetMcp.Core.Features.ExtractMethod;
using DotNetMcp.Core.Features.RenameSymbol;
using DotNetMcp.Core.Features.SolutionAnalysis;
using DotNetMcp.Core.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server;

/// <summary>
/// MCP Server implementation
/// </summary>
public class McpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    public McpServer(IServiceProvider services)
    {
        _logger = services.GetRequiredService<ILogger<McpServer>>();
        _mediator = services.GetRequiredService<IMediator>();
        _serviceProvider = services;
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
                    ["name"] = "dotnet-mcp",
                    ["version"] = "2.0.0"
                }
            }),
            "tools/list" => CreateSuccessResponse(id, new JsonObject
            {
                ["tools"] = new JsonArray
                {
                    CreateTool("extract_method", "Extract selected code into a new method"),
                    CreateTool("rename_symbol", "Rename a symbol throughout the codebase"),
                    CreateTool("extract_interface", "Extract an interface from a class"),
                    CreateTool("find_symbol", "Find symbols in the codebase with advanced filtering and token optimization"),
                    CreateTool("get_class_context", "Get comprehensive context for a class including dependencies, usages, and inheritance"),
                    CreateTool("analyze_project_structure", "Analyze project structure, architecture, and metrics"),
                    CreateTool("find_symbol_usages", "Find all usages of a symbol across the codebase with impact analysis"),
                    CreateTool("analyze_solution", "Analyze solution structure, dependencies, and detect architectural issues"),
                    CreateTool("auto_fix", "Apply automatic fixes to common code issues like missing usings, nullability warnings, async methods"),
                    CreateTool("batch_refactor", "Apply multiple refactoring operations in sequence with rollback support")
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
                "analyze_solution" => await HandleAnalyzeSolution(arguments),
                "auto_fix" => await HandleAutoFix(arguments),
                "batch_refactor" => await HandleBatchRefactor(arguments),
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
            
            // Check if we have a structured error available
            if (ex.Data.Contains("AnalysisError") && ex.Data["AnalysisError"] is AnalysisError analysisError)
            {
                var context = ex.Data["AnalysisContext"] as AnalysisContext;
                return CreateStructuredErrorResponse(id, analysisError, context);
            }
            
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

    private async Task<string> HandleAnalyzeSolution(JsonObject arguments)
    {
        var command = new AnalyzeSolutionCommand
        {
            SolutionPath = arguments["solutionPath"]?.GetValue<string>() ?? throw new ArgumentException("solutionPath is required"),
            IncludeDependencyGraph = arguments["includeDependencyGraph"]?.GetValue<bool>() ?? true,
            IncludeProjectDetails = arguments["includeProjectDetails"]?.GetValue<bool>() ?? true,
            ValidateBuilds = arguments["validateBuilds"]?.GetValue<bool>() ?? false,
            DetectIssues = arguments["detectIssues"]?.GetValue<bool>() ?? true
        };

        var result = await _mediator.Send(command, CancellationToken.None);

        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleAutoFix(JsonObject arguments)
    {
        var fixTypes = AutoFixTypes.None;
        var fixTypesString = arguments["fixTypes"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(fixTypesString))
        {
            var typeNames = fixTypesString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var typeName in typeNames)
            {
                if (Enum.TryParse<AutoFixTypes>(typeName.Trim(), true, out var type))
                {
                    fixTypes |= type;
                }
            }
        }
        else
        {
            fixTypes = AutoFixTypes.All;
        }

        var buildErrorsArray = arguments["buildErrors"]?.AsArray();
        var buildErrors = buildErrorsArray?.Select(x => x?.GetValue<string>())
            .Where(x => x != null).Cast<string>().ToArray() ?? Array.Empty<string>();

        var command = new AutoFixCommand
        {
            Code = arguments["code"]?.GetValue<string>() ?? "",
            FilePath = arguments["filePath"]?.GetValue<string>(),
            BuildErrors = buildErrors,
            FixTypes = fixTypes,
            ApplyFixes = arguments["applyFixes"]?.GetValue<bool>() ?? true,
            MaxFixes = arguments["maxFixes"]?.GetValue<int>() ?? 50
        };

        var result = await _mediator.Send(command);
        
        return result.Match(
            success => JsonSerializer.Serialize(success, new JsonSerializerOptions { WriteIndented = true }),
            (error, _) => $"Error: {error}");
    }

    private async Task<string> HandleBatchRefactor(JsonObject arguments)
    {
        // Parse the operations array
        var operationsArray = arguments["operations"]?.AsArray();
        if (operationsArray == null || !operationsArray.Any())
        {
            return "Error: No operations specified for batch refactoring";
        }

        var results = new List<object>();
        var rollbackActions = new List<Func<Task>>();
        var originalCode = arguments["code"]?.GetValue<string>() ?? "";
        var currentCode = originalCode;
        var allSuccessful = true;

        try
        {
            foreach (var operationNode in operationsArray)
            {
                if (operationNode is not JsonObject operation)
                    continue;

                var operationType = operation["type"]?.GetValue<string>();
                var operationArgs = operation["arguments"] as JsonObject ?? new JsonObject();

                // Add current code to operation arguments
                operationArgs["code"] = currentCode;

                try
                {
                    var result = operationType switch
                    {
                        "extract_method" => await HandleExtractMethod(operationArgs),
                        "rename_symbol" => await HandleRenameSymbol(operationArgs),
                        "extract_interface" => await HandleExtractInterface(operationArgs),
                        "auto_fix" => await HandleAutoFix(operationArgs),
                        _ => $"Error: Unknown operation type: {operationType}"
                    };

                    if (result.StartsWith("Error:"))
                    {
                        allSuccessful = false;
                        results.Add(new { 
                            operation = operationType, 
                            success = false, 
                            error = result 
                        });
                        break; // Stop processing on error
                    }
                    else
                    {
                        // Try to extract the modified code from the result
                        var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
                        if (resultObj.TryGetProperty("modifiedCode", out var modifiedCodeElement))
                        {
                            currentCode = modifiedCodeElement.GetString() ?? currentCode;
                        }
                        else if (resultObj.TryGetProperty("fixedCode", out var fixedCodeElement))
                        {
                            var fixedCode = fixedCodeElement.GetString();
                            if (!string.IsNullOrEmpty(fixedCode))
                            {
                                currentCode = fixedCode;
                            }
                        }

                        results.Add(new { 
                            operation = operationType, 
                            success = true, 
                            result = result 
                        });
                    }
                }
                catch (Exception ex)
                {
                    allSuccessful = false;
                    results.Add(new { 
                        operation = operationType, 
                        success = false, 
                        error = $"Exception: {ex.Message}" 
                    });
                    break;
                }
            }

            var batchResult = new
            {
                success = allSuccessful,
                originalCode = originalCode,
                finalCode = allSuccessful ? currentCode : originalCode,
                operationsExecuted = results.Count,
                totalOperations = operationsArray.Count,
                results = results.ToArray(),
                summary = allSuccessful 
                    ? $"Successfully executed {results.Count} operations" 
                    : $"Failed after {results.Count} operations - no changes applied"
            };

            return JsonSerializer.Serialize(batchResult, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Batch refactoring failed: {ex.Message}",
                originalCode = originalCode,
                finalCode = originalCode,
                operationsExecuted = 0,
                totalOperations = operationsArray.Count
            }, new JsonSerializerOptions { WriteIndented = true });
        }
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

    private static JsonObject CreateStructuredErrorResponse(int id, AnalysisError analysisError, AnalysisContext? context)
    {
        var errorData = new JsonObject
        {
            ["code"] = analysisError.Code,
            ["message"] = analysisError.Message,
            ["severity"] = analysisError.Severity.ToString(),
            ["suggestion"] = analysisError.Suggestion,
            ["alternatives"] = new JsonArray(analysisError.Alternatives.Select(a => JsonValue.Create(a)).ToArray()),
            ["canRetry"] = analysisError.CanRetry
        };

        // Add type-specific details
        if (analysisError is DuplicateFilesError duplicateError)
        {
            var duplicateFiles = new JsonArray();
            foreach (var duplicate in duplicateError.DuplicateFiles)
            {
                duplicateFiles.Add(new JsonObject
                {
                    ["fileName"] = duplicate.FileName,
                    ["locations"] = new JsonArray(duplicate.Locations.Select(l => JsonValue.Create(l)).ToArray()),
                    ["projects"] = new JsonArray(duplicate.Projects.Select(p => JsonValue.Create(p)).ToArray()),
                    ["identicalContent"] = duplicate.IdenticalContent,
                    ["suggestedResolution"] = duplicate.SuggestedResolution
                });
            }
            errorData["duplicateFiles"] = duplicateFiles;
            errorData["affectedFileCount"] = duplicateError.AffectedFileCount;
            errorData["resolutionStrategies"] = new JsonArray(duplicateError.ResolutionStrategies.Select(s => JsonValue.Create(s)).ToArray());
        }
        else if (analysisError is BuildValidationError buildError)
        {
            errorData["errorCount"] = buildError.ErrorCount;
            errorData["warningCount"] = buildError.WarningCount;
            errorData["errorSummary"] = buildError.ErrorSummary;
            errorData["failedProjects"] = new JsonArray(buildError.FailedProjects.Select(p => JsonValue.Create(p)).ToArray());
            
            var categories = new JsonArray();
            foreach (var category in buildError.CommonErrorTypes)
            {
                categories.Add(new JsonObject
                {
                    ["category"] = category.Category,
                    ["count"] = category.Count,
                    ["description"] = category.Description,
                    ["suggestedFix"] = category.SuggestedFix
                });
            }
            errorData["commonErrorTypes"] = categories;
        }
        else if (analysisError is ProjectDiscoveryError discoveryError)
        {
            errorData["attemptedPath"] = discoveryError.AttemptedPath;
            errorData["failureReason"] = discoveryError.FailureReason;
            errorData["discoveryType"] = discoveryError.DiscoveryType.ToString();
            errorData["foundFiles"] = new JsonArray(discoveryError.FoundFiles.Select(f => JsonValue.Create(f)).ToArray());
            errorData["suggestedPaths"] = new JsonArray(discoveryError.SuggestedPaths.Select(p => JsonValue.Create(p)).ToArray());
        }

        // Add context if available
        if (context != null)
        {
            errorData["context"] = new JsonObject
            {
                ["projectPath"] = context.ProjectPath,
                ["analysisType"] = context.AnalysisType,
                ["filesProcessed"] = context.FilesProcessed,
                ["failurePoint"] = context.FailurePoint,
                ["elapsedTime"] = context.ElapsedTime.TotalMilliseconds
            };
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = -32603,
                ["message"] = analysisError.Message,
                ["data"] = errorData
            }
        };
    }
}
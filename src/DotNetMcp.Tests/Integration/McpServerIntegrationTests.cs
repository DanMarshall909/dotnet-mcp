using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetMcp.Server;

namespace DotNetMcp.Tests.Integration;

public class McpServerIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly McpServer _mcpServer;

    public McpServerIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "TestClass.cs");

        // Set up DI container
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<McpServer>>(NullLogger<McpServer>.Instance);
        services.AddSingleton<ILogger<ExtractMethodTool>>(NullLogger<ExtractMethodTool>.Instance);
        services.AddSingleton<ILogger<ExtractMethodCompactTool>>(NullLogger<ExtractMethodCompactTool>.Instance);
        services.AddSingleton<ILogger<RenameSymbolTool>>(NullLogger<RenameSymbolTool>.Instance);
        services.AddSingleton<ILogger<RenameSymbolMultiFileTool>>(NullLogger<RenameSymbolMultiFileTool>.Instance);
        services.AddSingleton<ILogger<ExtractInterfaceTool>>(NullLogger<ExtractInterfaceTool>.Instance);
        services.AddSingleton<ILogger<IntroduceVariableTool>>(NullLogger<IntroduceVariableTool>.Instance);
        services.AddSingleton<ExtractMethodTool>();
        services.AddSingleton<ExtractMethodCompactTool>();
        services.AddSingleton<RenameSymbolTool>();
        services.AddSingleton<RenameSymbolMultiFileTool>();
        services.AddSingleton<ExtractInterfaceTool>();
        services.AddSingleton<IntroduceVariableTool>();

        var serviceProvider = services.BuildServiceProvider();
        _mcpServer = new McpServer(serviceProvider);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task McpServer_Initialize_ReturnsCorrectCapabilities()
    {
        // Arrange
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize",
            ["params"] = new JsonObject()
        };

        // Act
        var response = await ProcessMcpRequest(request);

        // Assert
        Assert.NotNull(response);
        var result = response["result"]?.AsObject();
        Assert.NotNull(result);
        Assert.Equal("2024-11-05", result["protocolVersion"]?.GetValue<string>());
        Assert.NotNull(result["capabilities"]);
        Assert.Equal("dotnet-mcp", result["serverInfo"]?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task McpServer_ToolsList_ReturnsAllRefactoringTools()
    {
        // Act
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/list"
        };

        var response = await ProcessMcpRequest(request);

        // Assert
        Assert.NotNull(response);
        var tools = response["result"]?["tools"]?.AsArray();
        Assert.NotNull(tools);
        Assert.Equal(6, tools.Count);

        var toolNames = tools.Select(t => t?["name"]?.GetValue<string>()).ToArray();
        Assert.Contains("extract_method", toolNames);
        Assert.Contains("extract_method_compact", toolNames);
        Assert.Contains("rename_symbol", toolNames);
        Assert.Contains("rename_symbol_multi_file", toolNames);
        Assert.Contains("extract_interface", toolNames);
        Assert.Contains("introduce_variable", toolNames);
    }

    [Fact]
    public async Task McpServer_ExtractMethodTool_ProcessesValidRequest()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Calculate(int a, int b)
        {
            int sum = a + b;
            int doubled = sum * 2;
            return doubled;
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = "extract_method",
                ["arguments"] = new JsonObject
                {
                    ["filePath"] = _testFilePath,
                    ["selectedCode"] = "int sum = a + b;",
                    ["methodName"] = "CalculateSum"
                }
            }
        };

        // Act
        var response = await ProcessMcpRequest(request);

        // Assert
        Assert.NotNull(response);
        var content = response["result"]?["content"]?[0]?["text"]?.GetValue<string>();
        Assert.NotNull(content);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("CalculateSum", jsonResult.GetProperty("modifiedContent").GetString()!);
    }

    [Fact]
    public async Task McpServer_InvalidMethod_ReturnsError()
    {
        // Arrange
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "invalid_method"
        };

        // Act
        var response = await ProcessMcpRequest(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response["error"]);
        Assert.Equal("Method not found", response["error"]?["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task McpServer_ToolCallWithMissingParameters_ReturnsError()
    {
        // Arrange
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = "extract_method"
                // Missing arguments
            }
        };

        // Act
        var response = await ProcessMcpRequest(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response["error"]);
        Assert.Equal("Invalid arguments", response["error"]?["message"]?.GetValue<string>());
    }

    private async Task<JsonObject> ProcessMcpRequest(JsonObject request)
    {
        // Use reflection to access the private ProcessRequest method
        var method = typeof(McpServer).GetMethod("ProcessRequest", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task<JsonObject?>)method!.Invoke(_mcpServer, new object[] { request })!;
        var result = await task;
        
        return result ?? throw new InvalidOperationException("ProcessRequest returned null");
    }
}
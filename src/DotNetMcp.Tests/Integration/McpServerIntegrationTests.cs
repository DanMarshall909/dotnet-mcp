using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetMcp.Server;
using DotNetMcp.Core.Extensions;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace DotNetMcp.Tests.Integration;

public class McpServerIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly McpServer _mcpServer;
    private readonly MockFileSystem _fileSystem;

    public McpServerIntegrationTests()
    {
        _testDirectory = "/test/project";
        _testFilePath = "/test/project/TestClass.cs";
        _fileSystem = new MockFileSystem();

        // Set up DI container with modern approach
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddCoreServices();

        var serviceProvider = services.BuildServiceProvider();
        _mcpServer = new McpServer(serviceProvider);
        
        // Set up test files
        SetupTestFiles();
    }
    
    private void SetupTestFiles()
    {
        _fileSystem.AddDirectory(_testDirectory);
        _fileSystem.AddFile(_testFilePath, new MockFileData(@"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello World"");
        }
    }
}
"));
        
        // Add a project file
        _fileSystem.AddFile("/test/project/TestProject.csproj", new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>"));
    }

    public void Dispose()
    {
        // MockFileSystem doesn't need disposal
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
        Assert.Equal(10, tools.Count);

        var toolNames = tools.Select(t => t?["name"]?.GetValue<string>()).ToArray();
        Assert.Contains("extract_method", toolNames);
        Assert.Contains("rename_symbol", toolNames);
        Assert.Contains("extract_interface", toolNames);
        Assert.Contains("find_symbol", toolNames);
        Assert.Contains("get_class_context", toolNames);
        Assert.Contains("analyze_project_structure", toolNames);
        Assert.Contains("find_symbol_usages", toolNames);
        Assert.Contains("analyze_solution", toolNames);
        Assert.Contains("auto_fix", toolNames);
        Assert.Contains("batch_refactor", toolNames);
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

        _fileSystem.AddFile(_testFilePath, new MockFileData(sourceCode));

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
                    ["code"] = sourceCode,
                    ["selectedCode"] = "int sum = a + b;",
                    ["methodName"] = "CalculateSum",
                    ["filePath"] = _testFilePath
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
        Assert.Contains("CalculateSum", jsonResult.GetProperty("ModifiedCode").GetString()!);
        Assert.Contains("CalculateSum", jsonResult.GetProperty("ExtractedMethod").GetString()!);
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
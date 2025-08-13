using DotNetMcp.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace DotNetMcp.Tests.Integration;

public class McpToolsIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly ILogger<ExtractMethodTool> _extractMethodLogger;
    private readonly ILogger<RenameSymbolTool> _renameSymbolLogger;
    private readonly ILogger<ExtractInterfaceTool> _extractInterfaceLogger;
    private readonly ILogger<IntroduceVariableTool> _introduceVariableLogger;

    public McpToolsIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "TestClass.cs");

        _extractMethodLogger = NullLogger<ExtractMethodTool>.Instance;
        _renameSymbolLogger = NullLogger<RenameSymbolTool>.Instance;
        _extractInterfaceLogger = NullLogger<ExtractInterfaceTool>.Instance;
        _introduceVariableLogger = NullLogger<IntroduceVariableTool>.Instance;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExtractMethodTool_EndToEnd_ReturnsSuccessfulJson()
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

        var selectedCode = @"int sum = a + b;
            int doubled = sum * 2;";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractMethodTool(_extractMethodLogger);

        // Act
        var result = await tool.ExtractMethod(_testFilePath, selectedCode, "CalculateSum");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("modifiedContent").GetString()!.Contains("CalculateSum"));
        Assert.True(jsonResult.GetProperty("extractedMethodSignature").GetString()!.Contains("CalculateSum"));
        Assert.True(jsonResult.GetProperty("affectedFiles").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ExtractMethodTool_InvalidCode_ReturnsErrorJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            int x = 5;
        }
    }
}";

        var selectedCode = "nonexistent code";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractMethodTool(_extractMethodLogger);

        // Act
        var result = await tool.ExtractMethod(_testFilePath, selectedCode, "NewMethod");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.False(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("error").GetString()!.Length > 0);
    }

    [Fact]
    public async Task RenameSymbolTool_EndToEnd_ReturnsSuccessfulJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class OldClassName
    {
        public void TestMethod()
        {
            var instance = new OldClassName();
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        
        // Create a minimal project file for the test
        var projectPath = Path.Combine(_testDirectory, "TestProject.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, projectContent);

        var tool = new RenameSymbolTool(_renameSymbolLogger);

        // Act
        var result = await tool.RenameSymbol(projectPath, "OldClassName", "NewClassName", "class");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("totalChanges").GetInt32() > 0);
        Assert.True(jsonResult.GetProperty("affectedFiles").GetArrayLength() > 0);
        Assert.Equal("class", jsonResult.GetProperty("symbolType").GetString());
    }

    [Fact]
    public async Task ExtractInterfaceTool_EndToEnd_ReturnsSuccessfulJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class UserService
    {
        public void CreateUser(string name)
        {
            Console.WriteLine($""Creating user: {name}"");
        }

        public string GetUser(int id)
        {
            return $""User {id}"";
        }

        private void LogOperation(string operation)
        {
            Console.WriteLine($""Operation: {operation}"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractInterfaceTool(_extractInterfaceLogger);

        // Act
        var result = await tool.ExtractInterface(_testFilePath, "UserService", "IUserService", null);

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("interfaceContent").GetString()!.Contains("IUserService"));
        Assert.True(jsonResult.GetProperty("modifiedClassContent").GetString()!.Contains("IUserService"));
        Assert.True(jsonResult.GetProperty("extractedMembers").GetArrayLength() > 0);
        Assert.True(jsonResult.GetProperty("affectedFiles").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ExtractInterfaceTool_SpecificMembers_ReturnsSuccessfulJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class DataProcessor
    {
        public void ProcessData(string data)
        {
            Console.WriteLine($""Processing: {data}"");
        }

        public string FormatData(string data)
        {
            return data.ToUpper();
        }

        public void SaveData(string data)
        {
            Console.WriteLine($""Saving: {data}"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractInterfaceTool(_extractInterfaceLogger);

        // Act
        var result = await tool.ExtractInterface(_testFilePath, "DataProcessor", "IDataProcessor", "ProcessData,FormatData");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("interfaceContent").GetString()!.Contains("ProcessData"));
        Assert.True(jsonResult.GetProperty("interfaceContent").GetString()!.Contains("FormatData"));
        Assert.False(jsonResult.GetProperty("interfaceContent").GetString()!.Contains("SaveData"));
        Assert.Equal(2, jsonResult.GetProperty("extractedMembers").GetArrayLength());
    }

    [Fact]
    public async Task IntroduceVariableTool_EndToEnd_ReturnsSuccessfulJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class StringProcessor
    {
        public void ProcessMessage()
        {
            Console.WriteLine(""Hello, World!"");
            var message = ""Hello, World!"";
            Console.WriteLine(""Hello, World!"");
        }
    }
}";

        var expression = @"""Hello, World!""";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new IntroduceVariableTool(_introduceVariableLogger);

        // Act
        var result = await tool.IntroduceVariable(_testFilePath, expression, "greeting", "local", true);

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("modifiedContent").GetString()!.Contains("greeting"));
        Assert.True(jsonResult.GetProperty("variableDeclaration").GetString()!.Contains("greeting"));
        Assert.Equal("string", jsonResult.GetProperty("variableType").GetString());
        Assert.Equal("local", jsonResult.GetProperty("scope").GetString());
        Assert.True(jsonResult.GetProperty("replacementCount").GetInt32() > 0);
    }

    [Fact]
    public async Task IntroduceVariableTool_FieldScope_ReturnsSuccessfulJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class ConfigurationManager
    {
        public void LoadConfiguration()
        {
            var timeout = 30000;
            Console.WriteLine($""Timeout: {30000}ms"");
        }
    }
}";

        var expression = "30000";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new IntroduceVariableTool(_introduceVariableLogger);

        // Act
        var result = await tool.IntroduceVariable(_testFilePath, expression, "defaultTimeout", "field", true);

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("variableDeclaration").GetString()!.Contains("private int defaultTimeout"));
        Assert.Equal("int", jsonResult.GetProperty("variableType").GetString());
        Assert.Equal("field", jsonResult.GetProperty("scope").GetString());
        Assert.True(jsonResult.GetProperty("replacementCount").GetInt32() > 0);
    }

    [Fact]
    public async Task AllTools_ErrorHandling_ReturnsErrorJson()
    {
        // Arrange
        var nonExistentFilePath = Path.Combine(_testDirectory, "NonExistent.cs");
        
        var extractMethodTool = new ExtractMethodTool(_extractMethodLogger);
        var renameSymbolTool = new RenameSymbolTool(_renameSymbolLogger);
        var extractInterfaceTool = new ExtractInterfaceTool(_extractInterfaceLogger);
        var introduceVariableTool = new IntroduceVariableTool(_introduceVariableLogger);

        // Act & Assert - ExtractMethodTool
        var extractResult = await extractMethodTool.ExtractMethod(nonExistentFilePath, "code", "method");
        var extractJson = JsonSerializer.Deserialize<JsonElement>(extractResult);
        Assert.False(extractJson.GetProperty("success").GetBoolean());

        // Act & Assert - RenameSymbolTool
        var renameResult = await renameSymbolTool.RenameSymbol(nonExistentFilePath, "old", "new", "auto");
        var renameJson = JsonSerializer.Deserialize<JsonElement>(renameResult);
        Assert.False(renameJson.GetProperty("success").GetBoolean());

        // Act & Assert - ExtractInterfaceTool
        var interfaceResult = await extractInterfaceTool.ExtractInterface(nonExistentFilePath, "Class", "IClass", null);
        var interfaceJson = JsonSerializer.Deserialize<JsonElement>(interfaceResult);
        Assert.False(interfaceJson.GetProperty("success").GetBoolean());

        // Act & Assert - IntroduceVariableTool
        var variableResult = await introduceVariableTool.IntroduceVariable(nonExistentFilePath, "expr", "var", "local", true);
        var variableJson = JsonSerializer.Deserialize<JsonElement>(variableResult);
        Assert.False(variableJson.GetProperty("success").GetBoolean());
    }
}
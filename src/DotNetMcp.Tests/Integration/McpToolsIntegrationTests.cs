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

        var selectedCode = "int sum = a + b;";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractMethodTool(_extractMethodLogger);

        // Act
        var result = await tool.ExtractMethod(_testFilePath, selectedCode, "CalculateSum");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("CalculateSum", jsonResult.GetProperty("modifiedContent").GetString()!);
        Assert.Contains("CalculateSum", jsonResult.GetProperty("extractedMethodSignature").GetString()!);
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
        Assert.True(jsonResult.TryGetProperty("error", out _));
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
        var tool = new RenameSymbolTool(_renameSymbolLogger);

        // Act
        var result = await tool.RenameSymbol(_testFilePath, "OldClassName", "NewClassName", "class");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.GetProperty("totalChanges").GetInt32() > 0);
        Assert.Equal("class", jsonResult.GetProperty("symbolType").GetString());
        Assert.True(jsonResult.GetProperty("affectedFiles").GetArrayLength() > 0);
    }

    [Fact]
    public async Task RenameSymbolTool_NonExistentSymbol_ReturnsNoChanges()
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
            Console.WriteLine(""Hello"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new RenameSymbolTool(_renameSymbolLogger);

        // Act
        var result = await tool.RenameSymbol(_testFilePath, "NonExistentSymbol", "NewName", "auto");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.Equal(0, jsonResult.GetProperty("totalChanges").GetInt32());
    }

    [Fact]
    public async Task ExtractInterfaceTool_EndToEnd_ReturnsSuccessfulJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public int Subtract(int a, int b)
        {
            return a - b;
        }

        private int MultiplyInternal(int a, int b)
        {
            return a * b;
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractInterfaceTool(_extractInterfaceLogger);

        // Act
        var result = await tool.ExtractInterface(_testFilePath, "Calculator", "ICalculator");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("ICalculator", jsonResult.GetProperty("interfaceContent").GetString()!);
        Assert.Contains("ICalculator", jsonResult.GetProperty("modifiedClassContent").GetString()!);
        Assert.True(jsonResult.GetProperty("extractedMembers").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ExtractInterfaceTool_NonExistentClass_ReturnsErrorJson()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class ExistingClass
    {
        public void DoSomething() { }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new ExtractInterfaceTool(_extractInterfaceLogger);

        // Act
        var result = await tool.ExtractInterface(_testFilePath, "NonExistentClass", "IInterface");

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.False(jsonResult.GetProperty("success").GetBoolean());
        Assert.True(jsonResult.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task IntroduceVariableTool_EndToEnd_ReturnsSuccessfulJson()
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
            Console.WriteLine(""Hello World"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new IntroduceVariableTool(_introduceVariableLogger);

        // Act
        var result = await tool.IntroduceVariable(_testFilePath, "\"Hello World\"", "message", "local", true);

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("message", jsonResult.GetProperty("modifiedContent").GetString()!);
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
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello World"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var tool = new IntroduceVariableTool(_introduceVariableLogger);

        // Act
        var result = await tool.IntroduceVariable(_testFilePath, "\"Hello World\"", "greeting", "field", true);

        // Assert
        Assert.NotNull(result);
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(jsonResult.GetProperty("success").GetBoolean());
        Assert.Contains("greeting", jsonResult.GetProperty("modifiedContent").GetString()!);
        Assert.Equal("field", jsonResult.GetProperty("scope").GetString());
        Assert.Contains("private", jsonResult.GetProperty("modifiedContent").GetString()!);
    }
}
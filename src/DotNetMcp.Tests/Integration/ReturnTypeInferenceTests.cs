using DotNetMcp.Core.Refactoring;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetMcp.Tests.Integration;

public class ReturnTypeInferenceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ExtractMethodRefactorer _refactorer;

    public ReturnTypeInferenceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _refactorer = new ExtractMethodRefactorer();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Theory]
    [InlineData("return 42;", "int")]
    [InlineData("return \"hello\";", "string")]
    [InlineData("return true;", "bool")]
    [InlineData("return 3.14;", "double")]
    [InlineData("return 2.5f;", "float")]
    public async Task DetermineReturnType_WithReturnStatements_InfersCorrectType(string returnStatement, string expectedType)
    {
        // Arrange
        var code = $@"
using System;

namespace TestNamespace
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            {returnStatement}
        }}
    }}
}}";

        // Act
        var result = await _refactorer.ExtractMethodAsync(code, returnStatement, "ExtractedMethod");

        // Debug output
        Console.WriteLine($"=== Debug for {expectedType} ===");
        Console.WriteLine($"Return statement: {returnStatement}");
        Console.WriteLine($"Expected type: {expectedType}");
        Console.WriteLine($"Actual return type: {result.ReturnType}");
        Console.WriteLine($"Extracted method: {result.ExtractedMethod}");

        // Assert
        Assert.Contains($"{expectedType} ExtractedMethod()", result.ExtractedMethod);
        Assert.Equal(expectedType, result.ReturnType);
    }

    [Fact]
    public async Task DetermineReturnType_WithMultipleSameTypeReturns_InfersCorrectType()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(bool condition)
        {
            if (condition)
                return 10;
            else
                return 20;
        }
    }
}";

        var selectedCode = @"if (condition)
                return 10;
            else
                return 20;";

        // Act
        var result = await _refactorer.ExtractMethodAsync(code, selectedCode, "GetNumber");

        // Assert
        Assert.Contains("int GetNumber(", result.ExtractedMethod);
        Assert.Equal("int", result.ReturnType);
    }

    [Fact]
    public async Task DetermineReturnType_WithNoReturnStatement_InfersVoid()
    {
        // Arrange
        var code = @"
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

        // Act
        var result = await _refactorer.ExtractMethodAsync(code, @"Console.WriteLine(""Hello World"");", "PrintMessage");

        // Assert
        Assert.Contains("void PrintMessage(", result.ExtractedMethod);
        Assert.Equal("void", result.ReturnType);
    }

    [Fact]
    public async Task DetermineReturnType_WithExpressionStatement_InfersFromExpression()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public int TestMethod()
        {
            var result = 5 + 10;
            return result;
        }
    }
}";

        // Act  
        var result = await _refactorer.ExtractMethodAsync(code, "var result = 5 + 10;", "Calculate");

        // Assert - for now this should still be void since we're not converting assignments to returns yet
        Assert.Contains("void Calculate(", result.ExtractedMethod);
        Assert.Equal("void", result.ReturnType);
    }

    [Fact]
    public async Task ExtractMethodWithReturnType_GeneratesValidCode()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";

        // Act
        var result = await _refactorer.ExtractMethodAsync(code, "return a + b;", "AddNumbers");

        // Assert
        Assert.True(result.ModifiedCode.Length > 0, "ModifiedCode should not be empty");
        Assert.Contains("int AddNumbers(", result.ExtractedMethod);
        Assert.Equal("int", result.ReturnType);
        
        // Verify the extracted method has proper parameters
        Assert.Contains("int a", result.ExtractedMethod);
        Assert.Contains("int b", result.ExtractedMethod);
        
        // Verify the method call was inserted in the modified code
        Assert.Contains("AddNumbers(a, b)", result.ModifiedCode);
        
        // Verify the original return statement was replaced
        Assert.DoesNotContain("return a + b;", result.ModifiedCode);
    }
}
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class ExtractMethodRefactorerTests
{
    [Fact]
    public async Task ExtractMethodAsync_SimpleCalculation_ExtractsMethodSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Calculate()
        {
            int a = 5;
            int b = 10;
            int result = a + b;
            return result;
        }
    }
}";

        var selectedCode = "int result = a + b;";
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(sourceCode, selectedCode, "AddNumbers");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("AddNumbers", result.ModifiedCode);
        Assert.Contains("AddNumbers", result.ExtractedMethod);
    }

    [Fact]
    public async Task ExtractMethodAsync_WithLocalVariables_HandlesVariablesCorrectly()
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
            string message = ""Hello"";
            string name = ""World"";
            Console.WriteLine(message + "" "" + name);
        }
    }
}";

        var selectedCode = "Console.WriteLine(message + \" \" + name);";
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(sourceCode, selectedCode, "PrintMessage");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("PrintMessage", result.ModifiedCode);
        Assert.Contains("message", result.UsedVariables);
        Assert.Contains("name", result.UsedVariables);
    }

    [Fact]
    public async Task ExtractMethodAsync_SingleStatement_ExtractsCorrectly()
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

        var selectedCode = "Console.WriteLine(\"Hello World\");";
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(sourceCode, selectedCode, "SayHello");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SayHello", result.ModifiedCode);
        Assert.Contains("SayHello", result.ExtractedMethod);
    }

    [Fact]
    public async Task ExtractMethodAsync_ComplexCodeBlock_PreservesLogic()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class DataProcessor
    {
        public void ProcessData()
        {
            int[] numbers = { 1, 2, 3, 4, 5 };
            int sum = 0;
            for (int i = 0; i < numbers.Length; i++)
            {
                sum += numbers[i];
            }
            Console.WriteLine($""Sum: {sum}"");
        }
    }
}";

        var selectedCode = @"for (int i = 0; i < numbers.Length; i++)
            {
                sum += numbers[i];
            }";
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(sourceCode, selectedCode, "CalculateSum");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("CalculateSum", result.ModifiedCode);
        Assert.Contains("numbers", result.UsedVariables);
        Assert.Contains("sum", result.UsedVariables);
    }

    [Fact]
    public async Task ExtractMethodAsync_CodeNotInClass_ThrowsException()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    Console.WriteLine(""This is not in a class"");
}";

        var selectedCode = "Console.WriteLine(\"This is not in a class\");";
        var refactorer = new ExtractMethodRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => refactorer.ExtractMethodAsync(sourceCode, selectedCode, "ExtractedMethod"));
    }
}
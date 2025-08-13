using DotNetMcp.Core.Refactoring;
using System.Text;

namespace DotNetMcp.Tests.Integration;

public class ExtractMethodRefactorerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;

    public ExtractMethodRefactorerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "TestClass.cs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

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
        public int Calculate(int a, int b)
        {
            int result = a + b;
            result = result * 2;
            return result;
        }
    }
}";

        var selectedCode = @"int result = a + b;
            result = result * 2;";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(_testFilePath, selectedCode, "CalculateSum");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("CalculateSum", result.ModifiedContent);
        Assert.Contains("private object CalculateSum", result.ModifiedContent);
        Assert.Contains("CalculateSum(a, b)", result.ModifiedContent);
        Assert.Equal("object", result.ReturnType);
        Assert.Contains("object a", result.Parameters);
        Assert.Contains("object b", result.Parameters);
    }

    [Fact]
    public async Task ExtractMethodAsync_WithLocalVariables_HandlesVariablesCorrectly()
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
            var data = GetData();
            string processed = data.ToUpper();
            int length = processed.Length;
            Console.WriteLine($""Processed: {processed}, Length: {length}"");
        }

        private string GetData() => ""test data"";
    }
}";

        var selectedCode = @"string processed = data.ToUpper();
            int length = processed.Length;";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(_testFilePath, selectedCode, "ProcessString");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ProcessString", result.ModifiedContent);
        Assert.Contains("private object ProcessString", result.ModifiedContent);
        Assert.Contains("ProcessString(data)", result.ModifiedContent);
    }

    [Fact]
    public async Task ExtractMethodAsync_InvalidCode_ThrowsException()
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
        var refactorer = new ExtractMethodRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => refactorer.ExtractMethodAsync(_testFilePath, selectedCode, "NewMethod"));
    }

    [Fact]
    public async Task ExtractMethodAsync_SingleStatement_ExtractsCorrectly()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Logger
    {
        public void LogMessage(string message)
        {
            Console.WriteLine($""[{DateTime.Now}] {message}"");
        }
    }
}";

        var selectedCode = @"Console.WriteLine($""[{DateTime.Now}] {message}"");";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(_testFilePath, selectedCode, "WriteLogEntry");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("WriteLogEntry", result.ModifiedContent);
        Assert.Contains("private object WriteLogEntry", result.ModifiedContent);
        Assert.Contains("WriteLogEntry(message)", result.ModifiedContent);
    }

    [Fact]
    public async Task ExtractMethodAsync_CodeNotInClass_ThrowsException()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    // This is outside a class
    int x = 5;
}";

        var selectedCode = "int x = 5;";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractMethodRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => refactorer.ExtractMethodAsync(_testFilePath, selectedCode, "NewMethod"));
    }

    [Fact]
    public async Task ExtractMethodAsync_ComplexCodeBlock_PreservesLogic()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class ListProcessor
    {
        public void ProcessItems(List<int> items)
        {
            foreach (var item in items)
            {
                if (item > 10)
                {
                    Console.WriteLine($""Large item: {item}"");
                }
                else
                {
                    Console.WriteLine($""Small item: {item}"");
                }
            }
        }
    }
}";

        var selectedCode = @"foreach (var item in items)
            {
                if (item > 10)
                {
                    Console.WriteLine($""Large item: {item}"");
                }
                else
                {
                    Console.WriteLine($""Small item: {item}"");
                }
            }";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractMethodRefactorer();

        // Act
        var result = await refactorer.ExtractMethodAsync(_testFilePath, selectedCode, "ProcessEachItem");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ProcessEachItem", result.ModifiedContent);
        Assert.Contains("private object ProcessEachItem", result.ModifiedContent);
        Assert.Contains("ProcessEachItem(items)", result.ModifiedContent);
        Assert.Contains("foreach", result.ModifiedContent);
        Assert.Contains("if (item > 10)", result.ModifiedContent);
    }
}
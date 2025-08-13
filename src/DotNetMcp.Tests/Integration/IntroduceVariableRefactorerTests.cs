using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class IntroduceVariableRefactorerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;

    public IntroduceVariableRefactorerTests()
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
    public async Task IntroduceVariableAsync_StringLiteral_IntroducesLocalVariable()
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
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "greeting", "local", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("string greeting =", result.VariableDeclaration);
        Assert.Equal("string", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.True(result.ReplacementCount > 0);
        Assert.Contains("greeting", result.ModifiedContent);
        Assert.Contains("Console.WriteLine(greeting)", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_NumericLiteral_IntroducesWithCorrectType()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public void Calculate()
        {
            int result = 42 + 10;
            Console.WriteLine(42);
            return 42;
        }
    }
}";

        var expression = "42";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "magicNumber", "local", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("int magicNumber =", result.VariableDeclaration);
        Assert.Equal("int", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.True(result.ReplacementCount > 0);
        Assert.Contains("magicNumber", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_BooleanLiteral_IntroducesWithBoolType()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Validator
    {
        public void ValidateInput()
        {
            if (true)
            {
                Console.WriteLine(""Valid"");
            }
            bool isValid = true;
        }
    }
}";

        var expression = "true";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "isEnabled", "local", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("bool isEnabled =", result.VariableDeclaration);
        Assert.Equal("bool", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.Contains("isEnabled", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_MethodCall_IntroducesWithVarType()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class DataService
    {
        public void ProcessData()
        {
            var result = GetData().ToString();
            Console.WriteLine(GetData().ToString());
        }

        private object GetData()
        {
            return ""some data"";
        }
    }
}";

        var expression = "GetData().ToString()";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "processedData", "local", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("var processedData =", result.VariableDeclaration);
        Assert.Equal("var", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.Contains("processedData", result.ModifiedContent);
        Assert.Contains("Console.WriteLine(processedData)", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_ObjectCreation_IntroducesWithCorrectType()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class CollectionManager
    {
        public void ManageCollections()
        {
            var items = new List<string>();
            items.AddRange(new List<string>());
        }
    }
}";

        var expression = "new List<string>()";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "emptyList", "local", false);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("List<string> emptyList =", result.VariableDeclaration);
        Assert.Equal("List<string>", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.Equal(1, result.ReplacementCount); // Only replace first occurrence since replaceAll = false
        Assert.Contains("emptyList", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_FieldScope_IntroducesAsField()
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
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "defaultTimeout", "field", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("private int defaultTimeout =", result.VariableDeclaration);
        Assert.Equal("int", result.VariableType);
        Assert.Equal("field", result.Scope);
        Assert.Contains("defaultTimeout", result.ModifiedContent);
        Assert.Contains("private int defaultTimeout", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_PropertyScope_IntroducesAsProperty()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class UserManager
    {
        public void CreateUser()
        {
            var maxUsers = 100;
            if (GetUserCount() > 100)
            {
                throw new InvalidOperationException(""Too many users"");
            }
        }

        private int GetUserCount() => 50;
    }
}";

        var expression = "100";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "MaxUserCount", "property", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("public int MaxUserCount { get; set; } =", result.VariableDeclaration);
        Assert.Equal("int", result.VariableType);
        Assert.Equal("property", result.Scope);
        Assert.Contains("MaxUserCount", result.ModifiedContent);
        Assert.Contains("public int MaxUserCount { get; set; }", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_ExpressionNotFound_ThrowsException()
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

        var expression = "nonexistent expression";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => refactorer.IntroduceVariableAsync(_testFilePath, expression, "newVariable", "local", true));
    }

    [Fact]
    public async Task IntroduceVariableAsync_InvalidExpression_ThrowsException()
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

        var expression = "invalid syntax +++";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => refactorer.IntroduceVariableAsync(_testFilePath, expression, "newVariable", "local", true));
    }

    [Fact]
    public async Task IntroduceVariableAsync_ComplexExpression_IntroducesCorrectly()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.Linq;

namespace TestNamespace
{
    public class DataAnalyzer
    {
        public void AnalyzeData()
        {
            var numbers = new[] { 1, 2, 3, 4, 5 };
            var result = numbers.Where(x => x > 2).Sum();
            Console.WriteLine(numbers.Where(x => x > 2).Sum());
        }
    }
}";

        var expression = "numbers.Where(x => x > 2).Sum()";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "filteredSum", "local", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("var filteredSum =", result.VariableDeclaration);
        Assert.Equal("var", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.True(result.ReplacementCount > 0);
        Assert.Contains("filteredSum", result.ModifiedContent);
        Assert.Contains("Console.WriteLine(filteredSum)", result.ModifiedContent);
    }

    [Fact]
    public async Task IntroduceVariableAsync_DoubleExpression_IntroducesWithDoubleType()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class MathCalculator
    {
        public void Calculate()
        {
            double result = 3.14159 * 2;
            Console.WriteLine($""Circle circumference: {3.14159 * 2}"");
        }
    }
}";

        var expression = "3.14159";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(_testFilePath, expression, "pi", "local", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("double pi =", result.VariableDeclaration);
        Assert.Equal("double", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.True(result.ReplacementCount > 0);
        Assert.Contains("pi", result.ModifiedContent);
    }
}
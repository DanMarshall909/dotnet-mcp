using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class IntroduceVariableRefactorerTests
{
    [Fact]
    public async Task IntroduceVariableAsync_StringLiteral_IntroducesLocalVariable()
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

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "\"Hello World\"", "message", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("message", result.ModifiedCode);
        Assert.Contains("string", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.Equal(1, result.ReplacementCount);
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
        public int Calculate()
        {
            return 42 + 8;
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "42", "answer", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("answer", result.ModifiedCode);
        Assert.Equal("int", result.VariableType);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariableAsync_BooleanLiteral_IntroducesWithBoolType()
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
            if (true)
            {
                Console.WriteLine(""Always true"");
            }
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "true", "isAlwaysTrue", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("isAlwaysTrue", result.ModifiedCode);
        Assert.Equal("bool", result.VariableType);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariableAsync_DoubleExpression_IntroducesWithDoubleType()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class MathClass
    {
        public void Calculate()
        {
            var result = 3.14159 * 2;
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "3.14159", "pi", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("pi", result.ModifiedCode);
        Assert.Equal("double", result.VariableType);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariableAsync_ObjectCreation_IntroducesWithCorrectType()
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
            var text = new StringBuilder().ToString();
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "new StringBuilder()", "builder", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("builder", result.ModifiedCode);
        Assert.Contains("StringBuilder", result.VariableType);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariableAsync_MethodCall_IntroducesWithVarType()
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
            Console.WriteLine(DateTime.Now.ToString());
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "DateTime.Now.ToString()", "currentTime", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("currentTime", result.ModifiedCode);
        Assert.Equal("var", result.VariableType);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariableAsync_ComplexExpression_IntroducesCorrectly()
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
            int a = 5;
            int b = 10;
            int result = (a + b) * 2;
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "(a + b) * 2", "calculation", "local");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("calculation", result.ModifiedCode);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariableAsync_FieldScope_IntroducesAsField()
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

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "\"Hello World\"", "greeting", "field");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("greeting", result.ModifiedCode);
        Assert.Equal("field", result.Scope);
        Assert.Contains("private", result.ModifiedCode);
    }

    [Fact]
    public async Task IntroduceVariableAsync_PropertyScope_IntroducesAsProperty()
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
            var value = 42;
        }
    }
}";

        var refactorer = new IntroduceVariableRefactorer();

        // Act
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "42", "DefaultValue", "property");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("DefaultValue", result.ModifiedCode);
        Assert.Equal("property", result.Scope);
        Assert.Contains("public", result.ModifiedCode);
        Assert.Contains("{ get; set; }", result.ModifiedCode);
    }
}
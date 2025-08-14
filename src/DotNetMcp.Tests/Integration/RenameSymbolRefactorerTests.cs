using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class RenameSymbolRefactorerTests
{
    [Fact]
    public async Task RenameSymbolAsync_RenameClass_RenamesSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class OldClassName
    {
        public void DoSomething()
        {
            var instance = new OldClassName();
        }
    }

    public class OtherClass
    {
        public void UseOldClass()
        {
            var obj = new OldClassName();
        }
    }
}";

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "OldClassName", "NewClassName", "class");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("class", result.SymbolType);
        Assert.Contains("NewClassName", result.ModifiedCode);
        Assert.DoesNotContain("OldClassName", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbolAsync_RenameMethod_RenamesSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void OldMethodName()
        {
            Console.WriteLine(""Hello World"");
        }

        public void CallOldMethod()
        {
            OldMethodName();
        }
    }
}";

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "OldMethodName", "NewMethodName", "method");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("method", result.SymbolType);
        Assert.Contains("NewMethodName", result.ModifiedCode);
        Assert.DoesNotContain("OldMethodName", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbolAsync_RenameVariable_RenamesSuccessfully()
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
            var oldVariableName = ""Hello World"";
            Console.WriteLine(oldVariableName);
            Console.WriteLine(oldVariableName.Length);
        }
    }
}";

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "oldVariableName", "newVariableName", "variable");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("local variable", result.SymbolType);
        Assert.Contains("newVariableName", result.ModifiedCode);
        Assert.DoesNotContain("oldVariableName", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbolAsync_InterfaceRename_RenamesSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public interface IOldInterface
    {
        void DoSomething();
    }

    public class TestClass : IOldInterface
    {
        public void DoSomething()
        {
            Console.WriteLine(""Implementation"");
        }
    }
}";

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "IOldInterface", "INewInterface", "interface");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("interface", result.SymbolType);
        Assert.Contains("INewInterface", result.ModifiedCode);
        Assert.DoesNotContain("IOldInterface", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbolAsync_FieldRename_RenamesSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        private string oldFieldName = ""Hello"";

        public void TestMethod()
        {
            Console.WriteLine(oldFieldName);
            oldFieldName = ""World"";
        }
    }
}";

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "oldFieldName", "newFieldName", "variable");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("field", result.SymbolType);
        Assert.Contains("newFieldName", result.ModifiedCode);
        Assert.DoesNotContain("oldFieldName", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbolAsync_AutoDetectSymbolType_RenamesSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestTarget
    {
        public void TestMethod()
        {
            var obj = new TestTarget();
            obj.TestMethod();
        }
    }
}";

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "TestTarget", "RenamedTarget", "auto");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("class", result.SymbolType);
        Assert.Contains("RenamedTarget", result.ModifiedCode);
        Assert.DoesNotContain("TestTarget", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbolAsync_NonExistentSymbol_ReturnsNoChanges()
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

        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(sourceCode, "NonExistentSymbol", "NewName", "auto");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalChanges);
        Assert.Equal("unknown", result.SymbolType);
        Assert.Equal(sourceCode, result.ModifiedCode);
    }
}
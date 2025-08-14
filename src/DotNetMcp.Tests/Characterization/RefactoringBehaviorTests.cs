using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Characterization;

/// <summary>
/// Characterization tests that document the exact behavior of refactoring tools.
/// These tests capture the current behavior to prevent regressions during refactoring.
/// </summary>
public class RefactoringBehaviorTests
{
    #region ExtractMethod Behavior Documentation

    [Fact]
    public async Task ExtractMethod_DocumentsBehavior_SimpleCalculation()
    {
        // Documents: How extract method handles simple arithmetic with variables
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

        var refactorer = new ExtractMethodRefactorer();
        var result = await refactorer.ExtractMethodAsync(sourceCode, "int result = a + b;", "AddNumbers");

        // Documents expected behavior
        Assert.Contains("AddNumbers", result.ModifiedCode);
        Assert.Contains("private void AddNumbers()", result.ExtractedMethod);
        Assert.Contains("a", result.UsedVariables);
        Assert.Contains("b", result.UsedVariables);
        Assert.Equal("void", result.ReturnType);
        Assert.Contains("AddNumbers();", result.ModifiedCode);
    }

    [Fact]
    public async Task ExtractMethod_DocumentsBehavior_InvalidCodeThrowsException()
    {
        // Documents: Tool validates that selected code exists in source
        var sourceCode = @"
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

        var refactorer = new ExtractMethodRefactorer();
        
        // Documents expected exception behavior
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => refactorer.ExtractMethodAsync(sourceCode, "nonexistent code", "NewMethod"));
        
        Assert.Contains("Selected code 'nonexistent code' not found in source", exception.Message);
    }

    #endregion

    #region RenameSymbol Behavior Documentation

    [Fact]
    public async Task RenameSymbol_DocumentsBehavior_ClassRenaming()
    {
        // Documents: How class renaming handles all occurrences including constructor calls
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

        var refactorer = new RenameSymbolRefactorer();
        var result = await refactorer.RenameSymbolAsync(sourceCode, "OldClassName", "NewClassName", "class");

        // Documents expected behavior for class renaming
        Assert.True(result.TotalChanges >= 2); // Class declaration + constructor call minimum
        Assert.Equal("class", result.SymbolType);
        Assert.Contains("NewClassName", result.ModifiedCode);
        Assert.DoesNotContain("OldClassName", result.ModifiedCode);
        Assert.Empty(result.Conflicts);
        Assert.Contains("new NewClassName()", result.ModifiedCode);
    }

    [Fact]
    public async Task RenameSymbol_DocumentsBehavior_NonExistentSymbol()
    {
        // Documents: How tool handles non-existent symbols
        var sourceCode = @"
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

        var refactorer = new RenameSymbolRefactorer();
        var result = await refactorer.RenameSymbolAsync(sourceCode, "NonExistentSymbol", "NewName", "auto");

        // Documents expected behavior for non-existent symbols
        Assert.Equal(0, result.TotalChanges);
        Assert.Equal("unknown", result.SymbolType);
        Assert.Equal(sourceCode, result.ModifiedCode);
        Assert.Empty(result.Conflicts);
    }

    #endregion

    #region ExtractInterface Behavior Documentation

    [Fact]
    public async Task ExtractInterface_DocumentsBehavior_AllPublicMembers()
    {
        // Documents: How interface extraction handles all public members by default
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

        private int MultiplyInternal(int a, int b)
        {
            return a * b;
        }

        public string Name { get; set; }
    }
}";

        var refactorer = new ExtractInterfaceRefactorer();
        var result = await refactorer.ExtractInterfaceAsync(sourceCode, "Calculator", "ICalculator");

        // Documents expected behavior for interface extraction
        Assert.Equal("ICalculator", result.InterfaceName);
        Assert.Contains("ICalculator", result.ModifiedCode);
        Assert.Contains("Add", result.ExtractedMembers);
        Assert.Contains("Name", result.ExtractedMembers);
        Assert.DoesNotContain("MultiplyInternal", result.ExtractedMembers);
        // Documents that class implements the new interface (may have different syntax)
        Assert.Contains("ICalculator", result.ModifiedCode);
        Assert.Contains("ICalculator", result.ExtractedInterface);
    }

    #endregion

    #region IntroduceVariable Behavior Documentation

    [Fact]
    public async Task IntroduceVariable_DocumentsBehavior_StringLiteral()
    {
        // Documents: How string literals are handled with local scope
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
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "\"Hello World\"", "message", "local");

        // Documents expected behavior for string literal introduction
        Assert.Contains("message", result.ModifiedCode);
        Assert.Equal("string", result.VariableType);
        Assert.Equal("local", result.Scope);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Contains("message", result.VariableDeclaration);
        Assert.Contains("Hello World", result.VariableDeclaration);
        Assert.Contains("Console.WriteLine(message);", result.ModifiedCode);
    }

    [Fact]
    public async Task IntroduceVariable_DocumentsBehavior_MethodCallUsesVar()
    {
        // Documents: Method calls should use 'var' type instead of resolved type
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
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "DateTime.Now.ToString()", "currentTime", "local");

        // Documents expected behavior for method calls
        Assert.Contains("currentTime", result.ModifiedCode);
        Assert.Equal("var", result.VariableType); // Method calls should use 'var'
        Assert.Equal("local", result.Scope);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public async Task IntroduceVariable_DocumentsBehavior_PropertyScope()
    {
        // Documents: How property scope creates auto-properties
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
        var result = await refactorer.IntroduceVariableAsync(sourceCode, "42", "DefaultValue", "property");

        // Documents expected behavior for property scope
        Assert.Contains("DefaultValue", result.ModifiedCode);
        Assert.Equal("int", result.VariableType);
        Assert.Equal("property", result.Scope);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Contains("public int DefaultValue { get; set; }", result.ModifiedCode);
    }

    #endregion

    #region Error Handling Documentation

    [Fact]
    public async Task ExtractInterface_DocumentsBehavior_NonExistentClass()
    {
        // Documents: Error handling for non-existent classes
        var sourceCode = @"
namespace TestNamespace
{
    public class ExistingClass
    {
        public void DoSomething() { }
    }
}";

        var refactorer = new ExtractInterfaceRefactorer();
        
        // Documents expected exception behavior
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => refactorer.ExtractInterfaceAsync(sourceCode, "NonExistentClass", "IInterface"));
        
        Assert.Contains("Class 'NonExistentClass' not found", exception.Message);
    }

    #endregion
}
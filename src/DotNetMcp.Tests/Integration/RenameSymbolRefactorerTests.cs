using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class RenameSymbolRefactorerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _solutionPath;
    private readonly string _projectPath;
    private readonly string _testFilePath;

    public RenameSymbolRefactorerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        _solutionPath = Path.Combine(_testDirectory, "TestSolution.sln");
        _projectPath = Path.Combine(_testDirectory, "TestProject.csproj");
        _testFilePath = Path.Combine(_testDirectory, "TestClass.cs");
        
        SetupTestProject();
    }

    private void SetupTestProject()
    {
        // Create a minimal solution file
        var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 16
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""TestProject"", ""TestProject.csproj"", ""{12345678-1234-1234-1234-123456789012}""
EndProject
";
        File.WriteAllText(_solutionPath, solutionContent);

        // Create a minimal project file
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(_projectPath, projectContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

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

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "OldClassName", "NewClassName", "class");

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.True(result.TotalChanges > 0);
        Assert.Equal("class", result.SymbolType);

        // Verify the file was actually modified
        var modifiedContent = await File.ReadAllTextAsync(_testFilePath);
        Assert.Contains("NewClassName", modifiedContent);
        Assert.DoesNotContain("OldClassName", modifiedContent);
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
            Console.WriteLine(""Hello"");
        }

        public void CallMethod()
        {
            OldMethodName();
            this.OldMethodName();
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "OldMethodName", "NewMethodName", "method");

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.True(result.TotalChanges > 0);

        // Verify the file was actually modified
        var modifiedContent = await File.ReadAllTextAsync(_testFilePath);
        Assert.Contains("NewMethodName", modifiedContent);
        Assert.DoesNotContain("OldMethodName", modifiedContent);
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
            int oldVariableName = 42;
            Console.WriteLine(oldVariableName);
            int result = oldVariableName + 10;
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "oldVariableName", "newVariableName", "variable");

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.True(result.TotalChanges > 0);

        // Verify the file was actually modified
        var modifiedContent = await File.ReadAllTextAsync(_testFilePath);
        Assert.Contains("newVariableName", modifiedContent);
        Assert.DoesNotContain("oldVariableName", modifiedContent);
    }

    [Fact]
    public async Task RenameSymbolAsync_AutoDetectSymbolType_RenamesSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public string OldProperty { get; set; }

        public void UseProperty()
        {
            OldProperty = ""test"";
            Console.WriteLine(OldProperty);
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "OldProperty", "NewProperty", "auto");

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.True(result.TotalChanges > 0);

        // Verify the file was actually modified
        var modifiedContent = await File.ReadAllTextAsync(_testFilePath);
        Assert.Contains("NewProperty", modifiedContent);
        Assert.DoesNotContain("OldProperty", modifiedContent);
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
            Console.WriteLine(""Hello"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "NonExistentSymbol", "NewName", "auto");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalChanges);
        Assert.Empty(result.AffectedFiles);
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

    public class Implementation : IOldInterface
    {
        public void DoSomething()
        {
            // Implementation
        }
    }

    public class Client
    {
        public void UseInterface(IOldInterface service)
        {
            service.DoSomething();
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "IOldInterface", "INewInterface", "interface");

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.True(result.TotalChanges > 0);

        // Verify the file was actually modified
        var modifiedContent = await File.ReadAllTextAsync(_testFilePath);
        Assert.Contains("INewInterface", modifiedContent);
        Assert.DoesNotContain("IOldInterface", modifiedContent);
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
        private string oldFieldName;

        public TestClass()
        {
            oldFieldName = ""initial value"";
        }

        public void UseField()
        {
            Console.WriteLine(oldFieldName);
            oldFieldName = ""new value"";
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolAsync(_projectPath, "oldFieldName", "newFieldName", "variable");

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.True(result.TotalChanges > 0);

        // Verify the file was actually modified
        var modifiedContent = await File.ReadAllTextAsync(_testFilePath);
        Assert.Contains("newFieldName", modifiedContent);
        Assert.DoesNotContain("oldFieldName", modifiedContent);
    }
}
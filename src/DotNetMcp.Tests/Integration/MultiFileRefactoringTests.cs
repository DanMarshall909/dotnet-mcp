using DotNetMcp.Core.Services;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class MultiFileRefactoringTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _projectPath;

    public MultiFileRefactoringTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _projectPath = Path.Combine(_testDirectory, "TestProject.csproj");
        
        SetupTestProject();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    private void SetupTestProject()
    {
        // Create a simple .csproj file
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";

        File.WriteAllText(_projectPath, projectContent);

        // Create test files
        var classAContent = @"
namespace TestNamespace
{
    public class ClassA
    {
        public void DoSomething()
        {
            var helper = new HelperClass();
            helper.ProcessData();
        }
    }
}";

        var classBContent = @"
namespace TestNamespace
{
    public class ClassB
    {
        public void AnotherMethod()
        {
            var helper = new HelperClass();
            helper.ProcessData();
        }
    }
}";

        var helperClassContent = @"
namespace TestNamespace
{
    public class HelperClass
    {
        public void ProcessData()
        {
            // Processing logic here
        }
    }
}";

        File.WriteAllText(Path.Combine(_testDirectory, "ClassA.cs"), classAContent);
        File.WriteAllText(Path.Combine(_testDirectory, "ClassB.cs"), classBContent);
        File.WriteAllText(Path.Combine(_testDirectory, "HelperClass.cs"), helperClassContent);
    }

    [Fact]
    public async Task MultiFileEngine_LoadsFilesSuccessfully()
    {
        // Arrange
        using var engine = new MultiFileRefactoringEngine();
        var files = new[]
        {
            Path.Combine(_testDirectory, "ClassA.cs"),
            Path.Combine(_testDirectory, "ClassB.cs"),
            Path.Combine(_testDirectory, "HelperClass.cs")
        };

        // Act
        var loaded = await engine.LoadFilesAsync(files);

        // Assert
        Assert.True(loaded);
        
        // Verify we can get semantic models
        var semanticModel = engine.GetSemanticModel(files[0]);
        Assert.NotNull(semanticModel);
    }

    [Fact]
    public async Task RenameSymbolAcrossFiles_FindsAllReferences()
    {
        // Arrange
        using var engine = new MultiFileRefactoringEngine();
        var files = new[]
        {
            Path.Combine(_testDirectory, "ClassA.cs"),
            Path.Combine(_testDirectory, "ClassB.cs"),
            Path.Combine(_testDirectory, "HelperClass.cs")
        };

        await engine.LoadFilesAsync(files);
        var refactorer = new RenameSymbolRefactorer();

        // Act - Rename HelperClass to DataProcessor
        var result = await refactorer.RenameSymbolMultiFileAsync(
            engine, 
            "HelperClass", 
            "DataProcessor", 
            files[2]); // Start search from HelperClass.cs

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Deltas);
        
        // Should find references in all three files
        var affectedFiles = result.Deltas.Select(d => d.FilePath).Distinct().ToList();
        Console.WriteLine($"Affected files: {string.Join(", ", affectedFiles.Select(Path.GetFileName))}");
        
        // Verify changes
        var totalChanges = result.Deltas.Sum(d => d.Changes.Count);
        Console.WriteLine($"Total changes: {totalChanges}");
        
        Assert.True(totalChanges >= 3); // At least class declaration + 2 usages
        
        // Verify summary
        Assert.NotNull(result.Summary);
        Assert.Contains("HelperClass", result.Summary.MethodName);
        Assert.Contains("DataProcessor", result.Summary.MethodName);
    }

    [Fact]
    public async Task RenameSymbolAcrossFiles_HandlesMethodNames()
    {
        // Arrange
        using var engine = new MultiFileRefactoringEngine();
        var files = new[]
        {
            Path.Combine(_testDirectory, "ClassA.cs"),
            Path.Combine(_testDirectory, "ClassB.cs"),
            Path.Combine(_testDirectory, "HelperClass.cs")
        };

        await engine.LoadFilesAsync(files);
        var refactorer = new RenameSymbolRefactorer();

        // Act - Rename ProcessData to HandleData
        var result = await refactorer.RenameSymbolMultiFileAsync(
            engine, 
            "ProcessData", 
            "HandleData", 
            files[2]); // Start search from HelperClass.cs

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Deltas);
        
        var totalChanges = result.Deltas.Sum(d => d.Changes.Count);
        Console.WriteLine($"Method rename - Total changes: {totalChanges}");
        
        // Should find method declaration + method calls
        Assert.True(totalChanges >= 3); // Method declaration + 2 calls
        
        foreach (var delta in result.Deltas)
        {
            Console.WriteLine($"File: {Path.GetFileName(delta.FilePath)}");
            foreach (var change in delta.Changes)
            {
                Console.WriteLine($"  Line {change.StartLine}: '{change.OriginalText.Trim()}' -> '{change.NewText.Trim()}'");
            }
        }
    }

    [Fact]
    public async Task MultiFileEngine_LoadsProjectSuccessfully()
    {
        // Arrange
        using var engine = new MultiFileRefactoringEngine();

        // Act
        var loaded = await engine.LoadSolutionAsync(_projectPath);

        // Assert
        Assert.True(loaded);
    }

    [Fact]
    public async Task RenameSymbol_NonExistentSymbol_ReturnsFailure()
    {
        // Arrange
        using var engine = new MultiFileRefactoringEngine();
        var files = new[]
        {
            Path.Combine(_testDirectory, "ClassA.cs"),
            Path.Combine(_testDirectory, "ClassB.cs"),
            Path.Combine(_testDirectory, "HelperClass.cs")
        };

        await engine.LoadFilesAsync(files);
        var refactorer = new RenameSymbolRefactorer();

        // Act
        var result = await refactorer.RenameSymbolMultiFileAsync(
            engine, 
            "NonExistentSymbol", 
            "NewName");

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.Deltas);
        Assert.Contains("not found", result.ErrorMessage);
    }
}
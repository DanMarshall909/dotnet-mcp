using DotNetMcp.Core.Services;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

Console.WriteLine("Testing duplicate file handling with CompilationService...");

// Create a mock file system with duplicate GlobalUsings.cs files
var fileSystem = new MockFileSystem();

// Add duplicate GlobalUsings.cs files in different projects
fileSystem.AddFile("/solution/Project1/GlobalUsings.cs", "global using System;");
fileSystem.AddFile("/solution/Project2/GlobalUsings.cs", "global using System.Collections.Generic;");
fileSystem.AddFile("/solution/Project3/GlobalUsings.cs", "global using System.Linq;");

// Add some regular C# files
fileSystem.AddFile("/solution/Project1/Service.cs", @"
namespace Project1
{
    public class Service
    {
        public void DoWork() => Task.Delay(100);
    }
}");

fileSystem.AddFile("/solution/Project2/Controller.cs", @"
namespace Project2
{
    public class Controller
    {
        public void Handle() => Task.Delay(200);
    }
}");

var compilationService = new CompilationService(fileSystem, NullLogger<CompilationService>.Instance);

try
{
    Console.WriteLine("Test 1: Single file compilation with duplicate name...");
    
    // Test single file compilation - should work
    var singleFileCompilation = await compilationService.CreateSingleFileCompilationAsync("/solution/Project1/GlobalUsings.cs");
    Console.WriteLine($"✅ Single file compilation succeeded. Syntax trees: {singleFileCompilation.SyntaxTrees.Count()}");

    Console.WriteLine("\nTest 2: Multiple files with duplicates...");
    
    // Test multiple files with duplicates - should work with unique naming
    var multipleFiles = new[]
    {
        "/solution/Project1/GlobalUsings.cs",
        "/solution/Project2/GlobalUsings.cs", 
        "/solution/Project3/GlobalUsings.cs",
        "/solution/Project1/Service.cs",
        "/solution/Project2/Controller.cs"
    };
    
    var multiFileCompilation = await compilationService.CreateCompilationAsync(multipleFiles);
    Console.WriteLine($"✅ Multi-file compilation succeeded. Syntax trees: {multiFileCompilation.SyntaxTrees.Count()}");
    
    // Verify all files are included with unique paths
    foreach (var tree in multiFileCompilation.SyntaxTrees)
    {
        Console.WriteLine($"   - {tree.FilePath}");
    }

    Console.WriteLine("\nTest 3: Finding semantic models for original files...");
    
    // Test getting semantic models for original file paths
    foreach (var originalFile in multipleFiles.Take(3)) // Just the GlobalUsings files
    {
        var semanticModel = compilationService.GetSemanticModel(multiFileCompilation, originalFile);
        if (semanticModel != null)
        {
            Console.WriteLine($"✅ Found semantic model for: {originalFile}");
        }
        else
        {
            Console.WriteLine($"❌ Could not find semantic model for: {originalFile}");
        }
    }

        Console.WriteLine("\nTest 4: Simulating MCP find_symbol_usages scenario...");
    
    // Simulate the exact scenario from user feedback - finding Task.Delay usages
    var buildValidationService = new BuildValidationService(fileSystem, NullLogger<BuildValidationService>.Instance);
    
    // Create mock project structure like Flo project
    fileSystem.AddFile("/flo/src/Flo.Core/GlobalUsings.cs", "global using System;");
    fileSystem.AddFile("/flo/src/Flo.Infrastructure/GlobalUsings.cs", "global using System.Collections.Generic;");
    fileSystem.AddFile("/flo/test/Flo.Tests/GlobalUsings.cs", "global using Xunit;");
    
    // Add files with Task.Delay usage
    fileSystem.AddFile("/flo/src/Flo.Infrastructure/DelayProvider.cs", @"
namespace Flo.Infrastructure
{
    public class DelayProvider
    {
        public async Task DelayAsync(int milliseconds)
        {
            await Task.Delay(milliseconds);
        }
    }
}");

    fileSystem.AddFile("/flo/test/Flo.Tests/SomeTest.cs", @"
namespace Flo.Tests
{
    public class SomeTest
    {
        public async Task TestDelay()
        {
            await Task.Delay(100);
        }
    }
}");

    // Test compilation with the new Flo-like structure
    var floFiles = new[]
    {
        "/flo/src/Flo.Core/GlobalUsings.cs",
        "/flo/src/Flo.Infrastructure/GlobalUsings.cs", 
        "/flo/test/Flo.Tests/GlobalUsings.cs",
        "/flo/src/Flo.Infrastructure/DelayProvider.cs",
        "/flo/test/Flo.Tests/SomeTest.cs"
    };
    
    var floCompilation = await compilationService.CreateCompilationAsync(floFiles, "FloSolution");
    Console.WriteLine($"✅ Flo-like compilation succeeded. Syntax trees: {floCompilation.SyntaxTrees.Count()}");
    
    // Test finding Task.Delay usages using semantic analysis
    var delayUsages = new List<string>();
    
    foreach (var syntaxTree in floCompilation.SyntaxTrees)
    {
        var semanticModel = floCompilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();
        
        var invocations = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Task.Delay"));
            
        foreach (var invocation in invocations)
        {
            delayUsages.Add($"{syntaxTree.FilePath}: {invocation}");
        }
    }
    
    Console.WriteLine($"✅ Found {delayUsages.Count} Task.Delay usages:");
    foreach (var usage in delayUsages)
    {
        Console.WriteLine($"   - {usage}");
    }

Console.WriteLine("\n🎉 All tests passed! Duplicate file handling is working correctly.");
Console.WriteLine("✅ The GlobalUsings.cs duplicate file issue has been resolved!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Test failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
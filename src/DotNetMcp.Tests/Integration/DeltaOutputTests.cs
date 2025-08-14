using DotNetMcp.Core.Refactoring;
using DotNetMcp.Core.Services;

namespace DotNetMcp.Tests.Integration;

public class DeltaOutputTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ExtractMethodRefactorer _refactorer;

    public DeltaOutputTests()
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

    [Fact]
    public async Task ExtractMethodCompactAsync_GeneratesDeltaOutput()
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

        var filePath = Path.Combine(_testDirectory, "Calculator.cs");
        await File.WriteAllTextAsync(filePath, code);

        // Act
        var result = await _refactorer.ExtractMethodCompactAsync(code, "return a + b;", "Sum", filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Deltas);
        
        var delta = result.Deltas.First();
        Assert.Equal(filePath, delta.FilePath);
        Assert.NotEmpty(delta.Changes);
        Assert.NotNull(delta.NewMethodSignature);
        
        // Should have a summary with token savings
        Assert.NotNull(result.Summary);
        Assert.Equal("Sum", result.Summary.MethodName);
        Assert.True(result.Summary.TokensSaved > 0);
    }

    [Fact]
    public async Task DeltaGenerator_CalculatesTokenSavings()
    {
        // Arrange
        var originalCode = @"
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

        var modifiedCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            Sum(a, b);
        }

        private int Sum(int a, int b)
        {
            return a + b;
        }
    }
}";

        // Act
        var delta = DeltaGenerator.GenerateDelta("test.cs", originalCode, modifiedCode);
        var tokenSavings = DeltaGenerator.EstimateTokenSavings(delta, originalCode);

        // Assert
        Assert.NotEmpty(delta.Changes);
        Assert.True(tokenSavings >= 0); // Should not be negative
        
        Console.WriteLine($"Generated {delta.Changes.Count} changes");
        Console.WriteLine($"Estimated token savings: {tokenSavings}");
        
        foreach (var change in delta.Changes)
        {
            Console.WriteLine($"Change {change.Type}: Lines {change.StartLine}-{change.EndLine}");
            Console.WriteLine($"  Original: {change.OriginalText.Replace("\n", "\\n")}");
            Console.WriteLine($"  New: {change.NewText.Replace("\n", "\\n")}");
        }
    }

    [Fact]
    public async Task CompactOutput_IsSignificantlySmallerThanFullOutput()
    {
        // Arrange
        var largeCode = GenerateLargeCodeFile();
        var filePath = Path.Combine(_testDirectory, "LargeFile.cs");
        await File.WriteAllTextAsync(filePath, largeCode);

        // Act
        var fullResult = await _refactorer.ExtractMethodAsync(largeCode, "return x + y;", "AddNumbers");
        var compactResult = await _refactorer.ExtractMethodCompactAsync(largeCode, "return x + y;", "AddNumbers", filePath);

        // Assert
        var fullOutputSize = fullResult.ModifiedCode.Length;
        var compactOutputSize = compactResult.Deltas.Sum(d => d.Changes.Sum(c => c.NewText.Length));
        
        Console.WriteLine($"Full output size: {fullOutputSize} characters");
        Console.WriteLine($"Compact output size: {compactOutputSize} characters");
        Console.WriteLine($"Reduction: {((double)(fullOutputSize - compactOutputSize) / fullOutputSize * 100):F1}%");
        
        // Compact output should be significantly smaller
        Assert.True(compactOutputSize < fullOutputSize / 2, 
            $"Compact output ({compactOutputSize}) should be less than half of full output ({fullOutputSize})");
    }

    private static string GenerateLargeCodeFile()
    {
        var code = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace LargeNamespace
{
    public class LargeClass
    {
        private readonly List<string> _items = new();
        
        public void Method1()
        {
            Console.WriteLine(""Method 1"");
            // Lots of code here...
        }
        
        public void Method2()
        {
            Console.WriteLine(""Method 2"");
            // Lots of code here...
        }
        
        public void Method3()
        {
            Console.WriteLine(""Method 3"");
            // Lots of code here...
        }
        
        public int Calculate(int x, int y)
        {
            // Some setup code
            var temp1 = x * 2;
            var temp2 = y * 3;
            
            // The line we want to extract
            return x + y;
        }
        
        public void Method4()
        {
            Console.WriteLine(""Method 4"");
            // Lots more code here...
        }
        
        public void Method5()
        {
            Console.WriteLine(""Method 5"");
            // Even more code here...
        }
    }
}";
        return code;
    }
}
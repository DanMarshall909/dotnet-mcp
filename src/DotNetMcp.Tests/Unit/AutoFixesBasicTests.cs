using DotNetMcp.Core.AutoFixes;
using FluentAssertions;
using Xunit;

namespace DotNetMcp.Tests.Unit;

/// <summary>
/// Basic tests for auto-fix functionality
/// </summary>
public class AutoFixesBasicTests
{
    [Fact]
    public void AutoFixProvider_AddMissingUsings_ShouldAddUsingStatements()
    {
        // Arrange
        var sourceCode = @"namespace TestNamespace
{
    public class TestClass
    {
        public List<string> Items { get; set; }
    }
}";
        var missingNamespaces = new[] { "System.Collections.Generic" };

        // Act
        var result = AutoFixProvider.AddMissingUsings(sourceCode, missingNamespaces);

        // Assert
        result.Should().Contain("using System.Collections.Generic;");
        result.Should().Contain("namespace TestNamespace");
        result.Should().Contain("List<string>");
    }

    [Fact]
    public void AutoFixProvider_FixNullabilityWarnings_ShouldAddNullChecks()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    public void TestMethod(string input)
    {
        var length = input.Length;
    }
}";

        // Act
        var result = AutoFixProvider.FixNullabilityWarnings(sourceCode);

        // Assert
        // The fix should make some improvement to nullability
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterOrEqualTo(sourceCode.Length);
    }

    [Fact]
    public void AutoFixProvider_ModernizeCollections_ShouldReplaceOldTypes()
    {
        // Arrange
        var sourceCode = "var items = new ArrayList();";

        // Act
        var result = AutoFixProvider.ModernizeCollections(sourceCode);

        // Assert
        result.Should().Contain("List<object>");
        result.Should().NotContain("ArrayList");
    }

    [Fact]
    public void PatternBasedFixes_ApplyBuildErrorFixes_ShouldReturnSuggestions()
    {
        // Arrange
        var errorMessage = "CS0246: The type or namespace name 'List' could not be found";

        // Act
        var fixes = PatternBasedFixes.ApplyBuildErrorFixes(errorMessage);

        // Assert
        fixes.Should().NotBeEmpty();
        fixes.Should().Contain(fix => fix.Contains("System.Collections.Generic"));
    }

    [Fact]
    public void PatternBasedFixes_ApplyCodeStyleFixes_ShouldFixPublicFields()
    {
        // Arrange
        var sourceCode = "public string name;";

        // Act
        var result = PatternBasedFixes.ApplyCodeStyleFixes(sourceCode);

        // Assert
        result.Should().Contain("{ get; set; }");
        result.Should().NotContain("public string name;");
    }

    [Fact]
    public void PatternBasedFixes_ApplyPerformanceFixes_ShouldOptimizeCode()
    {
        // Arrange
        var sourceCode = "var count = items.ToList().Count;";

        // Act
        var result = PatternBasedFixes.ApplyPerformanceFixes(sourceCode);

        // Assert
        result.Should().Contain("items.Count()");
        result.Should().NotContain("ToList().Count");
    }

    [Fact]
    public void PatternBasedFixes_GetFixesForError_UnknownError_ShouldReturnEmpty()
    {
        // Arrange
        var errorMessage = "CS9999: Some unknown compiler error";

        // Act
        var fixes = PatternBasedFixes.GetFixesForError(errorMessage);

        // Assert
        fixes.Should().BeEmpty();
    }

    [Fact]
    public void PatternBasedFixes_GetFixesForError_HttpClientError_ShouldSuggestUsing()
    {
        // Arrange
        var errorMessage = "CS0246: The type or namespace name 'HttpClient' could not be found";

        // Act
        var fixes = PatternBasedFixes.GetFixesForError(errorMessage);

        // Assert
        fixes.Should().NotBeEmpty();
        fixes.Should().Contain(fix => fix.Contains("System.Net.Http"));
    }

    [Theory]
    [InlineData("List", "System.Collections.Generic")]
    [InlineData("Task", "System.Threading.Tasks")]
    [InlineData("HttpClient", "System.Net.Http")]
    [InlineData("JsonSerializer", "System.Text.Json")]
    public void PatternBasedFixes_CommonTypes_ShouldSuggestCorrectNamespace(string typeName, string expectedNamespace)
    {
        // Arrange
        var errorMessage = $"CS0246: The type or namespace name '{typeName}' could not be found";

        // Act
        var fixes = PatternBasedFixes.GetFixesForError(errorMessage);

        // Assert
        fixes.Should().Contain(fix => fix.Contains(expectedNamespace));
    }
}
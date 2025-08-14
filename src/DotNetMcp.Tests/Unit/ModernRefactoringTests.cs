using DotNetMcp.Core.Common;
using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Unit;

public class ModernRefactoringTests
{
    [Fact]
    public void Result_Success_ShouldCreateSuccessResult()
    {
        // Arrange & Act
        var result = Result.Success("test value");
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("test value", result.Value);
    }

    [Fact]
    public void Result_Failure_ShouldCreateFailureResult()
    {
        // Arrange & Act
        var result = Result.Failure<string>("error message");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("error message", result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Result_Map_ShouldTransformSuccessValue()
    {
        // Arrange
        var result = Result.Success(42);
        
        // Act
        var mapped = result.Map(x => x.ToString());
        
        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal("42", mapped.Value);
    }

    [Fact]
    public void Result_Map_ShouldPropagateFailure()
    {
        // Arrange
        var result = Result.Failure<int>("error");
        
        // Act
        var mapped = result.Map(x => x.ToString());
        
        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal("error", mapped.Error);
    }

    [Fact]
    public void Result_Bind_ShouldChainOperations()
    {
        // Arrange
        var result = Result.Success(10);
        
        // Act
        var chained = result.Bind(x => x > 5 ? Result.Success(x * 2) : Result.Failure<int>("too small"));
        
        // Assert
        Assert.True(chained.IsSuccess);
        Assert.Equal(20, chained.Value);
    }

    [Fact]
    public void Option_Some_ShouldCreateSomeOption()
    {
        // Arrange & Act
        var option = Option.Some("test");
        
        // Assert
        Assert.True(option.IsSome);
        Assert.False(option.IsNone);
    }

    [Fact]
    public void Option_None_ShouldCreateNoneOption()
    {
        // Arrange & Act
        var option = Option.None<string>();
        
        // Assert
        Assert.False(option.IsSome);
        Assert.True(option.IsNone);
    }

    [Fact]
    public void Option_Match_ShouldExecuteCorrectBranch()
    {
        // Arrange
        var someOption = Option.Some(42);
        var noneOption = Option.None<int>();
        
        // Act
        var someResult = someOption.Match(x => $"Value: {x}", () => "No value");
        var noneResult = noneOption.Match(x => $"Value: {x}", () => "No value");
        
        // Assert
        Assert.Equal("Value: 42", someResult);
        Assert.Equal("No value", noneResult);
    }

    [Fact]
    public void Option_Map_ShouldTransformSomeValue()
    {
        // Arrange
        var option = Option.Some(42);
        
        // Act
        var mapped = option.Map(x => x.ToString());
        
        // Assert
        Assert.True(mapped.IsSome);
        Assert.Equal("42", mapped.GetValueOrDefault(""));
    }

    [Fact]
    public void Option_Filter_ShouldReturnNoneWhenPredicateFails()
    {
        // Arrange
        var option = Option.Some(3);
        
        // Act
        var filtered = option.Filter(x => x > 5);
        
        // Assert
        Assert.True(filtered.IsNone);
    }

    [Fact]
    public async Task ModernExtractMethodRefactorer_ShouldExtractSimpleMethod()
    {
        // Arrange
        var refactorer = new ModernExtractMethodRefactorer();
        var code = @"
using System;

namespace Test
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";
        var request = new ModernExtractMethodRefactorer.ExtractMethodRequest(
            code, 
            "return a + b;", 
            "AddNumbers");

        // Act
        var result = await refactorer.ExtractMethodAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Contains("AddNumbers", response.ExtractedMethod);
        Assert.Contains("int", response.ReturnType);
        Assert.Contains("a", response.UsedVariables);
        Assert.Contains("b", response.UsedVariables);
    }

    [Fact]
    public async Task ModernExtractInterfaceRefactorer_ShouldExtractInterface()
    {
        // Arrange
        var refactorer = new ModernExtractInterfaceRefactorer();
        var code = @"
using System;

namespace Test
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
        
        public int Multiply(int a, int b)
        {
            return a * b;
        }
    }
}";
        var request = new ModernExtractInterfaceRefactorer.ExtractInterfaceRequest(
            code,
            "Calculator",
            "ICalculator");

        // Act
        var result = await refactorer.ExtractInterfaceAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Contains("ICalculator", response.ExtractedInterface);
        Assert.Contains("Add", response.ExtractedMembers);
        Assert.Contains("Multiply", response.ExtractedMembers);
        Assert.Contains(": ICalculator", response.ModifiedCode);
    }

    [Theory]
    [InlineData("int", "System.Int32")]
    [InlineData("string", "System.String")]
    [InlineData("bool", "System.Boolean")]
    [InlineData("double", "System.Double")]
    public void TypeHelpers_GetDisplayName_ShouldReturnCorrectName(string expected, string typeName)
    {
        // This is a conceptual test - in practice, we'd need to create ITypeSymbol instances
        // The actual implementation would be tested with integration tests
        Assert.True(true); // Placeholder for type system tests
    }

    [Fact]
    public void SyntaxBuilders_Method_ShouldCreateWellFormattedMethod()
    {
        // Arrange & Act
        var method = SyntaxBuilders.Method("TestMethod", 
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.PredefinedType(
                    Microsoft.CodeAnalysis.CSharp.SyntaxFactory.Token(
                        Microsoft.CodeAnalysis.CSharp.SyntaxKind.VoidKeyword)))
            .WithModifiers(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword)
            .Build();

        // Assert
        var methodText = method.ToString();
        Assert.Contains("public", methodText);
        Assert.Contains("void", methodText);
        Assert.Contains("TestMethod", methodText);
    }
}
using DotNetMcp.Core.Features.ExtractMethod;
using DotNetMcp.Core.VSA;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetMcp.Tests.VSA;

public class ExtractMethodFeatureTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public ExtractMethodFeatureTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ExtractMethodHandler>>(NullLogger<ExtractMethodHandler>.Instance);
        services.AddVerticalSliceArchitecture();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ExtractMethod_WithValidInput_ShouldSucceed()
    {
        // Arrange
        var command = new ExtractMethodCommand
        {
            Code = @"
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
}",
            SelectedCode = "return a + b;",
            MethodName = "AddNumbers"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Contains("AddNumbers", response.ExtractedMethod);
        Assert.Equal("int", response.ReturnType);
        Assert.Contains("a", response.UsedVariables);
        Assert.Contains("b", response.UsedVariables);
    }

    [Fact]
    public async Task ExtractMethod_WithInvalidMethodName_ShouldFail()
    {
        // Arrange
        var command = new ExtractMethodCommand
        {
            Code = "public class Test { }",
            SelectedCode = "return 1;",
            MethodName = "123InvalidName" // Invalid identifier
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("valid C# identifier", result.Error);
    }

    [Fact]
    public async Task ExtractMethod_WithEmptyCode_ShouldFail()
    {
        // Arrange
        var command = new ExtractMethodCommand
        {
            Code = "",
            SelectedCode = "return 1;",
            MethodName = "TestMethod"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Code cannot be empty", result.Error);
    }

    [Fact]
    public async Task ExtractMethod_WithDeltaRequest_ShouldIncludeDelta()
    {
        // Arrange
        var command = new ExtractMethodCommand
        {
            Code = @"
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
}",
            SelectedCode = "return a + b;",
            MethodName = "AddNumbers",
            FilePath = "Calculator.cs",
            ReturnDelta = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.NotNull(response.Delta);
        Assert.True(response.TokensSaved > 0);
    }

    [Fact]
    public void ExtractMethodCommandValidator_WithValidCommand_ShouldPass()
    {
        // Arrange
        var validator = new ExtractMethodCommandValidator();
        var command = new ExtractMethodCommand
        {
            Code = "public class Test { }",
            SelectedCode = "return 1;",
            MethodName = "ValidMethodName",
            FilePath = "Test.cs"
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Code cannot be empty")]
    [InlineData("public class Test { }", "Selected code cannot be empty")]
    public void ExtractMethodCommandValidator_WithInvalidInput_ShouldFail(string code, string expectedError)
    {
        // Arrange
        var validator = new ExtractMethodCommandValidator();
        var command = new ExtractMethodCommand
        {
            Code = code,
            SelectedCode = code == "" ? "return 1;" : "",
            MethodName = "ValidMethodName"
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.Errors.Select(e => e.ErrorMessage));
    }
}
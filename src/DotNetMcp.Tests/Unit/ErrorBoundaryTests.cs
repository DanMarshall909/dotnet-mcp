using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using DotNetMcp.Core.Extensions;
using DotNetMcp.Core.Features.CodeAnalysis;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotNetMcp.Tests.Unit;

/// <summary>
/// Tests for error boundary conditions and edge cases
/// </summary>
public class ErrorBoundaryTests : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public ErrorBoundaryTests()
    {
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddCoreServices();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task FindSymbol_NullProjectPath_ShouldHandleGracefully()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = null!,
            SymbolName = "TestSymbol",
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task FindSymbol_EmptyProjectPath_ShouldReturnError()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = "",
            SymbolName = "TestSymbol",
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Project path cannot be empty");
    }

    [Fact]
    public async Task FindSymbol_NonExistentProjectPath_ShouldReturnError()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = "/nonexistent/path",
            SymbolName = "TestSymbol", 
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task FindSymbol_EmptySymbolName_ShouldReturnError()
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolCommand
        {
            ProjectPath = "/test/project",
            SymbolName = "",
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Symbol name cannot be empty");
    }

    [Fact]
    public async Task FindSymbol_WhitespaceSymbolName_ShouldReturnError()
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolCommand
        {
            ProjectPath = "/test/project",
            SymbolName = "   ",
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Symbol name cannot be empty");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task FindSymbol_InvalidMaxResults_ShouldReturnError(int maxResults)
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolCommand
        {
            ProjectPath = "/test/project",
            SymbolName = "TestSymbol",
            SymbolType = SymbolType.Class,
            MaxResults = maxResults
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Max results must be between 1 and 1000");
    }

    [Fact]
    public async Task FindSymbol_InvalidSymbolName_ShouldReturnError()
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolCommand
        {
            ProjectPath = "/test/project",
            SymbolName = "123InvalidName!@#",
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("valid C# identifier");
    }

    [Fact]
    public async Task GetClassContext_NullClassName_ShouldReturnError()
    {
        // Arrange
        SetupValidProject();
        var command = new GetClassContextCommand
        {
            ProjectPath = "/test/project",
            ClassName = null!,
            MaxDepth = 2
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetClassContext_EmptyClassName_ShouldReturnError()
    {
        // Arrange
        SetupValidProject();
        var command = new GetClassContextCommand
        {
            ProjectPath = "/test/project",
            ClassName = "",
            MaxDepth = 2
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Class name cannot be empty");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(11)]
    public async Task GetClassContext_InvalidMaxDepth_ShouldReturnError(int maxDepth)
    {
        // Arrange
        SetupValidProject();
        var command = new GetClassContextCommand
        {
            ProjectPath = "/test/project",
            ClassName = "TestClass",
            MaxDepth = maxDepth
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Max depth must be between 1 and 10");
    }

    [Fact]
    public async Task AnalyzeProjectStructure_ProjectWithoutFiles_ShouldHandleGracefully()
    {
        // Arrange
        var projectPath = "/empty/project";
        _fileSystem.AddDirectory("/empty");
        _fileSystem.AddDirectory("/empty/project");
        _fileSystem.AddFile($"{projectPath}/Empty.csproj", new MockFileData(@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>"));

        var command = new AnalyzeProjectStructureCommand
        {
            ProjectPath = projectPath,
            MaxDepth = 3
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Files.Should().BeEmpty();
        result.Value.Namespaces.Should().BeEmpty();
        result.Value.Metrics.TotalFiles.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeProjectStructure_ProjectWithCorruptedFiles_ShouldHandleGracefully()
    {
        // Arrange  
        var projectPath = "/corrupted/project";
        _fileSystem.AddDirectory("/corrupted");
        _fileSystem.AddDirectory("/corrupted/project");
        _fileSystem.AddFile($"{projectPath}/Corrupted.csproj", new MockFileData(@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>"));

        // Add a file with invalid C# syntax
        _fileSystem.AddFile($"{projectPath}/Corrupted.cs", new MockFileData(@"
using System;
namespace CorruptedProject
{
    public class CorruptedClass
        // Missing opening brace
        public void InvalidMethod()
        {
            Console.WriteLine(""This is corrupted"";
        }
        // Missing closing brace
}"));

        var command = new AnalyzeProjectStructureCommand
        {
            ProjectPath = projectPath,
            MaxDepth = 3
        };

        // Debug: Check what files are actually found
        var csharpFiles = _fileSystem.Directory
            .GetFiles(projectPath, "*.cs", System.IO.SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToArray();
        
        System.Console.WriteLine($"Debug: Found {csharpFiles.Length} C# files in {projectPath}");
        foreach (var file in csharpFiles)
        {
            System.Console.WriteLine($"Debug: File - {file}");
        }

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Should not fail completely
        result.Value.Files.Should().NotBeEmpty(); // Should still find files
        result.Value.ProjectInfo.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task AnalyzeProjectStructure_InvalidMaxDepth_ShouldReturnError(int maxDepth)
    {
        // Arrange
        SetupValidProject();
        var command = new AnalyzeProjectStructureCommand
        {
            ProjectPath = "/test/project",
            MaxDepth = maxDepth
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Max depth must be between 1 and 100");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50001)]
    public async Task AnalyzeProjectStructure_InvalidMaxTokens_ShouldReturnError(int maxTokens)
    {
        // Arrange
        SetupValidProject();
        var command = new AnalyzeProjectStructureCommand
        {
            ProjectPath = "/test/project",
            OptimizeForTokens = true,
            MaxTokens = maxTokens,
            MaxDepth = 3
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Max tokens must be between 1 and 50000");
    }

    [Fact]
    public async Task FindSymbolUsages_NullSymbolName_ShouldReturnError()
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = "/test/project",
            SymbolName = null!,
            SymbolType = SymbolType.Method
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task FindSymbolUsages_InvalidMaxResults_ShouldReturnError(int maxResults)
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = "/test/project",
            SymbolName = "TestMethod",
            SymbolType = SymbolType.Method,
            MaxResults = maxResults
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Max results must be between 1 and 1000");
    }

    [Fact]
    public async Task ConcurrentRequests_ShouldHandleSafely()
    {
        // Arrange
        SetupValidProject();
        var command = new FindSymbolCommand
        {
            ProjectPath = "/test/project",
            SymbolName = "TestClass",
            SymbolType = SymbolType.Class,
            MaxResults = 10
        };

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _mediator.Send(command))
            .ToArray();
        
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(result => result.Should().NotBeNull());
        // All should succeed or all should fail consistently
        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess);
        (successCount == 10 || failureCount == 10).Should().BeTrue();
    }

    private void SetupValidProject()
    {
        var projectPath = "/test/project";
        _fileSystem.AddDirectory(projectPath);
        
        _fileSystem.AddFile($"{projectPath}/TestProject.csproj", new MockFileData(@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>"));

        _fileSystem.AddFile($"{projectPath}/TestClass.cs", new MockFileData(@"
using System;

namespace TestProject
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Test"");
        }
    }
}"));
    }
}
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.VSA;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetMcp.Tests.BehaviorTests.CodeAnalysis;

public class Finding_symbols_in_codebase : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly string _projectPath;

    public Finding_symbols_in_codebase()
    {
        _projectPath = "/test/project";
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddVerticalSliceArchitecture();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        SetupTestCodebase();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private void SetupTestCodebase()
    {
        // Create a realistic C# project structure
        _fileSystem.AddFile($"{_projectPath}/Models/User.cs", new MockFileData(@"
using System;

namespace TestProject.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        
        public void UpdateEmail(string newEmail)
        {
            Email = newEmail;
        }
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Services/UserService.cs", new MockFileData(@"
using TestProject.Models;
using System.Collections.Generic;

namespace TestProject.Services
{
    public class UserService : IUserService
    {
        private readonly List<User> _users = new();
        
        public User GetUser(int id)
        {
            return _users.Find(u => u.Id == id);
        }
        
        public void AddUser(User user)
        {
            _users.Add(user);
        }
    }
    
    public interface IUserService
    {
        User GetUser(int id);
        void AddUser(User user);
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Controllers/UserController.cs", new MockFileData(@"
using TestProject.Models;
using TestProject.Services;

namespace TestProject.Controllers
{
    public class UserController
    {
        private readonly IUserService _userService;
        
        public UserController(IUserService userService)
        {
            _userService = userService;
        }
        
        public User GetUser(int id)
        {
            return _userService.GetUser(id);
        }
    }
}"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "C# project with multiple classes")]
    [Trait("When", "Searching for class by name")]
    [Trait("Then", "Returns matching class with location and basic info")]
    public async Task Finds_class_by_exact_name()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "User",
            SymbolType = SymbolType.Class
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Symbols.Should().HaveCount(1);
        
        var symbol = result.Value.Symbols.First();
        symbol.Name.Should().Be("User");
        symbol.SymbolType.Should().Be(SymbolType.Class);
        symbol.Namespace.Should().Be("TestProject.Models");
        symbol.FilePath.Should().Contain("Models/User.cs");
        symbol.LineNumber.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "C# project with interfaces and implementations")]
    [Trait("When", "Searching for interface by name")]
    [Trait("Then", "Returns interface with implementations")]
    public async Task Finds_interface_with_implementations()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "IUserService",
            SymbolType = SymbolType.Interface,
            IncludeImplementations = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var symbol = result.Value.Symbols.First();
        symbol.Name.Should().Be("IUserService");
        symbol.SymbolType.Should().Be(SymbolType.Interface);
        symbol.Implementations.Should().NotBeEmpty();
        symbol.Implementations.Should().Contain(impl => impl.Name == "UserService");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "C# project with method overloads")]
    [Trait("When", "Searching for method by name")]
    [Trait("Then", "Returns all overloads with signatures")]
    public async Task Finds_method_with_all_overloads()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "GetUser",
            SymbolType = SymbolType.Method
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Symbols.Should().HaveCountGreaterOrEqualTo(2); // UserService.GetUser and UserController.GetUser
        
        var methods = result.Value.Symbols.Where(s => s.Name == "GetUser").ToList();
        methods.Should().AllSatisfy(method =>
        {
            method.SymbolType.Should().Be(SymbolType.Method);
            method.Signature.Should().NotBeNullOrEmpty();
            method.Parameters.Should().NotBeNull();
        });
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "C# project")]
    [Trait("When", "Searching with wildcard pattern")]
    [Trait("Then", "Returns all matching symbols")]
    public async Task Finds_symbols_with_wildcard_pattern()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "User*",
            SymbolType = SymbolType.Any
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Symbols.Should().HaveCountGreaterOrEqualTo(2); // User class and UserService class
        result.Value.Symbols.Should().Contain(s => s.Name == "User");
        result.Value.Symbols.Should().Contain(s => s.Name == "UserService");
        result.Value.Symbols.Should().Contain(s => s.Name == "UserController");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Empty or invalid project path")]
    [Trait("When", "Searching for symbols")]
    [Trait("Then", "Returns meaningful error message")]
    public async Task Returns_error_for_invalid_project_path()
    {
        // Arrange
        var command = new FindSymbolCommand
        {
            ProjectPath = "/nonexistent/path",
            SymbolName = "User",
            SymbolType = SymbolType.Class
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project path not found");
    }
}
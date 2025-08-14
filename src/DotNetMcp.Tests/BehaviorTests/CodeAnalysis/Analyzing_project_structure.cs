using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.VSA;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotNetMcp.Tests.BehaviorTests.CodeAnalysis;

public class Analyzing_project_structure : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly string _solutionPath;

    public Analyzing_project_structure()
    {
        _solutionPath = "/test/solution";
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddVerticalSliceArchitecture();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        SetupRealisticSolution();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private void SetupRealisticSolution()
    {
        // Solution file
        _fileSystem.AddFile($"{_solutionPath}/ECommerce.sln", new MockFileData(@"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""ECommerce.Domain"", ""src\ECommerce.Domain\ECommerce.Domain.csproj"", ""{GUID}""
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""ECommerce.Application"", ""src\ECommerce.Application\ECommerce.Application.csproj"", ""{GUID}""
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""ECommerce.Infrastructure"", ""src\ECommerce.Infrastructure\ECommerce.Infrastructure.csproj"", ""{GUID}""
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""ECommerce.API"", ""src\ECommerce.API\ECommerce.API.csproj"", ""{GUID}""
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""ECommerce.Tests"", ""tests\ECommerce.Tests\ECommerce.Tests.csproj"", ""{GUID}""
"));

        // Domain layer
        _fileSystem.AddFile($"{_solutionPath}/src/ECommerce.Domain/Entities/Product.cs", new MockFileData(@"
namespace ECommerce.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public Category Category { get; set; }
        public List<Review> Reviews { get; set; } = new();
    }
}"));

        _fileSystem.AddFile($"{_solutionPath}/src/ECommerce.Domain/Entities/Order.cs", new MockFileData(@"
namespace ECommerce.Domain.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public Customer Customer { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);
    }
}"));

        // Application layer
        _fileSystem.AddFile($"{_solutionPath}/src/ECommerce.Application/Services/OrderService.cs", new MockFileData(@"
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services
{
    public class OrderService
    {
        public async Task<Order> CreateOrderAsync(CreateOrderCommand command) { }
        public async Task<List<Order>> GetOrdersAsync(int customerId) { }
    }
}"));

        // Infrastructure layer
        _fileSystem.AddFile($"{_solutionPath}/src/ECommerce.Infrastructure/Repositories/OrderRepository.cs", new MockFileData(@"
using ECommerce.Domain.Entities;

namespace ECommerce.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        public async Task<Order> GetByIdAsync(int id) { }
        public async Task SaveAsync(Order order) { }
    }
}"));

        // API layer
        _fileSystem.AddFile($"{_solutionPath}/src/ECommerce.API/Controllers/OrdersController.cs", new MockFileData(@"
using Microsoft.AspNetCore.Mvc;
using ECommerce.Application.Services;

namespace ECommerce.API.Controllers
{
    [ApiController]
    [Route(""api/[controller]"")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderService _orderService;
        
        [HttpGet]
        public async Task<IActionResult> GetOrders() { }
        
        [HttpPost]
        public async Task<IActionResult> CreateOrder() { }
    }
}"));

        // Tests
        _fileSystem.AddFile($"{_solutionPath}/tests/ECommerce.Tests/OrderServiceTests.cs", new MockFileData(@"
using ECommerce.Application.Services;
using Xunit;

namespace ECommerce.Tests
{
    public class OrderServiceTests
    {
        [Fact]
        public async Task CreateOrder_ValidData_ReturnsOrder() { }
    }
}"));

        // Configuration files
        _fileSystem.AddFile($"{_solutionPath}/src/ECommerce.API/appsettings.json", new MockFileData(@"
{
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=ECommerce;""
  }
}"));

        _fileSystem.AddFile($"{_solutionPath}/Directory.Build.props", new MockFileData(@"
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Multi-project .NET solution")]
    [Trait("When", "Analyzing project structure")]
    [Trait("Then", "Returns organized overview of solution architecture")]
    public async Task Analyzes_solution_architecture_and_project_relationships()
    {
        // Arrange
        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = $"{_solutionPath}/ECommerce.sln",
            IncludeMetrics = true,
            IncludeDependencies = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var structure = result.Value;
        
        // Solution overview
        structure.SolutionName.Should().Be("ECommerce");
        structure.ProjectCount.Should().Be(5);
        
        // Projects organized by type
        structure.Projects.Should().HaveCount(5);
        structure.Projects.Should().Contain(p => p.Name == "ECommerce.Domain");
        structure.Projects.Should().Contain(p => p.Name == "ECommerce.Application");
        structure.Projects.Should().Contain(p => p.Name == "ECommerce.Infrastructure");
        structure.Projects.Should().Contain(p => p.Name == "ECommerce.API");
        structure.Projects.Should().Contain(p => p.Name == "ECommerce.Tests");
        
        // Architecture layers
        structure.ArchitectureLayers.Should().NotBeEmpty();
        structure.ArchitectureLayers.Should().Contain(layer => layer.Name == "Domain");
        structure.ArchitectureLayers.Should().Contain(layer => layer.Name == "Application");
        structure.ArchitectureLayers.Should().Contain(layer => layer.Name == "Infrastructure");
        structure.ArchitectureLayers.Should().Contain(layer => layer.Name == "Presentation");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Solution with domain entities")]
    [Trait("When", "Analyzing project structure with focus on key classes")]
    [Trait("Then", "Returns key entities and their relationships")]
    public async Task Identifies_key_domain_entities_and_relationships()
    {
        // Arrange
        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = $"{_solutionPath}/ECommerce.sln",
            FocusOnKeyClasses = true,
            IncludeRelationships = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var structure = result.Value;
        
        // Key entities identified
        structure.KeyClasses.Should().NotBeEmpty();
        structure.KeyClasses.Should().Contain(c => c.Name == "Product");
        structure.KeyClasses.Should().Contain(c => c.Name == "Order");
        structure.KeyClasses.Should().Contain(c => c.Name == "OrderService");
        structure.KeyClasses.Should().Contain(c => c.Name == "OrdersController");
        
        // Entity relationships
        structure.Relationships.Should().NotBeEmpty();
        structure.Relationships.Should().Contain(r => 
            r.FromClass == "Order" && 
            r.ToClass == "OrderItem" && 
            r.RelationshipType == "HasMany");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Solution with tests")]
    [Trait("When", "Analyzing project structure with test analysis")]
    [Trait("Then", "Returns test coverage and patterns")]
    public async Task Analyzes_test_coverage_and_testing_patterns()
    {
        // Arrange
        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = $"{_solutionPath}/ECommerce.sln",
            IncludeTestAnalysis = true,
            IncludeMetrics = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var structure = result.Value;
        
        // Test analysis
        structure.TestAnalysis.Should().NotBeNull();
        structure.TestAnalysis.TestProjects.Should().HaveCount(1);
        structure.TestAnalysis.TestProjects.Should().Contain("ECommerce.Tests");
        
        // Coverage analysis
        structure.TestAnalysis.TestedClasses.Should().Contain("OrderService");
        structure.TestAnalysis.UntestedClasses.Should().NotBeEmpty();
        
        // Test patterns
        structure.TestAnalysis.TestPatterns.Should().NotBeEmpty();
        structure.TestAnalysis.TestFrameworks.Should().Contain("xUnit");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Solution request with token optimization")]
    [Trait("When", "Analyzing large solution")]
    [Trait("Then", "Returns token-efficient summary focused on architecture")]
    public async Task Returns_token_optimized_summary_for_large_solutions()
    {
        // Arrange
        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = $"{_solutionPath}/ECommerce.sln",
            MaxTokens = 800,
            OptimizeForTokens = true,
            FocusOnArchitecture = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var structure = result.Value;
        
        // Should provide architectural overview
        structure.ArchitectureSummary.Should().NotBeNullOrEmpty();
        structure.ArchitectureSummary.Should().Contain("Clean Architecture");
        structure.ArchitectureSummary.Should().Contain("Domain-Driven Design");
        
        // Should be within token budget
        structure.EstimatedTokens.Should().BeLessOrEqualTo(800);
        
        // Should prioritize most important information
        structure.KeyInsights.Should().NotBeEmpty();
        structure.KeyInsights.Should().HaveCountLessOrEqualTo(5);
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Solution with design patterns")]
    [Trait("When", "Analyzing project structure with pattern detection")]
    [Trait("Then", "Returns identified design patterns and architectural decisions")]
    public async Task Identifies_design_patterns_and_architectural_decisions()
    {
        // Arrange
        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = $"{_solutionPath}/ECommerce.sln",
            DetectPatterns = true,
            IncludeArchitecturalAnalysis = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var structure = result.Value;
        
        // Design patterns detected
        structure.DesignPatterns.Should().NotBeEmpty();
        structure.DesignPatterns.Should().Contain(p => p.PatternName == "Repository Pattern");
        structure.DesignPatterns.Should().Contain(p => p.PatternName == "Service Layer");
        structure.DesignPatterns.Should().Contain(p => p.PatternName == "MVC Pattern");
        
        // Architectural decisions
        structure.ArchitecturalDecisions.Should().NotBeEmpty();
        structure.ArchitecturalDecisions.Should().Contain(d => d.Decision.Contains("Clean Architecture"));
        structure.ArchitecturalDecisions.Should().Contain(d => d.Decision.Contains("Domain-Driven Design"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Invalid solution path")]
    [Trait("When", "Analyzing project structure")]
    [Trait("Then", "Returns helpful error with suggestions")]
    public async Task Returns_helpful_error_for_invalid_solution_path()
    {
        // Arrange
        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = "/nonexistent/solution.sln"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Solution file not found");
        result.Error.Should().Contain("Make sure the path is correct");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Single project instead of solution")]
    [Trait("When", "Analyzing project structure")]
    [Trait("Then", "Analyzes single project structure")]
    public async Task Analyzes_single_project_when_solution_not_available()
    {
        // Arrange - Add a single project
        _fileSystem.AddFile($"{_solutionPath}/SingleProject/SingleProject.csproj", new MockFileData(@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>"));

        var command = new AnalyzeProjectStructureCommand
        {
            SolutionPath = $"{_solutionPath}/SingleProject/SingleProject.csproj"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var structure = result.Value;
        
        structure.SolutionName.Should().Be("SingleProject");
        structure.ProjectCount.Should().Be(1);
        structure.Projects.Should().HaveCount(1);
        structure.IsSingleProject.Should().BeTrue();
    }
}
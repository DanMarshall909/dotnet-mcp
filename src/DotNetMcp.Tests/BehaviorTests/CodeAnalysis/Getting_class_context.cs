using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.VSA;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotNetMcp.Tests.BehaviorTests.CodeAnalysis;

public class Getting_class_context : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly string _projectPath;

    public Getting_class_context()
    {
        _projectPath = "/test/project";
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddVerticalSliceArchitecture();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        SetupComplexCodebase();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private void SetupComplexCodebase()
    {
        // Payment domain with dependencies
        _fileSystem.AddFile($"{_projectPath}/Domain/Payment.cs", new MockFileData(@"
using System;
using TestProject.Domain.Common;

namespace TestProject.Domain
{
    public class Payment : BaseEntity
    {
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string Currency { get; set; }
        public DateTime ProcessedAt { get; set; }
        
        public void Process()
        {
            Status = PaymentStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
        }
    }
    
    public enum PaymentStatus
    {
        Pending,
        Processed,
        Failed
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Domain/Common/BaseEntity.cs", new MockFileData(@"
namespace TestProject.Domain.Common
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Services/PaymentService.cs", new MockFileData(@"
using TestProject.Domain;
using TestProject.Repositories;

namespace TestProject.Services
{
    public class PaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly INotificationService _notificationService;
        
        public PaymentService(IPaymentRepository paymentRepository, INotificationService notificationService)
        {
            _paymentRepository = paymentRepository;
            _notificationService = notificationService;
        }
        
        public async Task<bool> ProcessPayment(Payment payment)
        {
            payment.Process();
            await _paymentRepository.SaveAsync(payment);
            await _notificationService.SendPaymentConfirmation(payment);
            return true;
        }
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Repositories/IPaymentRepository.cs", new MockFileData(@"
using TestProject.Domain;

namespace TestProject.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment> GetByIdAsync(int id);
        Task SaveAsync(Payment payment);
        Task<List<Payment>> GetByStatusAsync(PaymentStatus status);
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Services/INotificationService.cs", new MockFileData(@"
using TestProject.Domain;

namespace TestProject.Services
{
    public interface INotificationService
    {
        Task SendPaymentConfirmation(Payment payment);
        Task SendPaymentFailure(Payment payment, string reason);
    }
}"));

        _fileSystem.AddFile($"{_projectPath}/Tests/PaymentServiceTests.cs", new MockFileData(@"
using TestProject.Domain;
using TestProject.Services;
using TestProject.Repositories;
using Xunit;
using NSubstitute;

namespace TestProject.Tests
{
    public class PaymentServiceTests
    {
        [Fact]
        public async Task ProcessPayment_ValidPayment_ReturnsTrue()
        {
            // Arrange
            var payment = new Payment { Amount = 100m };
            var repository = Substitute.For<IPaymentRepository>();
            var notification = Substitute.For<INotificationService>();
            var service = new PaymentService(repository, notification);
            
            // Act
            var result = await service.ProcessPayment(payment);
            
            // Assert
            result.Should().BeTrue();
            payment.Status.Should().Be(PaymentStatus.Processed);
        }
    }
}"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Complex class with dependencies")]
    [Trait("When", "Getting class context")]
    [Trait("Then", "Returns class with all its dependencies and usage")]
    public async Task Gets_complete_class_context_with_dependencies()
    {
        // Arrange
        var command = new GetClassContextCommand
        {
            ProjectPath = _projectPath,
            ClassName = "PaymentService",
            IncludeDependencies = true,
            IncludeUsages = true,
            MaxDepth = 2
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var context = result.Value;
        
        // Main class
        context.MainClass.Name.Should().Be("PaymentService");
        context.MainClass.Namespace.Should().Be("TestProject.Services");
        context.MainClass.Methods.Should().Contain(m => m.Name == "ProcessPayment");
        
        // Dependencies
        context.Dependencies.Should().HaveCountGreaterOrEqualTo(2);
        context.Dependencies.Should().Contain(d => d.Name == "IPaymentRepository");
        context.Dependencies.Should().Contain(d => d.Name == "INotificationService");
        context.Dependencies.Should().Contain(d => d.Name == "Payment");
        
        // Usage examples
        context.Usages.Should().NotBeEmpty();
        context.Usages.Should().Contain(u => u.FilePath.Contains("Tests"));
        
        // Token efficiency check
        context.Summary.Should().NotBeNull();
        context.Summary.TotalLines.Should().BeGreaterThan(0);
        context.Summary.CoreComplexity.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Domain entity class")]
    [Trait("When", "Getting class context with inheritance")]
    [Trait("Then", "Returns class with base classes and derived relationships")]
    public async Task Gets_class_context_with_inheritance_relationships()
    {
        // Arrange
        var command = new GetClassContextCommand
        {
            ProjectPath = _projectPath,
            ClassName = "Payment",
            IncludeInheritance = true,
            IncludeDependencies = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var context = result.Value;
        
        // Main class
        context.MainClass.Name.Should().Be("Payment");
        context.MainClass.BaseClass.Should().Be("BaseEntity");
        
        // Inheritance chain
        context.InheritanceChain.Should().NotBeEmpty();
        context.InheritanceChain.Should().Contain(c => c.Name == "BaseEntity");
        
        // Properties from base class should be included
        context.MainClass.Properties.Should().Contain(p => p.Name == "Id");
        context.MainClass.Properties.Should().Contain(p => p.Name == "CreatedAt");
        
        // Enum dependencies
        context.Dependencies.Should().Contain(d => d.Name == "PaymentStatus");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Interface with implementations")]
    [Trait("When", "Getting interface context")]
    [Trait("Then", "Returns interface with all implementations and usage patterns")]
    public async Task Gets_interface_context_with_implementations()
    {
        // Arrange
        var command = new GetClassContextCommand
        {
            ProjectPath = _projectPath,
            ClassName = "IPaymentRepository",
            IncludeImplementations = true,
            IncludeUsages = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var context = result.Value;
        
        // Interface details
        context.MainClass.Name.Should().Be("IPaymentRepository");
        context.MainClass.IsInterface.Should().BeTrue();
        context.MainClass.Methods.Should().HaveCountGreaterOrEqualTo(3);
        
        // Usage in dependency injection
        context.Usages.Should().Contain(u => u.UsageType == "Constructor Parameter");
        context.Usages.Should().Contain(u => u.FilePath.Contains("PaymentService"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Request for token-optimized context")]
    [Trait("When", "Getting class context with token limits")]
    [Trait("Then", "Returns summarized context within token budget")]
    public async Task Gets_token_optimized_class_context()
    {
        // Arrange
        var command = new GetClassContextCommand
        {
            ProjectPath = _projectPath,
            ClassName = "PaymentService",
            IncludeDependencies = true,
            MaxTokens = 500, // Force summarization
            OptimizeForTokens = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var context = result.Value;
        
        // Should include core information
        context.MainClass.Name.Should().Be("PaymentService");
        context.MainClass.Methods.Should().NotBeEmpty();
        
        // Should be summarized for token efficiency
        context.Summary.EstimatedTokens.Should().BeLessOrEqualTo(500);
        context.Summary.SummarizationApplied.Should().BeTrue();
        
        // Should prioritize most important dependencies
        context.Dependencies.Should().NotBeEmpty();
        context.Dependencies.Should().Contain(d => d.Name == "Payment"); // Core domain object
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Nonexistent class name")]
    [Trait("When", "Getting class context")]
    [Trait("Then", "Returns helpful error with suggestions")]
    public async Task Returns_helpful_error_for_nonexistent_class()
    {
        // Arrange
        var command = new GetClassContextCommand
        {
            ProjectPath = _projectPath,
            ClassName = "NonexistentService",
            IncludeDependencies = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        
        // Should provide suggestions for similar names
        result.Error.Should().Contain("PaymentService"); // Similar name suggestion
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Class context request")]
    [Trait("When", "Including test context")]
    [Trait("Then", "Returns related test files and test patterns")]
    public async Task Gets_class_context_with_test_relationships()
    {
        // Arrange
        var command = new GetClassContextCommand
        {
            ProjectPath = _projectPath,
            ClassName = "PaymentService",
            IncludeTestContext = true,
            IncludeUsages = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var context = result.Value;
        
        // Should find related test files
        context.TestContext.Should().NotBeNull();
        context.TestContext.TestFiles.Should().NotBeEmpty();
        context.TestContext.TestFiles.Should().Contain(tf => tf.Contains("PaymentServiceTests"));
        
        // Should analyze test patterns
        context.TestContext.TestMethods.Should().NotBeEmpty();
        context.TestContext.CoverageGaps.Should().NotBeNull();
    }
}
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.VSA;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotNetMcp.Tests.BehaviorTests.CodeAnalysis;

public class Finding_symbol_usages : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly string _projectPath;

    public Finding_symbol_usages()
    {
        _projectPath = "/test/project";
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddVerticalSliceArchitecture();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        SetupCodebaseWithUsages();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private void SetupCodebaseWithUsages()
    {
        // Core service interface
        _fileSystem.AddFile($"{_projectPath}/Services/IEmailService.cs", new MockFileData(@"
namespace TestProject.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendWelcomeEmailAsync(string userEmail);
        Task SendPasswordResetEmailAsync(string userEmail, string resetToken);
    }
}"));

        // Implementation
        _fileSystem.AddFile($"{_projectPath}/Services/EmailService.cs", new MockFileData(@"
using Microsoft.Extensions.Logging;

namespace TestProject.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        
        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }
        
        public async Task SendEmailAsync(string to, string subject, string body)
        {
            _logger.LogInformation(""Sending email to {Email}"", to);
            // Email sending logic
        }
        
        public async Task SendWelcomeEmailAsync(string userEmail)
        {
            await SendEmailAsync(userEmail, ""Welcome!"", ""Welcome to our platform"");
        }
        
        public async Task SendPasswordResetEmailAsync(string userEmail, string resetToken)
        {
            var resetLink = $""https://app.com/reset?token={resetToken}"";
            await SendEmailAsync(userEmail, ""Password Reset"", $""Click here: {resetLink}"");
        }
    }
}"));

        // Usage in user service
        _fileSystem.AddFile($"{_projectPath}/Services/UserService.cs", new MockFileData(@"
using TestProject.Models;

namespace TestProject.Services
{
    public class UserService
    {
        private readonly IEmailService _emailService;
        
        public UserService(IEmailService emailService)
        {
            _emailService = emailService;
        }
        
        public async Task RegisterUserAsync(User user)
        {
            // Save user logic
            await _emailService.SendWelcomeEmailAsync(user.Email);
        }
        
        public async Task InitiatePasswordResetAsync(string email)
        {
            var resetToken = GenerateResetToken();
            await _emailService.SendPasswordResetEmailAsync(email, resetToken);
        }
        
        private string GenerateResetToken() => Guid.NewGuid().ToString();
    }
}"));

        // Usage in controller
        _fileSystem.AddFile($"{_projectPath}/Controllers/AccountController.cs", new MockFileData(@"
using Microsoft.AspNetCore.Mvc;
using TestProject.Services;

namespace TestProject.Controllers
{
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly IEmailService _emailService;
        
        public AccountController(UserService userService, IEmailService emailService)
        {
            _userService = userService;
            _emailService = emailService;
        }
        
        [HttpPost(""contact"")]
        public async Task<IActionResult> SendContactEmail(ContactRequest request)
        {
            await _emailService.SendEmailAsync(
                ""support@company.com"", 
                $""Contact from {request.Name}"", 
                request.Message);
            return Ok();
        }
        
        [HttpPost(""password-reset"")]
        public async Task<IActionResult> RequestPasswordReset(string email)
        {
            await _userService.InitiatePasswordResetAsync(email);
            return Ok();
        }
    }
}"));

        // Usage in notification service
        _fileSystem.AddFile($"{_projectPath}/Services/NotificationService.cs", new MockFileData(@"
namespace TestProject.Services
{
    public class NotificationService
    {
        private readonly IEmailService _emailService;
        
        public NotificationService(IEmailService emailService)
        {
            _emailService = emailService;
        }
        
        public async Task NotifyAdminAsync(string message)
        {
            await _emailService.SendEmailAsync(""admin@company.com"", ""Admin Notification"", message);
        }
    }
}"));

        // DI registration
        _fileSystem.AddFile($"{_projectPath}/Program.cs", new MockFileData(@"
using TestProject.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();
app.Run();
"));

        // Test file
        _fileSystem.AddFile($"{_projectPath}/Tests/EmailServiceTests.cs", new MockFileData(@"
using TestProject.Services;
using Xunit;
using NSubstitute;

namespace TestProject.Tests
{
    public class EmailServiceTests
    {
        [Fact]
        public async Task SendEmailAsync_ValidInput_CallsLogger()
        {
            var emailService = new EmailService(Substitute.For<ILogger<EmailService>>());
            await emailService.SendEmailAsync(""test@test.com"", ""Subject"", ""Body"");
        }
        
        [Fact]
        public async Task SendWelcomeEmailAsync_CallsSendEmailAsync()
        {
            var emailService = new EmailService(Substitute.For<ILogger<EmailService>>());
            await emailService.SendWelcomeEmailAsync(""user@test.com"");
        }
    }
}"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Interface used across multiple classes")]
    [Trait("When", "Finding all usages of interface")]
    [Trait("Then", "Returns all dependency injection and method call usages")]
    public async Task Finds_all_interface_usages_across_codebase()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "IEmailService",
            IncludeConstructorInjection = true,
            IncludeMethodCalls = true,
            IncludeDependencyRegistration = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should find DI registrations
        usages.Usages.Should().Contain(u => 
            u.UsageType == "DependencyRegistration" && 
            u.FilePath.Contains("Program.cs"));
        
        // Should find constructor injections
        usages.Usages.Should().Contain(u => 
            u.UsageType == "ConstructorParameter" && 
            u.FilePath.Contains("UserService.cs"));
        usages.Usages.Should().Contain(u => 
            u.UsageType == "ConstructorParameter" && 
            u.FilePath.Contains("AccountController.cs"));
        
        // Should find method calls
        usages.Usages.Should().Contain(u => 
            u.UsageType == "MethodCall" && 
            u.MethodName == "SendWelcomeEmailAsync");
        usages.Usages.Should().Contain(u => 
            u.UsageType == "MethodCall" && 
            u.MethodName == "SendEmailAsync");
        
        // Should provide usage statistics
        usages.Summary.TotalUsages.Should().BeGreaterThan(5);
        usages.Summary.UsagesByType.Should().ContainKey("ConstructorParameter");
        usages.Summary.UsagesByType.Should().ContainKey("MethodCall");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Specific method with multiple call sites")]
    [Trait("When", "Finding method usages")]
    [Trait("Then", "Returns all method call locations with context")]
    public async Task Finds_method_usages_with_call_context()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "SendEmailAsync",
            SymbolType = SymbolType.Method,
            IncludeCallContext = true,
            IncludeParameters = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should find all call sites
        usages.Usages.Should().HaveCountGreaterOrEqualTo(4);
        
        // Should include call context
        var welcomeEmailCall = usages.Usages.FirstOrDefault(u => 
            u.FilePath.Contains("EmailService.cs") && 
            u.CallingMethod == "SendWelcomeEmailAsync");
        welcomeEmailCall.Should().NotBeNull();
        welcomeEmailCall.CallContext.Should().Contain("Welcome!");
        
        // Should show parameter values where possible
        var contactCall = usages.Usages.FirstOrDefault(u => 
            u.FilePath.Contains("AccountController.cs"));
        contactCall.Should().NotBeNull();
        contactCall.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Class with implementations")]
    [Trait("When", "Finding class usages")]
    [Trait("Then", "Returns instantiations, inheritance, and dependency injection")]
    public async Task Finds_class_usages_including_inheritance_and_instantiation()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "EmailService",
            SymbolType = SymbolType.Class,
            IncludeInheritance = true,
            IncludeInstantiation = true,
            IncludeDependencyRegistration = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should find interface implementation
        usages.Usages.Should().Contain(u => 
            u.UsageType == "InterfaceImplementation" && 
            u.InterfaceName == "IEmailService");
        
        // Should find DI registration
        usages.Usages.Should().Contain(u => 
            u.UsageType == "DependencyRegistration" && 
            u.FilePath.Contains("Program.cs"));
        
        // Should find test instantiations
        usages.Usages.Should().Contain(u => 
            u.UsageType == "ObjectCreation" && 
            u.FilePath.Contains("Tests"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Symbol usage search with filters")]
    [Trait("When", "Filtering by usage type and file patterns")]
    [Trait("Then", "Returns only matching usages")]
    public async Task Filters_usages_by_type_and_file_patterns()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "IEmailService",
            UsageTypeFilter = new[] { "ConstructorParameter" },
            FilePatternFilter = "*Service.cs",
            ExcludeTests = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should only include constructor parameters
        usages.Usages.Should().AllSatisfy(u => u.UsageType == "ConstructorParameter");
        
        // Should only include Service files
        usages.Usages.Should().AllSatisfy(u => u.FilePath.Contains("Service.cs"));
        
        // Should exclude test files
        usages.Usages.Should().NotContain(u => u.FilePath.Contains("Test"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Symbol usage search with impact analysis")]
    [Trait("When", "Finding usages with refactoring impact")]
    [Trait("Then", "Returns usage impact assessment for safe refactoring")]
    public async Task Analyzes_refactoring_impact_of_symbol_changes()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "SendEmailAsync",
            AnalyzeRefactoringImpact = true,
            IncludeBreakingChangeAnalysis = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should include impact analysis
        usages.RefactoringImpact.Should().NotBeNull();
        usages.RefactoringImpact.AffectedFiles.Should().HaveCountGreaterThan(0);
        usages.RefactoringImpact.BreakingChanges.Should().NotBeNull();
        
        // Should categorize by risk level
        usages.RefactoringImpact.RiskLevel.Should().BeOneOf("Low", "Medium", "High");
        usages.RefactoringImpact.SafeToRefactor.Should().HaveValue();
        
        // Should provide recommendations
        usages.RefactoringImpact.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Token-optimized usage search")]
    [Trait("When", "Finding usages with token limits")]
    [Trait("Then", "Returns prioritized usage summary")]
    public async Task Returns_token_optimized_usage_summary()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "IEmailService",
            MaxTokens = 300,
            OptimizeForTokens = true,
            PrioritizeByImportance = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should be within token budget
        usages.EstimatedTokens.Should().BeLessOrEqualTo(300);
        
        // Should prioritize most important usages
        usages.Usages.Should().NotBeEmpty();
        usages.Summary.SummarizationApplied.Should().BeTrue();
        
        // Should include high-impact usages first
        var firstUsage = usages.Usages.First();
        firstUsage.ImportanceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Nonexistent symbol")]
    [Trait("When", "Finding symbol usages")]
    [Trait("Then", "Returns helpful error with similar symbol suggestions")]
    public async Task Returns_helpful_error_for_nonexistent_symbol()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "IEmailServiceTypo",
            IncludeSuggestions = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("IEmailService"); // Similar name suggestion
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Symbol with complex usage patterns")]
    [Trait("When", "Finding usages with pattern analysis")]
    [Trait("Then", "Returns usage patterns and anti-patterns")]
    public async Task Identifies_usage_patterns_and_anti_patterns()
    {
        // Arrange
        var command = new FindSymbolUsagesCommand
        {
            ProjectPath = _projectPath,
            SymbolName = "IEmailService",
            AnalyzeUsagePatterns = true,
            DetectAntiPatterns = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var usages = result.Value;
        
        // Should identify usage patterns
        usages.UsagePatterns.Should().NotBeEmpty();
        usages.UsagePatterns.Should().Contain(p => p.PatternName == "Dependency Injection");
        usages.UsagePatterns.Should().Contain(p => p.PatternName == "Service Layer");
        
        // Should detect any anti-patterns
        usages.AntiPatterns.Should().NotBeNull();
        
        // Should provide best practices
        usages.BestPractices.Should().NotBeEmpty();
    }
}
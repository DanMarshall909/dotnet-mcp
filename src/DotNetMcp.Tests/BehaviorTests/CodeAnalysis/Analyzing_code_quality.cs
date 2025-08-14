using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.Extensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotNetMcp.Tests.BehaviorTests.CodeAnalysis;

public class Analyzing_code_quality : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly string _projectPath;

    public Analyzing_code_quality()
    {
        _projectPath = "/test/project";
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddCoreServices();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        SetupCodebaseWithQualityIssues();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private void SetupCodebaseWithQualityIssues()
    {
        // God class with multiple responsibilities
        _fileSystem.AddFile($"{_projectPath}/Services/UserManagerService.cs", new MockFileData(@"
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace TestProject.Services
{
    // This class violates Single Responsibility Principle
    public class UserManagerService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly List<string> _cache = new List<string>();
        
        // User management
        public User CreateUser(string name, string email)
        {
            if (name == null || email == null) return null;
            var user = new User { Name = name, Email = email };
            SaveUserToDatabase(user);
            SendWelcomeEmail(user);
            LogUserCreation(user);
            UpdateStatistics();
            return user;
        }
        
        // Database operations
        public void SaveUserToDatabase(User user)
        {
            // Long method with complex logic
            var connection = GetDatabaseConnection();
            var sql = \"INSERT INTO Users (Name, Email, CreatedAt, UpdatedAt, IsActive, Department, Manager, PhoneNumber, Address, City, State, ZipCode, Country) VALUES (@name, @email, @created, @updated, @active, @dept, @manager, @phone, @address, @city, @state, @zip, @country)\";
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add("@name", user.Name);
            command.Parameters.Add("@email", user.Email);
            command.Parameters.Add("@created", DateTime.Now);
            command.Parameters.Add("@updated", DateTime.Now);
            command.Parameters.Add("@active", true);
            command.Parameters.Add("@dept", user.Department ?? "");
            command.Parameters.Add("@manager", user.Manager ?? "");
            command.Parameters.Add("@phone", user.PhoneNumber ?? "");
            command.Parameters.Add("@address", user.Address ?? "");
            command.Parameters.Add("@city", user.City ?? "");
            command.Parameters.Add("@state", user.State ?? "");
            command.Parameters.Add("@zip", user.ZipCode ?? "");
            command.Parameters.Add("@country", user.Country ?? "");
            command.ExecuteNonQuery();
        }
        
        // Email operations
        public void SendWelcomeEmail(User user)
        {
            var emailBody = $\"Welcome {user.Name}! Your account has been created successfully.\";
            var request = new HttpRequestMessage(HttpMethod.Post, \"https://api.email.com/send\");
            request.Content = new StringContent($\"{{\\\"to\\\":\\\"{user.Email}\\\",\\\"subject\\\":\\\"Welcome\\\",\\\"body\\\":\\\"{emailBody}\\\"}}\", Encoding.UTF8, \"application/json\");
            _httpClient.Send(request);
        }
        
        // Logging operations
        public void LogUserCreation(User user)
        {
            Console.WriteLine($\"User created: {user.Name} - {user.Email}\");
            _cache.Add($\"{user.Name}:{user.Email}:{DateTime.Now}\");
        }
        
        // Statistics operations
        public void UpdateStatistics()
        {
            // Complex nested logic
            var totalUsers = GetTotalUserCount();
            if (totalUsers > 1000)
            {
                if (totalUsers > 5000)
                {
                    if (totalUsers > 10000)
                    {
                        Console.WriteLine(\"We have over 10k users!\");
                    }
                    else
                    {
                        Console.WriteLine(\"We have over 5k users!\");
                    }
                }
                else
                {
                    Console.WriteLine(\"We have over 1k users!\");
                }
            }
        }
        
        // Magic numbers and hardcoded values
        public bool IsValidEmail(string email)
        {
            return email.Length > 5 && email.Contains(\"@\") && email.Length < 100;
        }
        
        // Unused method
        private void UnusedMethod()
        {
            var x = 42;
            var y = x * 2;
        }
        
        // Poor naming
        public void DoStuff(object thing)
        {
            var temp = thing.ToString();
            var result = temp.Length;
        }
        
        private object GetDatabaseConnection() => null;
        private int GetTotalUserCount() => 1500;
    }
    
    public class User
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public string Manager { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }
    }
}"));

        // Well-structured code for comparison
        _fileSystem.AddFile($"{_projectPath}/Domain/Product.cs", new MockFileData(@"
namespace TestProject.Domain
{
    public class Product
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public decimal Price { get; private set; }
        
        public Product(string name, decimal price)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException(nameof(name));
            if (price < 0)
                throw new ArgumentException(nameof(price));
                
            Name = name;
            Price = price;
        }
        
        public void UpdatePrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException(nameof(newPrice));
                
            Price = newPrice;
        }
    }
}"));

        // Empty class
        _fileSystem.AddFile($"{_projectPath}/Services/EmptyService.cs", new MockFileData(@"
namespace TestProject.Services
{
    public class EmptyService
    {
        // This class is empty and should be flagged
    }
}"));

        // High complexity method
        _fileSystem.AddFile($"{_projectPath}/Utilities/ComplexCalculator.cs", new MockFileData(@"
namespace TestProject.Utilities
{
    public class ComplexCalculator
    {
        public double CalculateComplexFormula(int a, int b, int c, string operation)
        {
            if (operation == ""add"")
            {
                if (a > 0)
                {
                    if (b > 0)
                    {
                        if (c > 0)
                        {
                            return a + b + c + 10;
                        }
                        else if (c < 0)
                        {
                            return a + b - c + 5;
                        }
                        else
                        {
                            return a + b;
                        }
                    }
                    else if (b < 0)
                    {
                        if (c > 0)
                        {
                            return a - b + c;
                        }
                        else
                        {
                            return a - b - c;
                        }
                    }
                    else
                    {
                        return a + c;
                    }
                }
                else if (a < 0)
                {
                    if (b > 0 && c > 0)
                    {
                        return -a + b + c;
                    }
                    else
                    {
                        return a + b + c;
                    }
                }
                else
                {
                    return b + c;
                }
            }
            else if (operation == ""multiply"")
            {
                return a * b * c;
            }
            else
            {
                return 0;
            }
        }
    }
}"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Codebase with various quality issues")]
    [Trait("When", "Analyzing code quality")]
    [Trait("Then", "Identifies code smells and quality metrics")]
    public async Task Identifies_code_smells_and_quality_issues()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            IncludeCodeSmells = true,
            IncludeComplexityMetrics = true,
            IncludeDesignIssues = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should detect god class
        analysis.CodeSmells.Should().Contain(smell => 
            smell.SmellType == "God Class" && 
            smell.ClassName == "UserManagerService");
        
        // Should detect long methods
        analysis.CodeSmells.Should().Contain(smell => 
            smell.SmellType == "Long Method" && 
            smell.MethodName == "SaveUserToDatabase");
        
        // Should detect magic numbers
        analysis.CodeSmells.Should().Contain(smell => 
            smell.SmellType == "Magic Numbers" && 
            smell.Description.Contains("5") || smell.Description.Contains("100"));
        
        // Should detect unused code
        analysis.CodeSmells.Should().Contain(smell => 
            smell.SmellType == "Dead Code" && 
            smell.MethodName == "UnusedMethod");
        
        // Should detect poor naming
        analysis.CodeSmells.Should().Contain(smell => 
            smell.SmellType == "Poor Naming" && 
            smell.MethodName == "DoStuff");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Classes with varying complexity")]
    [Trait("When", "Analyzing complexity metrics")]
    [Trait("Then", "Returns cyclomatic complexity and maintainability scores")]
    public async Task Calculates_complexity_and_maintainability_metrics()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            IncludeComplexityMetrics = true,
            IncludeMaintainabilityIndex = true,
            ComplexityThreshold = 10
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should calculate complexity for complex method
        var complexMethod = analysis.ComplexityMetrics.FirstOrDefault(m => 
            m.MethodName == "CalculateComplexFormula");
        complexMethod.Should().NotBeNull();
        complexMethod.CyclomaticComplexity.Should().BeGreaterThan(10);
        complexMethod.IsComplexityViolation.Should().BeTrue();
        
        // Should show good maintainability for simple classes
        var productClass = analysis.ComplexityMetrics.FirstOrDefault(m => 
            m.ClassName == "Product");
        productClass.Should().NotBeNull();
        productClass.MaintainabilityIndex.Should().BeGreaterThan(70);
        
        // Should flag god class with poor maintainability
        var godClass = analysis.ComplexityMetrics.FirstOrDefault(m => 
            m.ClassName == "UserManagerService");
        godClass.Should().NotBeNull();
        godClass.MaintainabilityIndex.Should().BeLessThan(50);
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Codebase with SOLID violations")]
    [Trait("When", "Analyzing design principles")]
    [Trait("Then", "Identifies SOLID principle violations")]
    public async Task Identifies_solid_principle_violations()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            IncludeDesignIssues = true,
            AnalyzeSolidPrinciples = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should detect Single Responsibility Principle violation
        analysis.SolidViolations.Should().Contain(violation => 
            violation.Principle == "Single Responsibility Principle" && 
            violation.ClassName == "UserManagerService");
        
        // Should provide specific recommendations
        var srpViolation = analysis.SolidViolations.FirstOrDefault(v => 
            v.Principle == "Single Responsibility Principle");
        srpViolation.Should().NotBeNull();
        srpViolation.Recommendation.Should().Contain("separate");
        srpViolation.Severity.Should().Be("High");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Project with test coverage gaps")]
    [Trait("When", "Analyzing test coverage and quality")]
    [Trait("Then", "Identifies untested code and test quality issues")]
    public async Task Analyzes_test_coverage_and_identifies_gaps()
    {
        // Setup test file
        _fileSystem.AddFile($"{_projectPath}/Tests/ProductTests.cs", new MockFileData(@"
using Xunit;

namespace TestProject.Tests
{
    public class ProductTests
    {
        [Fact]
        public void Constructor_ValidInput_CreatesProduct()
        {
            var product = new Product(""Test"", 10.0m);
            Assert.Equal(""Test"", product.Name);
        }
    }
}"));

        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            IncludeTestCoverage = true,
            AnalyzeTestQuality = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should identify untested classes
        analysis.TestCoverage.UntestedClasses.Should().Contain("UserManagerService");
        analysis.TestCoverage.UntestedClasses.Should().Contain("ComplexCalculator");
        
        // Should show partial coverage for Product
        var productCoverage = analysis.TestCoverage.ClassCoverage.FirstOrDefault(c => 
            c.ClassName == "Product");
        productCoverage.Should().NotBeNull();
        productCoverage.TestedMethods.Should().BeGreaterThan(0);
        productCoverage.UntestedMethods.Should().BeGreaterThan(0);
        
        // Should recommend additional tests
        analysis.TestRecommendations.Should().NotBeEmpty();
        analysis.TestRecommendations.Should().Contain(r => 
            r.Priority == "High" && 
            r.ClassName == "UserManagerService");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Code quality analysis with custom rules")]
    [Trait("When", "Applying custom quality rules")]
    [Trait("Then", "Evaluates code against custom criteria")]
    public async Task Applies_custom_quality_rules_and_standards()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            CustomRules = new[]
            {
                new QualityRule { Name = "NoPublicFields", Severity = "Error" },
                new QualityRule { Name = "RequireXmlDocumentation", Severity = "Warning" },
                new QualityRule { Name = "MaxMethodParameters", Threshold = 3, Severity = "Warning" }
            },
            ApplyTeamStandards = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should evaluate custom rules
        analysis.CustomRuleViolations.Should().NotBeEmpty();
        analysis.CustomRuleViolations.Should().Contain(v => 
            v.RuleName == "RequireXmlDocumentation");
        
        // Should flag methods with too many parameters
        analysis.CustomRuleViolations.Should().Contain(v => 
            v.RuleName == "MaxMethodParameters" && 
            v.MethodName == "CalculateComplexFormula");
        
        // Should provide rule-specific recommendations
        var docViolation = analysis.CustomRuleViolations.FirstOrDefault(v => 
            v.RuleName == "RequireXmlDocumentation");
        docViolation.Should().NotBeNull();
        docViolation.Recommendation.Should().Contain("documentation");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Quality analysis with token optimization")]
    [Trait("When", "Analyzing large codebase")]
    [Trait("Then", "Returns prioritized quality issues within token budget")]
    public async Task Returns_prioritized_quality_summary_for_token_efficiency()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            MaxTokens = 600,
            OptimizeForTokens = true,
            PrioritizeByImpact = true,
            FocusOnCriticalIssues = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should be within token budget
        analysis.EstimatedTokens.Should().BeLessOrEqualTo(600);
        
        // Should prioritize critical issues
        analysis.Summary.CriticalIssues.Should().NotBeEmpty();
        analysis.Summary.CriticalIssues.Should().HaveCountLessOrEqualTo(5);
        
        // Should focus on highest impact items
        analysis.Summary.TopPriorities.Should().NotBeEmpty();
        analysis.Summary.TopPriorities.Should().AllSatisfy(priority => 
            priority.Impact.Should().BeOneOf("High", "Critical"));
        
        // Should provide actionable recommendations
        analysis.Summary.QuickWins.Should().NotBeEmpty();
        analysis.Summary.QuickWins.Should().AllSatisfy(win => 
            win.Effort.Should().BeOneOf("Low", "Medium"));
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Quality analysis request")]
    [Trait("When", "Generating improvement suggestions")]
    [Trait("Then", "Provides concrete refactoring recommendations")]
    public async Task Provides_concrete_refactoring_recommendations()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = _projectPath,
            IncludeRefactoringRecommendations = true,
            ProvideCodeExamples = true
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Should provide refactoring recommendations
        analysis.RefactoringRecommendations.Should().NotBeEmpty();
        
        // Should recommend extracting services from god class
        var extractServiceRec = analysis.RefactoringRecommendations.FirstOrDefault(r => 
            r.RecommendationType == "Extract Service" && 
            r.TargetClass == "UserManagerService");
        extractServiceRec.Should().NotBeNull();
        extractServiceRec.Description.Should().Contain("EmailService");
        extractServiceRec.Description.Should().Contain("UserRepository");
        
        // Should recommend simplifying complex methods
        var simplifyMethodRec = analysis.RefactoringRecommendations.FirstOrDefault(r => 
            r.RecommendationType == "Simplify Method" && 
            r.TargetMethod == "CalculateComplexFormula");
        simplifyMethodRec.Should().NotBeNull();
        simplifyMethodRec.Effort.Should().Be("Medium");
        
        // Should provide code examples
        extractServiceRec.CodeExample.Should().NotBeNullOrEmpty();
        extractServiceRec.CodeExample.Should().Contain("public class EmailService");
    }

    [Fact]
    [Trait("Category", "BehaviorTest")]
    [Trait("Feature", "CodeAnalysis")]
    [Trait("Given", "Invalid project path")]
    [Trait("When", "Analyzing code quality")]
    [Trait("Then", "Returns helpful error message")]
    public async Task Returns_helpful_error_for_invalid_project()
    {
        // Arrange
        var command = new AnalyzeCodeQualityCommand
        {
            ProjectPath = "/nonexistent/project"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project path not found");
        result.Error.Should().Contain("Make sure the path exists");
    }
}
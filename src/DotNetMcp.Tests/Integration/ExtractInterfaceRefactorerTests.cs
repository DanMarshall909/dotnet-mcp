using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class ExtractInterfaceRefactorerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;

    public ExtractInterfaceRefactorerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "TestClass.cs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExtractInterfaceAsync_AllPublicMembers_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class UserService
    {
        public void CreateUser(string name)
        {
            Console.WriteLine($""Creating user: {name}"");
        }

        public string GetUser(int id)
        {
            return $""User {id}"";
        }

        public void DeleteUser(int id)
        {
            Console.WriteLine($""Deleting user: {id}"");
        }

        private void LogOperation(string operation)
        {
            Console.WriteLine($""Operation: {operation}"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(_testFilePath, "UserService", "IUserService", Array.Empty<string>());

        // Assert
        Assert.NotNull(result);
        Assert.Contains("IUserService", result.InterfaceContent);
        Assert.Contains("void CreateUser(string name);", result.InterfaceContent);
        Assert.Contains("string GetUser(int id);", result.InterfaceContent);
        Assert.Contains("void DeleteUser(int id);", result.InterfaceContent);
        Assert.DoesNotContain("LogOperation", result.InterfaceContent); // Private method should not be included

        Assert.Contains("IUserService", result.ModifiedClassContent);
        Assert.Contains("UserService : IUserService", result.ModifiedClassContent);

        Assert.Contains("CreateUser", result.ExtractedMembers);
        Assert.Contains("GetUser", result.ExtractedMembers);
        Assert.Contains("DeleteUser", result.ExtractedMembers);

        Assert.Equal(2, result.AffectedFiles.Length);
        Assert.Contains(_testFilePath, result.AffectedFiles);
        Assert.Contains("IUserService.cs", result.InterfaceFilePath);

        // Verify interface file was created
        Assert.True(File.Exists(result.InterfaceFilePath));
        var interfaceContent = await File.ReadAllTextAsync(result.InterfaceFilePath);
        Assert.Contains("public interface IUserService", interfaceContent);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_SpecificMembers_ExtractsOnlySpecified()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class DataProcessor
    {
        public void ProcessData(string data)
        {
            Console.WriteLine($""Processing: {data}"");
        }

        public string FormatData(string data)
        {
            return data.ToUpper();
        }

        public void SaveData(string data)
        {
            Console.WriteLine($""Saving: {data}"");
        }

        public void ValidateData(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException(""Data cannot be null or empty"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();
        var membersToExtract = new[] { "ProcessData", "FormatData" };

        // Act
        var result = await refactorer.ExtractInterfaceAsync(_testFilePath, "DataProcessor", "IDataProcessor", membersToExtract);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("IDataProcessor", result.InterfaceContent);
        Assert.Contains("void ProcessData(string data);", result.InterfaceContent);
        Assert.Contains("string FormatData(string data);", result.InterfaceContent);
        Assert.DoesNotContain("SaveData", result.InterfaceContent);
        Assert.DoesNotContain("ValidateData", result.InterfaceContent);

        Assert.Equal(2, result.ExtractedMembers.Length);
        Assert.Contains("ProcessData", result.ExtractedMembers);
        Assert.Contains("FormatData", result.ExtractedMembers);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_WithProperties_ExtractsProperties()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Configuration
    {
        public string ConnectionString { get; set; }
        public int TimeoutSeconds { get; set; }
        private string InternalKey { get; set; }

        public void Initialize()
        {
            Console.WriteLine(""Initializing configuration"");
        }

        public string GetSetting(string key)
        {
            return ""some value"";
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(_testFilePath, "Configuration", "IConfiguration", Array.Empty<string>());

        // Assert
        Assert.NotNull(result);
        Assert.Contains("IConfiguration", result.InterfaceContent);
        Assert.Contains("string ConnectionString { get; set; }", result.InterfaceContent);
        Assert.Contains("int TimeoutSeconds { get; set; }", result.InterfaceContent);
        Assert.Contains("void Initialize();", result.InterfaceContent);
        Assert.Contains("string GetSetting(string key);", result.InterfaceContent);
        Assert.DoesNotContain("InternalKey", result.InterfaceContent); // Private property should not be included

        Assert.Contains("ConnectionString", result.ExtractedMembers);
        Assert.Contains("TimeoutSeconds", result.ExtractedMembers);
        Assert.Contains("Initialize", result.ExtractedMembers);
        Assert.Contains("GetSetting", result.ExtractedMembers);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_ClassNotFound_ThrowsException()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class ExistingClass
    {
        public void DoSomething() { }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => refactorer.ExtractInterfaceAsync(_testFilePath, "NonExistentClass", "IInterface", Array.Empty<string>()));
    }

    [Fact]
    public async Task ExtractInterfaceAsync_WithNamespace_PreservesNamespace()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace MyCompany.Services
{
    public class EmailService
    {
        public void SendEmail(string to, string subject, string body)
        {
            Console.WriteLine($""Sending email to {to}: {subject}"");
        }

        public bool IsValidEmail(string email)
        {
            return email.Contains(""@"");
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(_testFilePath, "EmailService", "IEmailService", Array.Empty<string>());

        // Assert
        Assert.NotNull(result);
        Assert.Contains("namespace MyCompany.Services", result.InterfaceContent);
        Assert.Contains("public interface IEmailService", result.InterfaceContent);
        Assert.Contains("void SendEmail(string to, string subject, string body);", result.InterfaceContent);
        Assert.Contains("bool IsValidEmail(string email);", result.InterfaceContent);

        Assert.Contains("EmailService : IEmailService", result.ModifiedClassContent);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_FileScopedNamespace_HandlesCorrectly()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace MyCompany.Services;

public class OrderService
{
    public void CreateOrder(int customerId, decimal amount)
    {
        Console.WriteLine($""Creating order for customer {customerId}: ${amount}"");
    }

    public void CancelOrder(int orderId)
    {
        Console.WriteLine($""Cancelling order {orderId}"");
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(_testFilePath, "OrderService", "IOrderService", Array.Empty<string>());

        // Assert
        Assert.NotNull(result);
        Assert.Contains("namespace MyCompany.Services;", result.InterfaceContent);
        Assert.Contains("public interface IOrderService", result.InterfaceContent);
        Assert.Contains("void CreateOrder(int customerId, decimal amount);", result.InterfaceContent);
        Assert.Contains("void CancelOrder(int orderId);", result.InterfaceContent);

        Assert.Contains("OrderService : IOrderService", result.ModifiedClassContent);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_ClassWithExistingInterfaces_AddsToBaseList()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public interface IExisting
    {
        void ExistingMethod();
    }

    public class MultiService : IExisting
    {
        public void ExistingMethod()
        {
            Console.WriteLine(""Existing implementation"");
        }

        public void NewMethod()
        {
            Console.WriteLine(""New method"");
        }

        public string GetData()
        {
            return ""data"";
        }
    }
}";

        await File.WriteAllTextAsync(_testFilePath, sourceCode);
        var refactorer = new ExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(_testFilePath, "MultiService", "IMultiService", Array.Empty<string>());

        // Assert
        Assert.NotNull(result);
        Assert.Contains("MultiService : IExisting, IMultiService", result.ModifiedClassContent);
        Assert.Contains("void NewMethod();", result.InterfaceContent);
        Assert.Contains("string GetData();", result.InterfaceContent);
        Assert.Contains("void ExistingMethod();", result.InterfaceContent); // Should include all public methods
    }
}
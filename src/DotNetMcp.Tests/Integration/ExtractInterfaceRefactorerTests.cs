using DotNetMcp.Core.Refactoring;

namespace DotNetMcp.Tests.Integration;

public class ExtractInterfaceRefactorerTests
{
    [Fact]
    public async Task ExtractInterfaceAsync_AllPublicMembers_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public int Subtract(int a, int b)
        {
            return a - b;
        }

        private int MultiplyInternal(int a, int b)
        {
            return a * b;
        }

        public string Name { get; set; }
    }
}";

        var refactorer = new SimpleExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(sourceCode, "Calculator", "ICalculator");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ICalculator", result.InterfaceName);
        Assert.Contains("ICalculator", result.ModifiedCode);
        Assert.Contains("Add", result.ExtractedMembers);
        Assert.Contains("Subtract", result.ExtractedMembers);
        Assert.Contains("Name", result.ExtractedMembers);
        Assert.DoesNotContain("MultiplyInternal", result.ExtractedMembers);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_SpecificMembers_ExtractsOnlySpecified()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class DataService
    {
        public string GetData()
        {
            return ""Data"";
        }

        public void SaveData(string data)
        {
            // Save implementation
        }

        public void DeleteData()
        {
            // Delete implementation
        }

        public string ConnectionString { get; set; }
    }
}";

        var refactorer = new SimpleExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(sourceCode, "DataService", "IDataService", new[] { "GetData", "SaveData" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("IDataService", result.InterfaceName);
        Assert.Contains("GetData", result.ExtractedMembers);
        Assert.Contains("SaveData", result.ExtractedMembers);
        Assert.DoesNotContain("DeleteData", result.ExtractedMembers);
        Assert.DoesNotContain("ConnectionString", result.ExtractedMembers);
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
            // Send email implementation
        }

        public bool IsValidEmail(string email)
        {
            return email.Contains(""@"");
        }
    }
}";

        var refactorer = new SimpleExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(sourceCode, "EmailService", "IEmailService");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("IEmailService", result.ExtractedInterface);
        Assert.Contains("SendEmail", result.ExtractedMembers);
        Assert.Contains("IsValidEmail", result.ExtractedMembers);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_ClassWithExistingInterfaces_AddsToBaseList()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class FileManager : IDisposable
    {
        public void OpenFile(string path)
        {
            // Open file implementation
        }

        public void CloseFile()
        {
            // Close file implementation
        }

        public void Dispose()
        {
            // Dispose implementation
        }
    }
}";

        var refactorer = new SimpleExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(sourceCode, "FileManager", "IFileManager", new[] { "OpenFile", "CloseFile" });

        // Assert
        Assert.NotNull(result);
        Assert.Contains("IFileManager", result.ModifiedCode);
        Assert.Contains("IDisposable", result.ModifiedCode);
        Assert.Contains("OpenFile", result.ExtractedMembers);
        Assert.Contains("CloseFile", result.ExtractedMembers);
        Assert.DoesNotContain("Dispose", result.ExtractedMembers);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_FileScopedNamespace_HandlesCorrectly()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace;

public class Logger
{
    public void LogInfo(string message)
    {
        Console.WriteLine($""INFO: {message}"");
    }

    public void LogError(string message)
    {
        Console.WriteLine($""ERROR: {message}"");
    }
}";

        var refactorer = new SimpleExtractInterfaceRefactorer();

        // Act
        var result = await refactorer.ExtractInterfaceAsync(sourceCode, "Logger", "ILogger");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ILogger", result.ExtractedInterface);
        Assert.Contains("LogInfo", result.ExtractedMembers);
        Assert.Contains("LogError", result.ExtractedMembers);
    }

    [Fact]
    public async Task ExtractInterfaceAsync_NonExistentClass_ThrowsException()
    {
        // Arrange
        var sourceCode = @"
using System;

namespace TestNamespace
{
    public class ExistingClass
    {
        public void DoSomething()
        {
            // Implementation
        }
    }
}";

        var refactorer = new SimpleExtractInterfaceRefactorer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => refactorer.ExtractInterfaceAsync(sourceCode, "NonExistentClass", "IInterface"));
    }
}
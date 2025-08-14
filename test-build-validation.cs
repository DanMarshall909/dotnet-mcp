using DotNetMcp.Core.Services;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("Testing BuildValidationService...");

// Test 1: No project files
var fileSystem1 = new MockFileSystem();
fileSystem1.AddDirectory("/empty/project");
var service1 = new BuildValidationService(fileSystem1, NullLogger<BuildValidationService>.Instance);
var result1 = await service1.ValidateBuildAsync("/empty/project");
Console.WriteLine($"Test 1 - No files: {result1.IsWarning} - {result1.Message}");

// Test 2: Solution file present
var fileSystem2 = new MockFileSystem();
fileSystem2.AddFile("/test/project/Test.sln", "solution content");
fileSystem2.AddFile("/test/project/src/Project.csproj", "project content");
var service2 = new BuildValidationService(fileSystem2, NullLogger<BuildValidationService>.Instance);
var result2 = await service2.ValidateBuildAsync("/test/project");
Console.WriteLine($"Test 2 - Solution file: {result2.IsSuccess} - {result2.Message}");

// Test 3: Project file only
var fileSystem3 = new MockFileSystem();
fileSystem3.AddFile("/test/project/Project.csproj", "project content");
var service3 = new BuildValidationService(fileSystem3, NullLogger<BuildValidationService>.Instance);
var result3 = await service3.ValidateBuildAsync("/test/project");
Console.WriteLine($"Test 3 - Project file: {result3.IsSuccess} - {result3.Message}");

Console.WriteLine("All tests completed!");
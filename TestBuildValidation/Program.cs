using DotNetMcp.Core.Features.CodeAnalysis;
using DotNetMcp.Core.Services;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("Testing build validation integration with MCP handlers...");

// Test with the current project
var fileSystem = new FileSystem();
var buildValidationService = new BuildValidationService(fileSystem, NullLogger<BuildValidationService>.Instance);

// Test FindSymbolHandler integration
var findSymbolHandler = new FindSymbolHandler(
    NullLogger<FindSymbolHandler>.Instance, 
    fileSystem, 
    buildValidationService);

var currentDir = "/home/dan/code/dotnet-mcp";
Console.WriteLine($"Testing FindSymbolHandler with build validation on: {currentDir}");

var findSymbolRequest = new FindSymbolCommand 
{ 
    ProjectPath = currentDir, 
    SymbolName = "TestSymbol",
    SymbolType = SymbolType.Class,
    IncludeImplementations = false
};

var result = await findSymbolHandler.Handle(findSymbolRequest, CancellationToken.None);

Console.WriteLine($"FindSymbolHandler Result: Success={result.IsSuccess}");
Console.WriteLine($"Error Message: {result.Error}");

// Test with a project that should build successfully (core only)
Console.WriteLine("\nTesting with Core project only (should succeed)...");
var coreProjectPath = "/home/dan/code/dotnet-mcp/src/DotNetMcp.Core";
var coreRequest = new FindSymbolCommand 
{ 
    ProjectPath = coreProjectPath, 
    SymbolName = "BuildValidationService",
    SymbolType = SymbolType.Class,
    IncludeImplementations = false
};

var coreResult = await findSymbolHandler.Handle(coreRequest, CancellationToken.None);
Console.WriteLine($"Core Project Result: Success={coreResult.IsSuccess}");
if (!coreResult.IsSuccess)
{
    Console.WriteLine($"Core Project Error: {coreResult.Error}");
}
else
{
    Console.WriteLine($"Found {coreResult.Value.Symbols.Length} symbols");
}

Console.WriteLine("Test completed!");
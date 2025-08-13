using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions;
using DotNetMcp.Server;

var builder = Host.CreateApplicationBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add MCP server services
builder.Services.AddMcpServer()
    .WithStdIoTransport()
    .WithToolDiscovery();

// Register our refactoring tools
builder.Services.AddSingleton<ExtractMethodTool>();
builder.Services.AddSingleton<RenameSymbolTool>();
builder.Services.AddSingleton<ExtractInterfaceTool>();
builder.Services.AddSingleton<IntroduceVariableTool>();

var app = builder.Build();

// Run the MCP server
await app.RunAsync();

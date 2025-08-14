using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotNetMcp.Server;

var builder = Host.CreateApplicationBuilder(args);

// Add logging to stderr only
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Register our refactoring tools
builder.Services.AddSingleton<ExtractMethodTool>();
builder.Services.AddSingleton<ExtractMethodCompactTool>();
builder.Services.AddSingleton<RenameSymbolTool>();
builder.Services.AddSingleton<ExtractInterfaceTool>();
builder.Services.AddSingleton<IntroduceVariableTool>();

var app = builder.Build();

// MCP JSON-RPC over stdin/stdout
var mcpServer = new McpServer(app.Services);
await mcpServer.RunAsync();

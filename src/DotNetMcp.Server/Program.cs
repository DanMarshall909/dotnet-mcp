using DotNetMcp.Core.Extensions;
using DotNetMcp.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add logging to stderr only
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Add core services
builder.Services.AddCoreServices();

// Register the MCP server
builder.Services.AddScoped<McpServer>();

var app = builder.Build();

// Run the MCP server
var mcpServer = app.Services.GetRequiredService<McpServer>();
await mcpServer.RunAsync();
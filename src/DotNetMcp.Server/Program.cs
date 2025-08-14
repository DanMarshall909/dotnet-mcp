using DotNetMcp.Core.VSA;
using DotNetMcp.Server.VSA;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add logging to stderr only
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Add Vertical Slice Architecture services
builder.Services.AddVerticalSliceArchitecture();

// Register the VSA MCP server
builder.Services.AddScoped<McpServerWithVSA>();

var app = builder.Build();

// Run the VSA MCP server
var mcpServer = app.Services.GetRequiredService<McpServerWithVSA>();
await mcpServer.RunAsync();
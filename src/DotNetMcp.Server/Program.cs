using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotNetMcp.Server;

var builder = Host.CreateApplicationBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register our refactoring tools
builder.Services.AddSingleton<ExtractMethodTool>();
builder.Services.AddSingleton<RenameSymbolTool>();
builder.Services.AddSingleton<ExtractInterfaceTool>();
builder.Services.AddSingleton<IntroduceVariableTool>();

var app = builder.Build();

Console.WriteLine("DotNet MCP Refactoring Server Started");
Console.WriteLine("Available tools: ExtractMethod, RenameSymbol, ExtractInterface, IntroduceVariable");

// For now, keep the application running
await app.RunAsync();

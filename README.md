# .NET MCP Server

A robust Model Context Protocol (MCP) server for .NET code analysis and refactoring with comprehensive auto-fix capabilities.

## 🚀 Features

- **10 MCP Tools** for comprehensive code analysis and refactoring
- **Auto-fix system** for common code issues (using statements, nullability, async methods)
- **Batch refactoring** with atomic operations and rollback support  
- **Fallback analysis strategies** - tools never completely fail
- **Solution-wide analysis** with dependency graph detection
- **Build validation** before Roslyn analysis
- **Structured error handling** with actionable suggestions

## 🛠️ Available Tools

1. `extract_method` - Extract code into new methods
2. `rename_symbol` - Rename symbols throughout the codebase  
3. `extract_interface` - Extract interfaces from classes
4. `find_symbol` - Find symbols with advanced filtering
5. `get_class_context` - Get comprehensive class context
6. `analyze_project_structure` - Analyze project architecture
7. `find_symbol_usages` - Find symbol usages with impact analysis
8. `analyze_solution` - Analyze solution structure and dependencies
9. `auto_fix` - Apply automatic fixes to common code issues
10. `batch_refactor` - Apply multiple refactoring operations in sequence

## 📦 Quick Start

1. Build the server:
   ```bash
   dotnet build src/DotNetMcp.Server/DotNetMcp.Server.csproj
   ```

2. Run the MCP server:
   ```bash
   ./release/DotNetMcp.Server
   ```

3. Configure your MCP client to use the server

## 🏗️ Architecture

- **Clean architecture** with MediatR and CQRS patterns
- **Fallback analysis strategies**: Semantic → Syntax → Text → Hybrid
- **Comprehensive error handling** with structured error types
- **Token optimization** for efficient LLM interactions

## 📖 Documentation

- [Release Notes](RELEASE_NOTES.md) - Latest features and improvements
- [Task Documentation](docs/tasks/) - Comprehensive implementation details
- [Build Instructions](docs/build.md) - Development setup

## 🎯 Production Ready

The MCP server is robust enough for production use with complex enterprise codebases. The fallback strategy system ensures users always get valuable results, even when preferred semantic analysis isn't possible.
<div align="center">

# lsp-use

An MCP interface on top of C# Language Server (what powers C# DevKit for VS Code)

[![NuGet: LspUse.Csharp.linux-x64](https://img.shields.io/nuget/v/LspUse.Csharp.linux-x64?label=LspUse.Csharp.linux-x64)](https://www.nuget.org/packages/LspUse.Csharp.linux-x64)
[![NuGet: LspUse.Csharp.win-x64](https://img.shields.io/nuget/v/LspUse.Csharp.win-x64?label=LspUse.Csharp.win-x64)](https://www.nuget.org/packages/LspUse.Csharp.win-x64)
[![NuGet: LspUse.Csharp.osx-arm64](https://img.shields.io/nuget/v/LspUse.Csharp.osx-arm64?label=LspUse.Csharp.osx-arm64)](https://www.nuget.org/packages/LspUse.Csharp.osx-arm64)

</div>

## Installation

Install the platform-specific package globally using dotnet tool:

```bash
# For Linux x64
dotnet tool install --global LspUse.Csharp.linux-x64

# For Windows x64
dotnet tool install --global LspUse.Csharp.win-x64

# For macOS ARM64
dotnet tool install --global LspUse.Csharp.osx-arm64
```

## Configuration

Add the following to your `.mcp.json` file to configure Claude Code to use the MCP server:

```json
{
  "mcpServers": {
    "csharp": {
      "command": "lsp-use",
      "args": []
    }
  }
}
```

We recommend using the "csharp" name for the MCP server as it provides the clearest indication of functionality.

## Available Tools

LspUse provides comprehensive C# code analysis tools through the Language Server Protocol:

### Navigation Tools

- **`find_references`**: Finds all references to a symbol in the codebase
- **`go_to_definition`**: Navigates to the definition location of a symbol
- **`go_to_type_definition`**: Navigates to the type definition of a symbol
- **`go_to_implementation`**: Finds concrete implementations of interfaces or abstract members

### Symbol Tools

- **`search_symbols`**: Searches for symbols across the entire workspace by name
- **`get_symbols`**: Extracts all symbols from a specific file

### Interactive Tools

- **`hover`**: Gets hover information for a symbol at a specific position
- **`completion`**: Gets code completion suggestions at a specific position

### Diagnostic Tools

- **`get_diagnostics`**: Retrieves diagnostic information (errors, warnings, etc.) for a file
- **`get_window_log_messages`**: Retrieves LSP server status messages and logs

### Code Modification Tools

- **`rename_symbol`**: Renames a symbol across the entire codebase using LSP rename functionality

## Workflows

### 🔍 Class Discovery & Understanding
```
search_symbols("ApplicationService") → get_symbols(ApplicationService.cs)
```
**Use case**: Understanding what a class does, its methods, dependencies  
**Value**: Full method signatures show parameters, return types, async patterns without opening files

### 🔗 Interface Analysis  
```
go_to_definition(IApplicationService) → find_references(IApplicationService location)
```
**Use case**: Understanding interface contracts and their implementations  
**Value**: Full line context shows class inheritance, dependency injection registrations

### 📊 Dependency Mapping
```
find_references(_rpc field) → go_to_type_definition(JsonRpc usage)
```
**Use case**: Understanding external dependencies and how they're used  
**Value**: Full context shows field types, see framework class inheritance

### 🧭 Architecture Navigation
```
search_symbols("DefinitionAsync") → go_to_definition(specific method) → find_references(that method)
```
**Use case**: Tracing how functionality flows through layers  
**Value**: Understand call chains and architectural boundaries

### 🔄 Pattern Analysis
```
search_symbols("Async") → get_symbols(multiple files)
```
**Use case**: Understanding async patterns, naming conventions  
**Value**: Identify consistency in API design and implementation patterns

### 🎯 Refactoring Planning
```
find_references(method/field) → go_to_definition(each usage context)
```
**Use case**: Impact analysis before making changes  
**Value**: Assess refactoring scope and identify breaking changes

### 🔬 Type Exploration (Framework/Library APIs)
```
go_to_type_definition(System.Console) → get_symbols(decompiled Console.cs)
```
**Use case**: Understanding available methods/properties on framework types  
**Value**: Complete API discovery without documentation lookup

### 🔧 Symbol Refactoring
```
find_references(symbol) → rename_symbol(filePath, line, character, newName)
```
**Use case**: Safely renaming symbols across the entire codebase  
**Value**: Automated refactoring with LSP-level accuracy and safety

## Features

- **Rich Context**: All tools include enriched text content showing full line context
- **LSP-Level Accuracy**: Uses the same language server that powers VS Code C# DevKit
- **Cross-File Operations**: Navigate and refactor across entire solutions
- **Framework Integration**: Access decompiled framework types and APIs
- **Atomic Operations**: Rename operations succeed or fail atomically
- **Real-time Diagnostics**: Get live error and warning information

## Requirements

- .NET 9.0 or later
- A C# solution or project to analyze
- Claude Code with MCP support

## License

[Add your license information here]
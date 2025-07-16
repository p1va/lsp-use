# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LspUse is a MCP (Model Context Protocol) server that exposes LSP (Language Server Protocol) functionality as tools for Claude Code. The MCP server allows Claude to directly query LSP operations for C# code analysis and manipulation.

## Key Commands

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Running Specific Test Project
```bash
dotnet test LspUse.TestHarness
```

### LSP Server Setup (via justfile)
```bash
# List available platforms
just list-platforms

# Download LSP server for specific platform
just download-lsp linux-x64

# Run LSP server
just run-lsp linux-x64

# Run LSP server with named pipes
just run-lsp-named-pipes linux-x64
```

## Architecture

The project is structured as a multi-layered architecture:

- **LspUse.LanguageServerClient**: Core LSP client implementation with `JsonRpcLspClient` that wraps StreamJsonRpc
- **LspUse.McpServer**: MCP server entry point that exposes LSP functionality as MCP tools
- **LspUse.Application**: Application service layer that orchestrates LSP operations and provides business logic
- **LspUse.TestHarness**: Integration tests for LSP operations
- **LspUse.Client.Tests**: Unit tests for the client library

### Key Components

1. **ILspClient Interface** (`/src/LspUse.LanguageServerClient/ILspClient.cs`): Defines typed async wrappers for LSP operations like definition, references, hover, completion, diagnostics, and rename
2. **JsonRpcLspClient** (`/src/LspUse.LanguageServerClient/JsonRpcLspClient.cs`): Main implementation that forwards calls through StreamJsonRpc to LSP server processes
3. **ApplicationService** (`/src/LspUse.Application/ApplicationService.cs`): Orchestrates LSP operations, manages file lifecycle, and provides text enrichment
4. **Notification Handlers** (`/src/LspUse.LanguageServerClient/Handlers/`): Handle LSP notifications (diagnostics, window, workspace events)
5. **Models** (`/src/LspUse.LanguageServerClient/Models/`): Strong-typed DTOs for LSP protocol messages
6. **MCP Tools** (`/src/LspUse.McpServer/Tools/`): Individual tools that expose LSP functionality via MCP

### LSP Integration Pattern

The project follows this flow:
1. Start LSP server process (Roslyn language server)
2. Establish JSON-RPC communication over stdio
3. Perform LSP handshake (initialize/initialized)
4. Open solution via Roslyn-specific `solution/open` notification
5. Execute LSP operations (definition, references, hover, completion, diagnostics, rename)

### Application Service Pattern

All MCP tools follow this pattern:
1. **File Lifecycle Management**: `ExecuteWithFileLifecycleAsync` opens files on LSP, executes operations, then closes files
2. **Position Conversion**: Editor positions (1-based) are converted to LSP positions (0-based) using `EditorPosition.ToZeroBased()`
3. **Text Enrichment**: `EnrichWithTextAsync` adds actual source code text to symbol locations
4. **Error Handling**: Comprehensive logging and graceful error handling throughout

## Important Implementation Details

- Uses custom JSON serialization with `AbsoluteUriJsonConverter` for proper URI handling
- Implements both push diagnostics (via notifications) and pull diagnostics (via requests)
- Supports Metadata-As-Source (MAS) file operations for external type definitions
- Tests require the Roslyn language server to be downloaded via justfile commands
- Test harness uses `TestResource` class for portable path resolution
- All projects target .NET 9.0 with nullable reference types enabled
- Uses xUnit for testing with custom test output helpers
- StreamJsonRpc used for JSON-RPC communication
- Integration tests spawn actual LSP server processes and require proper setup

## Available MCP Tools

All tools include enriched text content showing the full line(s) where symbols are located, trimmed of whitespace. This provides immediate context without requiring file reads.

### Navigation Tools

- **find_references**: Finds all references to a symbol in the codebase
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based)
  - Returns: Array of reference locations with file paths, line numbers, character positions, and full line text

- **go_to_definition**: Navigates to the definition location of a symbol in the codebase
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based)
  - Returns: Array of definition locations with file paths, line numbers, character positions, and full line text (may point to decompiled files or external assemblies)

- **go_to_type_definition**: Navigates to the type definition of a symbol
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based)
  - Returns: Array of type definition locations, useful for external framework types

- **go_to_implementation**: Finds concrete implementations of interfaces or abstract members
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based)
  - Returns: Array of implementation locations, useful for finding concrete classes

### Symbol Tools

- **search_symbols**: Searches for symbols across the entire workspace by name
  - Parameters: `query` (string) - Search query to find symbols (supports partial matching)
  - Returns: Array of matching symbols with names, kinds, containers, and location text

- **get_symbols**: Extracts all symbols from a specific file
  - Parameters: `filePath` (string) - Path to the file to extract symbols from
  - Returns: Array of document symbols including classes, methods, fields, properties with full declaration text

### Interactive Tools

- **hover**: Gets hover information for a symbol at a specific position
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based)
  - Returns: Rich information including type signatures, documentation, and other symbol details

- **completion**: Gets code completion suggestions at a specific position
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based)
  - Returns: List of possible completions including variables, methods, classes, and other language constructs

### Diagnostic Tools

- **get_document_diagnostics**: Retrieves diagnostic information (errors, warnings, etc.) for a file
  - Parameters: `filePath` (string) - Path of the file to get diagnostics for
  - Returns: Consolidated diagnostics sorted by severity (Error → Warning → Information → Hint) then by line number

- **get_window_log_messages**: Retrieves LSP server status messages and logs
  - Parameters: None
  - Returns: High level messages like 'opened solution in 3s' to check LSP status

### Code Modification Tools

- **rename_symbol**: Renames a symbol across the entire codebase using LSP rename functionality
  - Parameters: `filePath` (string), `line` (int, 1-based), `character` (int, 1-based), `newName` (string)
  - Returns: Success status, list of changed files, and summary statistics
  - Features:
    - Cross-file rename support
    - Atomic operations (all changes succeed or all fail)
    - Smart reverse-order edit application to handle multiple edits per line
    - Comprehensive error handling and validation

## LSP Protocol Implementation

### Supported LSP Methods

The codebase implements the following LSP methods:

- **Lifecycle**: `initialize`, `initialized`, `shutdown`, `exit`
- **Document Sync**: `textDocument/didOpen`, `textDocument/didClose`
- **Navigation**: `textDocument/definition`, `textDocument/typeDefinition`, `textDocument/implementation`, `textDocument/references`
- **Symbols**: `textDocument/documentSymbol`, `workspace/symbol`
- **Language Features**: `textDocument/hover`, `textDocument/completion`
- **Diagnostics**: `textDocument/diagnostic` (pull diagnostics)
- **Refactoring**: `textDocument/rename`, `textDocument/prepareRename`

### Roslyn-Specific Extensions

- **`solution/open`**: Opens a solution in the Roslyn language server
- **`workspace/projectInitializationComplete`**: Notification when workspace is ready
- **`window/_roslyn_showToast`**: Roslyn-specific toast notifications

### LSP Models Structure

Models are located in `/src/LspUse.LanguageServerClient/Models/` and include:

- **Primitives.cs**: Basic LSP types (`ZeroBasedPosition`, `Range`, `Location`)
- **TextDocumentIdentifiers.cs**: Document identification models
- **Reference.cs**: Reference operation models
- **Rename.cs**: Rename operation models (`RenameParams`, `WorkspaceEdit`, `DocumentChange`, `TextEdit`)
- **Completion.cs**: Completion operation models
- **Hover.cs**: Hover operation models
- **Diagnostic.cs**: Diagnostic models

### File Editing Implementation

The rename functionality uses `WorkspaceEditApplicator` (`/src/LspUse.Application/WorkspaceEditApplicator.cs`) which:

1. **Processes WorkspaceEdit**: Handles document changes across multiple files
2. **Reverse-order application**: Applies edits from end to start to avoid offset calculation issues
3. **Multi-line support**: Handles edits that span multiple lines
4. **Error handling**: Comprehensive validation and atomic operations
5. **Performance optimized**: Efficient string manipulation and file I/O

## Code Analysis Workflows

The MCP tools support powerful code analysis workflows. Here are proven patterns for efficient codebase exploration:

### 🔍 Class Discovery & Understanding
```
search_symbols("ApplicationService") → get_symbols(ApplicationService.cs)
```
**Use case**: Understanding what a class does, its methods, dependencies  
**Example result**: See full method signatures, field types, class inheritance
**Value**: Full method signatures show parameters, return types, async patterns without opening files

### 🔗 Interface Analysis  
```
go_to_definition(IApplicationService) → find_references(IApplicationService location)
```
**Use case**: Understanding interface contracts and their implementations  
**Example result**: See interface declaration, then all implementing classes and usage sites
**Value**: Full line context shows class inheritance, dependency injection registrations

### 📊 Dependency Mapping
```
find_references(_rpc field) → go_to_type_definition(JsonRpc usage)
```
**Use case**: Understanding external dependencies and how they're used  
**Example result**: See field declarations, then external framework class signatures
**Value**: Full context shows field types, see framework class inheritance

### 🧭 Architecture Navigation
```
search_symbols("DefinitionAsync") → go_to_definition(specific method) → find_references(that method)
```
**Use case**: Tracing how functionality flows through layers  
**Example result**: See method signatures across interfaces/implementations/tools
**Value**: Understand call chains and architectural boundaries

### 🔄 Pattern Analysis
```
search_symbols("Async") → get_symbols(multiple files)
```
**Use case**: Understanding async patterns, naming conventions  
**Example result**: Scan method signatures to understand parameter patterns
**Value**: Identify consistency in API design and implementation patterns

### 🎯 Refactoring Planning
```
find_references(method/field) → go_to_definition(each usage context)
```
**Use case**: Impact analysis before making changes  
**Example result**: See full context lines to understand usage patterns
**Value**: Assess refactoring scope and identify breaking changes

### 🔬 Type Exploration (Framework/Library APIs)
```
go_to_type_definition(System.Console) → get_symbols(decompiled Console.cs)
```
**Use case**: Understanding available methods/properties on framework types  
**Example result**: See all Console methods: `WriteLine()`, `ReadLine()`, `Clear()`, `Beep()`, etc.
**Value**: Complete API discovery without documentation lookup - see full method signatures

### 🆕 New Type Discovery (Framework APIs)
```
Add type to existing project file → Wait ~5 seconds → go_to_definition(type) → get_symbols(decompiled file)
```
**Use case**: Learning about framework types you haven't used before  
**Example result**: Complete Dictionary/Task APIs with all methods: `TryGetValue()`, `WhenAll()`, `FromResult<T>()`
**Value**: Instant access to complete framework APIs without documentation
**Timing note**: LSP needs time to index new types and download metadata - wait a few seconds before expecting results

### 🧩 Completion-Driven Development
```
Type partial code → completion(cursor position) → go_to_definition(suggested types) → get_symbols(type files)
```
**Use case**: Exploring APIs while writing new code  
**Example result**: IntelliSense-like experience with full type information
**Value**: Write code with confidence about available APIs

### 🔧 Symbol Refactoring
```
find_references(symbol) → rename_symbol(filePath, line, character, newName)
```
**Use case**: Safely renaming symbols across the entire codebase
**Example result**: All references updated atomically across multiple files
**Value**: Automated refactoring with LSP-level accuracy and safety

### 💪 Advanced Refactoring Workflow
```
search_symbols("IApplicationService") → find_references(interface_location) → rename_symbol(location, "ITestInterface")
```
**Use case**: Large-scale refactoring with comprehensive impact analysis
**Example result**: Renamed interface across 15+ files with atomic success/failure
**Value**: Claude can perform complex refactoring with full confidence in correctness
**Key benefits**:
- **LSP-level accuracy**: Uses same rename logic as VS/VS Code
- **Atomic operations**: All changes succeed or all fail - no partial states
- **Cross-file safety**: Handles complex dependency chains automatically
- **Build verification**: Can verify changes compile before committing

### 💡 Pro Tips for Type Exploration
- **Add types to existing project files** rather than creating new files for better LSP recognition
- **Use fully qualified names** (System.Collections.Generic.Dictionary) for immediate LSP resolution
- **Wait 5-10 seconds** after adding new types before using go_to_definition
- **Explore constructor overloads** by looking at all the different constructors in get_symbols results
- **Check static methods** like Task.Run(), Task.WhenAll() for common patterns

## Development Workflows

### Adding New MCP Tools

1. **Create tool class** in `/src/LspUse.McpServer/Tools/`
2. **Add LSP models** if needed in `/src/LspUse.LanguageServerClient/Models/`
3. **Extend ILspClient** interface if new LSP methods are needed
4. **Implement in JsonRpcLspClient** following existing patterns
5. **Add ApplicationService method** if complex orchestration is needed
6. **Follow MCP attribute pattern**: Use `[McpServerToolType]` and `[McpServerTool]` attributes
7. **Test with build and basic functionality**

### MCP Development Workflow

When making changes to the codebase:
1. Make your code changes
2. Run `just remove` to uninstall the current MCP server
3. Run `just install` to install the updated MCP server
4. Restart Claude Code session to access the latest build
5. Test the changes using the MCP tools directly

### Code Style Guidelines

- Use records for DTOs and models
- Follow existing naming conventions (PascalCase for public members)
- Use nullable reference types consistently
- Add comprehensive logging for debugging
- Use dependency injection for services
- Follow async/await patterns consistently
- Use `required` keyword for mandatory properties

### Testing Guidelines

- Integration tests should use the actual LSP server
- Unit tests should mock external dependencies
- Test harness files are located in `/test/LspUse.TestHarness/TestSources`
- Use xUnit for all tests
- Include comprehensive error scenario testing

## Debugging and Logs

### MCP Server Logs
- **Location**: `$HOME/.local/share/lsp-use/`
- **Format**: `lsp-use-YYYY-MM-DD-HH-mm-ss.log`
- **Finding recent logs**: `ls -la $HOME/.local/share/lsp-use/ | tail -5`
- **Searching logs**: `grep -A 5 -B 5 "rename_symbol\|find_references\|go_to_definition" /path/to/log`
- **Contents**: Detailed LSP communication, tool invocations, and error traces

### LSP Server Diagnostics
- **Server capabilities**: Check initialization logs for supported features
- **Workspace state**: Monitor `workspace/projectInitializationComplete` notifications
- **File operations**: Track `textDocument/didOpen` and `textDocument/didClose` calls
- **Error patterns**: Look for JSON-RPC errors or LSP protocol violations

## External Resources

### LSP Specification
- **Official LSP Spec**: https://microsoft.github.io/language-server-protocol/
- **Roslyn LSP Implementation**: Microsoft.CodeAnalysis.LanguageServer
- **Key LSP Methods**: Documented in the codebase under `/src/LspUse.LanguageServerClient/`

### .NET Documentation
- **Local docs**: Available at `.external/dotnet-docs`
- **Framework APIs**: Accessible via LSP type definitions and MAS files
- **Roslyn APIs**: Available through LSP server integration

### Development Setup
- **LSP Server**: Downloaded via justfile, located in `/tmp/lsp-use/roslyn/`
- **Test Sources**: Accessed via `TestResource` class for portable path resolution
- **Build artifacts**: Standard .NET build output locations

### TestResource Class

The `TestResource` class provides strongly-typed access to test files and source files with automatic path resolution. It's designed to make tests portable across different environments by dynamically finding the repository root.

**Key Features:**
- **Automatic repository detection**: Uses `[CallerFilePath]` attribute to find .git directory or solution files
- **Strongly-typed properties**: Each test file and commonly used source file has a dedicated property
- **Cross-platform compatibility**: Uses `Path.Combine()` for proper path construction
- **Extensible design**: Easy to add new test resources or source files

**Usage Examples:**
```csharp
// Test resource files (returns Uri objects)
var testFileUri = TestResource.DiagnosticsErrorTest;
var jsonRpcUri = TestResource.JsonRpcTest;

// Source files (returns Uri objects)
var appServiceUri = TestResource.ApplicationService;
var findRefsUri = TestResource.FindReferencesResult;

// Repository paths (returns string paths)
var repoRoot = TestResource.RepositoryRoot;
var solutionFile = TestResource.SolutionFile;

// File paths for I/O operations (returns string paths)
var testFilePath = TestResource.Paths.DiagnosticsErrorTest;
var appServicePath = TestResource.Paths.ApplicationService;
```

**Available Properties:**
- **URI Properties**: `DiagnosticsErrorTest`, `JsonRpcTest`, `SystemConsoleTest`, `ApplicationService`, `FindReferencesResult`, `FileLoggerProvider`, `FindReferencesTool`, `IApplicationService`
- **Path Properties**: `RepositoryRoot`, `SolutionFile`
- **String Paths**: `TestResource.Paths.*` (mirrors all URI properties as string paths for file I/O)

## Future Enhancements

The architecture supports easy extension for:
- Additional LSP methods (code actions, formatting, etc.)
- Multi-language support (currently C#-focused)
- Advanced refactoring operations
- Real-time diagnostics streaming
- Workspace-wide operations
- Custom Roslyn analyzers integration
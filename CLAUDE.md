You are working on `lsp-use`: a C# codebase for a MCP (Model Context Protocol) server that exposes over stdio LSP (Language Server Protocol) functionality as tools for Claude Code. You are working on the source code itself and are also using a  in pre-release version of it available to you via the `mcp__csharp__*` tools.The MCP server allows Claude to directly query LSP operations for C# code analysis and manipulation.

# Key Commands

**dotnet**
- `dotnet restore` restores packages
- `dotnet build` builds the solution
- `dotnet test` runs all the tests
- `dotnet test LspUse.TestHarness` runs a specific test

**just**
- `just` to list all of the recipes available
- `just reinstall` to rebuild from source and install the tool. Use this when wanting to get access to the latest changes in the tools. ALWAYS ask the user to restart the session to make sure the new functionalities are available

# Git Workflow
All development should be done on feature branches rather than directly on main. Follow this workflow:
1) `git checkout -b features/name-of-the-feature`
2) Make your changes on the feature branch
3) Commit changes with descriptive messages
4) `git push -u origin features/name-of-the-feature`
5) create a PR with meaningful description
```bash
gh pr create --title "Feature: Name of the featurel" --body "$(cat <<'EOF'
## Summary
- Added new MCP tool for workspace operations
- Implemented comprehensive error handling
- Added unit tests for new functionality

## Test plan
- [ ] Run `dotnet build` to ensure compilation
- [ ] Run `dotnet test` to verify all tests pass
- [ ] Test MCP tool functionality manually
- [ ] Verify LSP integration works correctly

EOF
)"
```
6) `gh pr view --web` to open the PR in the browser

# Tool usage policy Addendum
- Use `mcp__csharp__search_symbols` as your first choice when searching for symbol names or portions of them for example`ApplicationService` or `Async`. Default to your usual search tool when no matches
- Use `mcp__csharp__get_symbols` as your first choice over the Read tool when you are interested in understanding the code structure over its raw content. This is much faster than reading entire files when exploring APIs.
- Use `mcp__csharp__get_diagnostics` on a file after making edits to it to retrieve any `Error`s that migth have been introduced with the change. Address `Error`s while keeping track of other severities to be highlighted in your final response.
- Use `mcp__csharp__get_diagnostics` on a file after making edits to it to retrieve any `Error`s that migth have been introduced with the change. Address `Error`s while keeping track of other severities to be highlighted in your final response.
- Use `mcp__csharp__rename_symbol` when you want to rename a symbol and all of its references over multiple files
- Use these tools for code navigation:
  - Use `mcp__csharp__find_references` for finding all of athe symbol references in the codebase
  - Use `mcp__csharp__go_to_definition` to retrieve the location of where the symbol is declared. This could be a decompiled file outside of the codebase for symbols imported via Nuget.
  - Use `mcp__csharp__go_to_type_definition` to retreive the location of where the symbol's type is declared
  - Use `mcp__csharp__go_to_implementation` to retrieve the locations of implementations of a given symbol e.g. all symbols implementing an interface
  - **IMPORTANT**: Position precision matters! Click EXACTLY on the symbol you want to navigate to. Clicking on parameters, string literals, or whitespace will take you to their type definitions (e.g., String class) instead of the intended symbol. For example, clicking on `RootCommand` in `new RootCommand("description")` should be on the actual word "RootCommand", not on the string parameter.
- When unsure which symbols (e.g. Properties, Methods) are available use a combination of Code Navigation tools to reach the definition e.g `mcp__csharp__go_to_definition` and then parse its symbols via `mcp__csharp__get_symbols` to retrieve available Methods, Properties, etc
- Use `mcp__microsoft-docs__microsoft_docs_search` for researching Microsoft/Azure documentation when working with MSBuild, NuGet packaging, .NET SDK features, or other Microsoft technologies. This tool provides authoritative answers from official documentation and can save significant time when troubleshooting build issues or implementing advanced MSBuild scenarios.

### Available MCP Tools

Available MCP tools are described in depth in @README.md

# Architecture

**Projects**
- **LspUse.LanguageServerClient**: Core LSP client implementation with `JsonRpcLspClient` that wraps StreamJsonRpc
- **LspUse.McpServer**: MCP server entry point that exposes LSP functionality as MCP tools
- **LspUse.Application**: Application service layer that orchestrates LSP operations and provides business logic
- **LspUse.TestHarness**: Integration tests for LSP operations (currently hangs)
- **LspUse.Client.Tests**: Unit tests for the client library

**Key files**
1. **MCP Tools** (`/src/LspUse.McpServer/Tools/`): Individual tools that expose LSP functionality via MCP
2. **ApplicationService** (`/src/LspUse.Application/ApplicationService.cs`): Orchestrates LSP operations, manages file lifecycle, and provides text enrichment. It's where the tool logic lives
3. **ILspClient Interface** (`/src/LspUse.LanguageServerClient/ILspClient.cs`): Defines typed async wrappers for LSP operations like definition, references, hover, completion, diagnostics, and rename
4. **JsonRpcLspClient** (`/src/LspUse.LanguageServerClient/JsonRpcLspClient.cs`): Main implementation that forwards calls through StreamJsonRpc to LSP server processes. This is where you can surface which LSP RPC is invoked.
5. **Notification Handlers** (`/src/LspUse.LanguageServerClient/Handlers/`): Handle LSP notifications (diagnostics, window, workspace events)
6. **Models** (`/src/LspUse.LanguageServerClient/Models/`): Strong-typed DTOs for LSP protocol messages

**MCP Server flow of request**
- **At startup:**
  1. start LSP server process (C# language server) and establish JSON-RPC communication over stdio
  2. Perform LSP handshake (`initialize`/`initialized`)
  3. Perform language specific `solution/open` or `project/open` notifications
- **When serving request**
  1. check whether workspace loaded notification was received, short-circuit otherwise
  2. Perform LSP operation(s) needed to fullfill request: (definition, references, hover, completion, diagnostics, rename)
  3. Map LSP client models DTOs to application results objects

**Patterns in ApplicationService**

All MCP tools follow this pattern:
1. **File Lifecycle Management**: `ExecuteWithFileLifecycleAsync` opens files on LSP, executes operations, then closes files
2. **Position Conversion**: Editor positions (1-based) are converted to LSP positions (0-based) using `EditorPosition.ToZeroBased()`
3. **Text Enrichment**: `EnrichWithTextAsync` adds actual source code text to symbol locations
4. **Error Handling**: Comprehensive logging and graceful error handling throughout

**Important Implementation Details**

- Uses custom JSON serialization with `AbsoluteUriJsonConverter` for proper URI handling
- Test harness uses `TestResource` class for portable path resolution
- All projects target .NET 9.0 with nullable reference types enabled
- Uses xUnit for testing with custom test output helpers
- StreamJsonRpc used for JSON-RPC communication
- Integration tests spawn actual LSP server processes and require proper setup

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

### Development Setup
- **LSP Server**: Downloaded via justfile, located in `/tmp/lsp-use/roslyn/`
- **Test Sources**: Accessed via `TestResource` class for portable path resolution
- **Build artifacts**: Standard .NET build output locations

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

- **LSP Specification**: https://microsoft.github.io/language-server-protocol/
- **Roslyn LSP Implementation**: Microsoft.CodeAnalysis.LanguageServer

### TestResource Class

The `TestResource` class provides strongly-typed access to test files and source files with automatic path resolution for portable tests across environments.
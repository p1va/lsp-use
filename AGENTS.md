# Agents & MCP Tools Reference

This repository is configured with an MCP (Language-Server-powered) tool-suite that exposes IDE-grade C# information to agents and humans alike.  
Below is a concise reference of what is available, plus typical scenarios for their usage.

## Available tools

Generic

* **shell** – run arbitrary shell commands inside the workspace.

C#-specific (Roslyn LSP)

* **csharp__OAI_CODEX_MCP__search_symbols** – search symbols across the whole workspace.
* **csharp__OAI_CODEX_MCP__get_symbols** – list every symbol in a given file.
* **csharp__OAI_CODEX_MCP__go_to_definition** – jump from a usage to where a symbol is declared.
* **csharp__OAI_CODEX_MCP__go_to_type_definition** – navigate to the declaration of a symbol’s *type*.
* **csharp__OAI_CODEX_MCP__go_to_implementation** – find concrete implementations of an interface / abstract member.
* **csharp__OAI_CODEX_MCP__find_references** – enumerate every reference to a symbol across the solution.
* **csharp__OAI_CODEX_MCP__rename_symbol** – refactor-safe rename across the code-base.
* **csharp__OAI_CODEX_MCP__completion** – code-completion items at a file/position.
* **csharp__OAI_CODEX_MCP__hover** – XML-doc / type information for a symbol.
* **csharp__OAI_CODEX_MCP__get_diagnostics** – compiler diagnostics (errors, warnings, hints) for a file.
* **csharp__OAI_CODEX_MCP__get_window_log_messages** – server-side status & log messages.

## Typical workflows

1. **API Discovery**  
   ```
   search_symbols("FooService") → get_symbols("src/MyProj/FooService.cs")
   ```

2. **Interface Understanding**  
   ```
   go_to_definition(IFoo) → go_to_implementation(IFoo location)
   ```

3. **Impact Analysis / Refactoring**  
   ```
   find_references(method) → rename_symbol(file, line, char, "NewName")
   ```

4. **Debugging & Code-health**  
   ```
   get_diagnostics(file) → hover(symbol) → go_to_definition(error location)
   ```

5. **Troubleshooting LSP**  
   ```
   get_window_log_messages()
   ```

## Why keep this document?

* Serves as a cheat-sheet for any future agent or contributor.  
* Encourages consistent, structured navigation instead of ad-hoc file dumping.  
* Reduces context/tokens when prompting LLMs by relying on structured responses.


<div align="center">

# lsp-use

An MCP interface to bring the structure of Language Servers to Coding Agents

![C#](https://img.shields.io/badge/c%23-%23239120.svg?logo=csharp&logoColor=white)
![Python](https://img.shields.io/badge/python-3670A0?&logo=python&logoColor=ffdd54)
![TypeScript](https://img.shields.io/badge/typescript-%23007ACC.svg?ogo=typescript&logoColor=white)

</div>

## Introduction

By giving Coding Agents direct access to the same Language Server that humans use through VS Code or other IDEs the codebase becomes more structured and easy to discover and navigate. This shorten the feedback loop, allows for codebase-aware generation and is an efficent use of the model's context.

<details>

<summary><b>More on why with this C# example</b></summary>

### Problem

Consider this scenario where `IsReady` disappears after a dependency update:

```csharp
using Your.Company.Lib;

var client = new YourCompanyClient();

if(client.IsReady) // Error after updating!
```

An LLM like `o3` encountering this would typically approach it by either
- attempting to search the local .nuget package folder on the look-out for source code
- write a temp console app that uses reflection to describe the type's symbols

```csharp
using System;
using Your.Company.Lib;

foreach (var p in typeof(YourCompanyClient).GetProperties())
    Console.WriteLine($"{p.Name} : {p.PropertyType}");
```

This isn't ideal for multiple reasons:
- longer feedback loops when developing
- more tokens needed so the model's context fills quicker and it costs more
- misses out on the structure of code that is so easy for humans to explore via IDEs

### With access to LSP

The LLM can now approach this by first retrieving the type's symbols similarly to what humans would do with autocomplete or source inspection.

For example invoking `mcp__csharp__get_symbols(YourCompanyClient.cs)` would return

```json
[
  {
    "name": "YourCompanyClient",
    "kind": "Class",
    "location": {
      "text": "public sealed class YourCompanyClient : IYourCompanyClient, IDisposable"
    }
  },
  {
    "name": "IsReadyAsync(CancellationToken ct = default)",
    "kind": "Method",
    "container": "YourCompanyClient",
    "location": {
      "text": "public Task IsReadyAsync(CancellationToken ct = default)"
    }
  },
  {
    "name": "DoWorkAsync(Request request, CancellationToken ct = default)",
    "kind": "Method",
    "container": "YourCompanyClient",
    "location": {
      "text": "public Task<Work> DoWorkAsync(Request request, CancellationToken ct = default)"
    }
  },
]
```

### Benefits

- **API Discovery**: No more guessing method signatures or writing reflection code
- **Accurate Refactoring**: LSP-powered rename and find-references across entire codebases
- **Rich Context**: Full type information, method signatures, documentation, and error diagnostics

</details>

## Installation

The tool spawn a Language Server process and then communicates over stdio with it according to the [Language Server Protocol](https://microsoft.github.io/language-server-protocol/). In order for the tool to work the Language Server for the language of choice needs to also be installed.

### Language Servers
The following Language Servers were tested however more should be compatible and can be added through configuration

<details>

<summary><b>✅ Csharp</b></summary>

Microsoft's C# language server can be downloaded from nuget. Alternatively the one bundled inside the VSCode C# DevKit extension can be used if the installation folder is known.

> 🚧 More instructions will be added soon. For now the recommended installation path is with the combined Nuget package below.

</details>

<details>

<summary><b>✅ Pyright</b></summary>

Install Pyright with `npm install -g pyright` then add the following to your project `pyproject.toml` to correctly point it in the direction of the virtual environment to be used.

```toml
[tool.pyright]
venvPath = "."
venv = ".venv"
```

> ⚠️ A sympthom of Pyright not being properly configured is the GetDiagnostics tool only reporting module import errors even when none in the IDE.

</details>

<details>

<summary><b>✅ Typescript</b></summary>

Install the Typescript Language Server with `npm install -g typescript typescript-language-server`

</details>

### Install the tool and the C# Language Server

Install the tool and the C# Langugage Server as one package.

| **OS**          | Install  	| Version  	|
|-----------------|-----------|-----------|
| **Linux**       | `dotnet tool install -g LspUse.Csharp.linux-x64`| [![NuGet: LspUse.Csharp.linux-x64](https://img.shields.io/nuget/v/LspUse.Csharp.linux-x64?label=LspUse.Csharp.linux-x64)](https://www.nuget.org/packages/LspUse.Csharp.linux-x64) |
| **Windows**	| `dotnet tool install -g LspUse.Csharp.win-x64`| [![NuGet: LspUse.Csharp.win-x64](https://img.shields.io/nuget/v/LspUse.Csharp.win-x64?label=LspUse.Csharp.win-x64)](https://www.nuget.org/packages/LspUse.Csharp.win-x64)	|
| **MacOS**      |`dotnet tool install -g LspUse.Csharp.osx-arm64`| [![NuGet: LspUse.Csharp.osx-arm64](https://img.shields.io/nuget/v/LspUse.Csharp.osx-arm64?label=LspUse.Csharp.osx-arm64)](https://www.nuget.org/packages/LspUse.Csharp.osx-arm64)|

### Install the tool with no LSP

Coming soon...

`dotnet tool install -g LspUse`

## Usage

- `lsp-use --help` for a list of the possible options and to see configuration paths
- `lsp-use --list-lsps` for a list of the configured language servers (some will be built-in while other taken by the optional config file)
- `lsp-use --version` to check version

### C#

- `lsp-use --lsp csharp --workspace /path/to/folder --sln /path/to/folder/MySolution.sln`
- `lsp-use --lsp csharp --workspace /path/to/folder --projects /path/to/folder/project1/Project1.csproj /path/to/folder/project2/Project2.csproj`

### Typescript

- `lsp-use --workspace /path/to/project --lsp typescript`

### Python

- `lsp-use --workspace /path/to/project --lsp pyright`

### Adding more Language Servers

To add more language servers, create a custom LSP configuration file at `~/.config/lsp-use/lsps.yaml`:

```bash
# Create config directory
mkdir -p ~/.config/lsp-use

# Copy example configuration
curl -o ~/.config/lsp-use/lsps.yaml https://raw.githubusercontent.com/p1va/lsp-use/main/examples/lsps.yaml
```

Via the configuration file (see [examples/lsps.yaml](examples/lsps.yaml)) is possible to:
- **Customize symbol filtering**: Control depth and symbol types shown
- **Add new file extensions**: Map custom file types to language servers
- **Tune diagnostics**: Configure error reporting strategies

Then run with
- `lsp-use --workspace /path/to/project --lsp your-custom-lsp`

## Coding Agents

<details>

<summary><b>Claude Code</b></summary>

Update your `.mcp.json` file with a `csharp` where the path and sln files match the ones of your repo

```json
{
  "mcpServers": {
    "csharp": {
      "command": "lsp-use",
      "args": [
        "--workspace",
        ".",
        "--sln",
        "solution.sln"
      ]
    }
  }
}

```

Update your `CLAUDE.md` with instructions on tool use recommending to prefer LSP-based discovery over traditional file read.

</details>

<details>

<summary><b>OpenAI Codex</b></summary>

Add or update your `$HOME/.codex/config.toml`. Doesn't seem to work at repo level yet. 

```toml
[mcp_servers.csharp]
command = "lsp-use"
args = ["--workspace=/path/to/repo", "--sln=/path/to/repo/solution.sln"]
```

Update your `AGENTS.md` with instructions on tool use like [here](AGENTS.md).

</details>

<details>

<summary><b>Copilot in VS Code</b></summary>

Add or update your `.vscode/mcp.toml` to include this `csharp` server and provide your own solution file name

```json
{
   "servers": {
     "csharp": {
       "type": "stdio",
       "command": "lsp-use",
       "args": [
         "--sln",
         "${workspaceFolder}/solution.sln",
         "--workspace",
         "${workspaceFolder}",
       ]
     }
   }
 }
 ```

</details>

## Available Tools

The server provides the following tools.

### Symbols

- **`mcp__csharp__search_symbols`**: Searches for symbols across the entire workspace e.g. `ApplicationService` or `Async`
- **`mcp__csharp__get_symbols`**: Parses all symbols in a file and returns them file

### Code Navigation & Edit

- **`mcp__csharp__find_references`**: Returns a list of all the references to a symbol across the codebase
- **`mcp__csharp__rename_symbol`**: Renames a symbol across the entire codebase
- **`mcp__csharp__go_to_definition`**: Returns the definition location of a symbol
- **`mcp__csharp__go_to_type_definition`**: Returns a list of type definition of a symbol
- **`mcp__csharp__go_to_implementation`**: Returns a list of implementations of interfaces or abstract members

### Diagnostic

- **`mcp__csharp__get_diagnostics`**: Returns a list of  diagnostics (errors, warnings, hints) for a file. These include codes, error messages, severity and link to Microsoft's code page. 


### Discovery

- **`mcp__csharp__hover`**: Gets XML documentation and description for a given symbol.
- **`mcp__csharp__completion`**: Returns a list of code completion suggestions at a specific file and their kinds (available properties, local variables, types, methods)

### Troubleshooting

- **`mcp__csharp__get_window_log_messages`**: Returns a list of messages from the server. Things like "solution successfully loaded"


## Workflows

The tools can be combined to create productive workflows or Claude commands.

### Class Discovery & Understanding
- **`mcp__csharp__search_symbols("ApplicationService")` → `mcp__csharp__get_symbols(ApplicationService.cs)`**
- **Use case**: Understanding what a class does, its methods, dependencies
- **Value**: Full method signatures show parameters, return types, async patterns without opening files

### Interface Analysis  
- **`mcp__csharp__go_to_definition(IApplicationService)` → `mcp__csharp__find_references(IApplicationService location)`**
- **Use case**: Understanding interface contracts and their implementations  
- **Value**: Full line context shows class inheritance, dependency injection registrations

### Dependency Mapping
- **`mcp__csharp__find_references(_rpc field)` → `mcp__csharp__go_to_type_definition(JsonRpc usage) → mcp__csharp__get_symbols(JsonRpc)`**
- **Use case**: Understanding external dependencies and how they're used  
- **Value**: Full context shows field types, available methods

### Architecture Navigation
- **`mcp__csharp__search_symbols("DefinitionAsync")` → `mcp__csharp__go_to_definition(specific method) → mcp__csharp__find_references(that method)`**
- **Use case**: Tracing how functionality flows through layers  
- **Value**: Understand call chains and architectural boundaries

### Pattern Analysis
- **`mcp__csharp__search_symbols("Async")` → `mcp__csharp__get_symbols(multiple files)`**
- **Use case**: Understanding async patterns, naming conventions  
- **Value**: Identify consistency in API design and implementation patterns

### Refactoring Planning
- **`mcp__csharp__find_references(method/field)` → `mcp__csharp__go_to_definition(each usage context)`**
- **Use case**: Impact analysis before making changes  
- **Value**: Assess refactoring scope and identify breaking changes

### Type Exploration (Framework/Library APIs)
- **`mcp__csharp__go_to_type_definition(System.Console)` → `mcp__csharp__get_symbols(decompiled Console.cs)`**
- **Use case**: Understanding available methods/properties on framework types  
- **Value**: Complete API discovery without documentation lookup

### Error Analysis & Debugging
- **`mcp__csharp__get_diagnostics(file.cs)` → `mcp__csharp__go_to_definition(error location)` → `mcp__csharp__hover(symbol`**
- **Use case**: Understanding compilation errors, warnings, and hints in context  
- **Value**: Provides error codes, messages, severity levels, and links to Microsoft documentation for quick resolution

### Symbol Refactoring
- **`mcp__csharp__find_references(symbol)` → `mcp__csharp__rename_symbol(filePath, line, character, newName)`**
- **Use case**: Safely renaming symbols across the entire codebase  
- **Value**: Automated refactoring with LSP-level accuracy and safety
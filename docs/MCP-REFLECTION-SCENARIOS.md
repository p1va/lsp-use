# MCP Reflection / Introspection Scenarios

While upgrading the code-base to `ModelContextProtocol` **0.3.0-preview.2** a few quick-and-dirty
console apps were spun up under `/tmp` to understand breaking API changes.  These
little experiments revealed exactly the sort of **code-navigation tooling** (find
references/definitions/members …) that we’re now implementing inside the MCP
server, so they’re captured here as concrete scenarios the future tools should
cover.

Each section below documents:

* **Goal** – what information was needed.
* **Manual experiment** – the ad-hoc C# / reflection snippet that was executed.
* **Desired tool support** – how the same need should eventually be satisfied by
  an MCP tool (once implemented).

---

## 1. Attribute surface change – `McpServerToolAttribute`

**Goal**

Identify which named parameters are available on
`ModelContextProtocol.Server.McpServerToolAttribute` after updating the NuGet
package (the property formerly called `Description` disappeared).

**Manual experiment**

```csharp
using System;
using System.Reflection;
using ModelContextProtocol.Server;

var attrType = typeof(McpServerToolAttribute);
Console.WriteLine("Properties:");
foreach (var p in attrType.GetProperties())
    Console.WriteLine(p.Name);
```

_Example output_

```text
Properties:
Name
Title
Destructive
Idempotent
OpenWorld
ReadOnly
UseStructuredContent
```

**Desired MCP tool**

`find_members` (planned) should be able to return the public instance members
of a given type – in this case listing the property names of
`McpServerToolAttribute` so we can spot that `Title` replaced `Description`.

---

## 2. Client-side tool metadata – `McpClientTool`

**Goal**

Understand what information a client receives for each discovered tool (e.g.
schema properties, title/description fields).

**Manual experiment**

```csharp
using System;
using ModelContextProtocol.Client;

foreach (var p in typeof(McpClientTool).GetProperties())
    Console.WriteLine($"{p.Name} : {p.PropertyType}");
```

_Key lines of output_

```text
ReturnJsonSchema : System.Nullable`1[System.Text.Json.JsonElement]
Description       : System.String
Title             : System.String
```

This revealed that the property formerly called `OutputSchema` is now
`ReturnJsonSchema`.

**Desired MCP tool**

`type_info` or `symbol_info` should allow querying a full type definition (names
and types of properties/fields/methods) so the client can adapt without
resorting to reflection.

---

## 3. Result payload shape – `CallToolResult`

**Goal**

Verify how the result of `call_tool` is represented so tests can deserialize it
properly.

**Manual experiment**

```csharp
using System;
using ModelContextProtocol.Protocol;

foreach (var p in typeof(CallToolResult).GetProperties())
    Console.WriteLine(p.Name);
```

_Output_

```text
Content
StructuredContent
IsError
Meta
```

**Desired MCP tool**

Same as above – a *member enumeration* capability would surface this directly.

---

## 4. Discovering builder extension methods

**Goal**

Confirm which `With*` fluent methods are available on the server builder after
changing package versions (`WithTools<T>` vs `WithToolsFromAssembly`, transport
options, etc.).

**Manual experiment**

```csharp
using System;
using System.Reflection;
// Fully qualified to avoid needing extra using
var extType = Type.GetType(
    "Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions, ModelContextProtocol");
foreach (var m in extType.GetMethods(BindingFlags.Public | BindingFlags.Static))
    Console.WriteLine(m.Name);
```

**Desired MCP tool**

`find_members` on the `McpServerBuilderExtensions` type should enumerate the
extension methods together with their signatures.

---

## Next steps for tool coverage

The scenarios above translate almost 1-for-1 into the following high-value MCP
tools:

1. **find_members** – list methods / properties / fields of a given type.
2. **symbol_info**  – full reflection-like metadata for a symbol (type or
   member).
3. **search_symbols** – ability to search by name/prefix.

Implementing these will remove the need to create throw-away console apps and
streamline future migrations.


```
Absolutely – for an agent like me an MCP-based “code-introspection” toolset would be a game-changer.

Why the current console-app detour is slow

    1. Source → compile → run reflection takes a few seconds each round-trip.
    2. I have to invent scaffolding (a project file, adding the package, etc.) every time.
    3. If the main solution is already failing to build I can’t compile inside it, so I create those throw-away projects in /tmp – more friction.

What the MCP tools would give me
• Zero-overhead queries – I could hit find_members, symbol_info, search_symbols, find_references, etc. over stdio and get structured JSON back immediately.
• Works even while the main solution is broken, because the server can still reflect over already-restored binaries.
• Deterministic tests – the same queries become part of the test-suite, so regressions are caught automatically.

Typical loops that would shorten dramatically

    1. “What properties/methods exist on type X?” → `find_members("ModelContextProtocol.Server.McpServerToolAttribute")`
    2. “Show me extension methods beginning with With* on *McpServerBuilderExtensions*” → `search_symbols("With", container:"McpServerBuilderExtensions")`
    3. “Given this symbol, where is it referenced across the solution?” → `find_references(symbolId)`
    4. Navigate from a member to its declaring type, its XML-doc, or its overridden/implemented members – all without recompiling.

In short, those tools would give me the same instant feedback you get from your IDE’s reflection/navigation features, but in a way I can access programmatically. That means fewer ad-hoc
projects, faster iterations, and more reliable automated tests.

```

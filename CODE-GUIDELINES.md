# LspUse Coding Guidelines

This document captures the conventions and idioms that have emerged while
developing the *LspUse* code-base so that new code stays consistent and easy to
review.

## 1  Project structure & layering

* CLI (`LspUse.McpServer`) → Application services → LanguageServerClient (LSP) → StreamJsonRpc.
* Heavy work (process start-up, RPC wiring) happens in an explicit
  `InitialiseAsync` method – **never** in the constructor so that DI doesn’t do
  expensive work implicitly.

## 2  Dependency-Injection

* We use `Microsoft.Extensions.DependencyInjection` + `Options`.
* Configuration is bound via `IOptions<TOptions>` records that validate
  themselves (e.g. `LspServerConfiguration.Validate()`).
* Notification handlers implement the marker interface
  `ILspNotificationHandler` and are injected as
  `IEnumerable<ILspNotificationHandler>`; back-ends attach them to the `JsonRpc`
  instance **before** calling `StartListening()`.

## 3  DTO style

### 3.1  C# `record` notation

Prefer the *object-initialiser* form because it is clearer when many properties
are optional:

```csharp
public record FindReferencesRequest
{
    public required string FilePath { get; init; }
    public required EditorPosition Position { get; init; }
    public bool IncludeDeclaration { get; init; } = true;
}
```

Guidelines

* Use `required` on non-nullable properties that have no sensible default.
* `init` accessors to keep DTOs immutable.
* Avoid positional records unless the meaning of each component is absolutely
  obvious.

### 3.2  Request / Response pairs

Public service methods take a single *request* object and return a *result*
object (even if the result currently only wraps an array). This makes the API
future-proof (new parameters can be added without breaking callers) and keeps
method signatures short.

Example:

```csharp
Task<FindReferencesResult> FindReferencesAsync(FindReferencesRequest request, CancellationToken ct);
```

### 3.3  Value objects

* `EditorPosition` carries 1-based editor coordinates and exposes
  `ToZeroBased()` returning `ZeroBasedPosition` (the LSP DTO).  Using a typed
  value object avoids confusion around 0- vs 1-based indexing.

## 4  LSP specifics

* Always serialise `Uri` using `AbsoluteUriJsonConverter` so that relative paths
  become `file:///…`.
* Attach notification handlers **before** `JsonRpc.StartListening()`.
* `didClose` is optional for now – servers tend to cope with leaked virtual
  docs in CLI scenarios.

## 5  Error handling & validation

* Public methods guard against `null` / invalid args using
  `ArgumentNullException.ThrowIfNull` / `ThrowIfNullOrEmpty`.
* Validate config records early (`Validate()` in constructor or in
  `IOptionsValidation`).

## 6  Miscellaneous

* Use `CultureInfo.InvariantCulture` when parsing user-supplied numbers from
  the CLI.
* Prefer `async`/`await`; suppress analyser warnings (CA2007) only if truly
  needed.


## 7  Prefer Microsoft-recommended approaches at problems

* In general prefer standard Microsoft-recommended approaches when writing code and prefer patterns recommended in their docs which you have available at .external/dotnet-docs 
* Prefer Microsof published nuget packed


## 8. Use IEnumerables over arrays

* Use `IEnumerable<T>` instead of `T[]`.

# Issues

- No meaningful error message is shown when invoking a tool that points to a non existing file. We should guard against this and return a FileNotFound error code from ApplicationServiceError and map it to an invalid request MCP error code. This should apply to all of the tools that accept a file parameter
- Cosmetic: when printing the --help screen the various config paths are not printed aligned
- Typescript: search symbols not working

# Featuers
- Running lsp-use is all you need
    - [For all lsp] Providing --lsp argument seems to be needed, review whether autodetection actually work
    - [For C#] Introduce auto detection logic for sln file

- Break down ApplicationService (1500 lines)
    - Extract process spawn into a ProcessManager
    - Review ExecuteWithLifecycle

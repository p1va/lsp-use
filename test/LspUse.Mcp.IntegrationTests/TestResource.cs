using System.Runtime.CompilerServices;

namespace LspUse.Mcp.IntegrationTests;

/// <summary>
/// Provides strongly-typed access to test resources and source files with automatic path resolution.
/// All paths are resolved relative to the repository root, making tests portable across different environments.
/// </summary>
public static class TestResource
{
    private static readonly Lazy<string> _repositoryRoot = new(() => FindRepositoryRoot());

    #region Test Resources

    /// <summary>
    /// Gets the URI to DiagnosticsErrorTest.cs test source file.
    /// </summary>
    public static Uri DiagnosticsErrorTest => new(GetTestSourcePath("DiagnosticsErrorTest.cs"));

    /// <summary>
    /// Gets the URI to JsonRpcTest.cs test source file.
    /// </summary>
    public static Uri JsonRpcTest => new(GetTestSourcePath("JsonRpcTest.cs"));

    /// <summary>
    /// Gets the URI to SystemConsoleTest.cs test source file.
    /// </summary>
    public static Uri SystemConsoleTest => new(GetTestSourcePath("SystemConsoleTest.cs"));

    #endregion

    #region Source Files

    /// <summary>
    /// Gets the URI to ApplicationService.cs source file.
    /// </summary>
    public static Uri ApplicationService => new(GetSourcePath("LspUse.Application/ApplicationService.cs"));

    /// <summary>
    /// Gets the URI to FindReferencesResult.cs source file.
    /// </summary>
    public static Uri FindReferencesResult => new(GetSourcePath("LspUse.Application/FindReferencesResult.cs"));

    /// <summary>
    /// Gets the URI to FileLoggerProvider.cs source file.
    /// </summary>
    public static Uri FileLoggerProvider => new(GetSourcePath("LspUse.McpServer/FileLoggerProvider.cs"));

    /// <summary>
    /// Gets the URI to FindReferencesTool.cs source file.
    /// </summary>
    public static Uri FindReferencesTool => new(GetSourcePath("LspUse.McpServer/Tools/FindReferencesTool.cs"));

    /// <summary>
    /// Gets the URI to IApplicationService.cs source file.
    /// </summary>
    public static Uri IApplicationService => new(GetSourcePath("LspUse.Application/IApplicationService.cs"));

    #endregion

    #region Repository Paths

    /// <summary>
    /// Gets the absolute path to the repository root directory.
    /// </summary>
    public static string RepositoryRoot => _repositoryRoot.Value;

    /// <summary>
    /// Gets the absolute path to the solution file.
    /// </summary>
    public static string SolutionFile => GetRepositoryPath("lsp-use.sln");

    /// <summary>
    /// Provides access to file paths as strings when needed for file operations.
    /// </summary>
    public static class Paths
    {
        /// <summary>
        /// Gets the absolute path to DiagnosticsErrorTest.cs test source file.
        /// </summary>
        public static string DiagnosticsErrorTest => GetTestSourcePath("DiagnosticsErrorTest.cs");

        /// <summary>
        /// Gets the absolute path to JsonRpcTest.cs test source file.
        /// </summary>
        public static string JsonRpcTest => GetTestSourcePath("JsonRpcTest.cs");

        /// <summary>
        /// Gets the absolute path to SystemConsoleTest.cs test source file.
        /// </summary>
        public static string SystemConsoleTest => GetTestSourcePath("SystemConsoleTest.cs");

        /// <summary>
        /// Gets the absolute path to ApplicationService.cs source file.
        /// </summary>
        public static string ApplicationService =>
            GetSourcePath("LspUse.Application/ApplicationService.cs");

        /// <summary>
        /// Gets the absolute path to FindReferencesResult.cs source file.
        /// </summary>
        public static string FindReferencesResult =>
            GetSourcePath("LspUse.Application/FindReferencesResult.cs");

        /// <summary>
        /// Gets the absolute path to FileLoggerProvider.cs source file.
        /// </summary>
        public static string FileLoggerProvider =>
            GetSourcePath("LspUse.McpServer/FileLoggerProvider.cs");

        /// <summary>
        /// Gets the absolute path to FindReferencesTool.cs source file.
        /// </summary>
        public static string FindReferencesTool =>
            GetSourcePath("LspUse.McpServer/Tools/FindReferencesTool.cs");

        /// <summary>
        /// Gets the absolute path to IApplicationService.cs source file.
        /// </summary>
        public static string IApplicationService =>
            GetSourcePath("LspUse.Application/IApplicationService.cs");
    }

    #endregion

    #region Path Resolution Methods

    /// <summary>
    /// Gets the absolute path to a test source file in the TestSources directory.
    /// </summary>
    /// <param name="fileName">The name of the test source file (e.g., "DiagnosticsErrorTest.cs")</param>
    /// <returns>The absolute path to the test source file</returns>
    private static string GetTestSourcePath(string fileName)
    {
        return Path.Combine(RepositoryRoot, "test", "LspUse.TestHarness", "TestSources", fileName);
    }

    /// <summary>
    /// Gets the absolute path to a source file in the src directory.
    /// </summary>
    /// <param name="relativePath">The relative path from the src directory (e.g., "LspUse.Application/ApplicationService.cs")</param>
    /// <returns>The absolute path to the source file</returns>
    private static string GetSourcePath(string relativePath)
    {
        return Path.Combine(RepositoryRoot, "src", relativePath);
    }

    /// <summary>
    /// Gets the absolute path to a file relative to the repository root.
    /// </summary>
    /// <param name="relativePath">The relative path from the repository root</param>
    /// <returns>The absolute path to the file</returns>
    private static string GetRepositoryPath(string relativePath)
    {
        return Path.Combine(RepositoryRoot, relativePath);
    }

    /// <summary>
    /// Finds the repository root by looking for the .git directory or solution file.
    /// </summary>
    /// <returns>The absolute path to the repository root</returns>
    private static string FindRepositoryRoot([CallerFilePath] string? callerFilePath = null)
    {
        if (callerFilePath == null)
        {
            throw new InvalidOperationException("Unable to determine caller file path for repository root detection");
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(callerFilePath)!);

        while (directory != null)
        {
            // Look for .git directory or solution file as indicators of repository root
            if (directory.GetDirectories(".git").Any() ||
                directory.GetFiles("*.sln").Any() ||
                directory.GetFiles("lsp-use.sln").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Unable to find repository root. Started search from: {callerFilePath}");
    }

    #endregion
}

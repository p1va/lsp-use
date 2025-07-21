using LspUse.Application;
using LspUse.Application.Configuration;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.McpServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

// Configure CLI arguments
var workspaceOption = new Option<DirectoryInfo?>("--workspace", "-w")
{
    Description = "Path to the workspace directory",
    Required = true
};

var solutionOption = new Option<FileInfo?>("--sln", "-s")
{
    Description = "Path to the solution file (.sln)"
};

var projectsOption = new Option<IEnumerable<FileInfo>>("--projects", "-p")
{
    Description = "Paths to project files (.csproj), can be specified multiple times or as comma-separated values",
    AllowMultipleArgumentsPerToken = true
};

var logLevelOption = new Option<LogLevel>("--log-level", "-l")
{
    Description = "Set the logging level (Trace, Debug, Information, Warning, Error, Critical)",
    DefaultValueFactory = _ => LogLevel.Information
};

var rootCommand = new RootCommand("An MCP server to bring the C# Language Server to LLMs")
{
    workspaceOption,
    solutionOption,
    projectsOption,
    logLevelOption
};

// Set the action for the root command
rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var workspace = parseResult.GetValue(workspaceOption)!;
    var solution = parseResult.GetValue(solutionOption);
    var projects = parseResult.GetValue(projectsOption);
    var logLevel = parseResult.GetValue(logLevelOption);
    
    // Validate that either solution or projects is provided
    if (solution == null && (projects == null || !projects.Any()))
    {
        Console.Error.WriteLine("Error: Either --sln or --projects must be specified.");
        return 1;
    }
    
    return await RunApplication(workspace, solution, projects, logLevel);
});

// Parse and invoke
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

static async Task<int> RunApplication(DirectoryInfo workspace, FileInfo? solution, IEnumerable<FileInfo>? projects, LogLevel logLevel)
{
    const string LanguageServerExecutable = "Microsoft.CodeAnalysis.LanguageServer";
    
    var lspDirectory = Path.Combine(AppContext.BaseDirectory, "lsp");
    var languageServerDllPath = Path.Combine(lspDirectory, LanguageServerExecutable);
    const string RoslynExtensionsLogsPath = "logs";
    
    // Get log level from environment variable if not specified
    var effectiveLogLevel = logLevel;
    if (Environment.GetEnvironmentVariable("LSP_USE_LOG_LEVEL") is var envLogLevel &&
        Enum.TryParse<LogLevel>(envLogLevel, true, out var parsedLogLevel))
    {
        effectiveLogLevel = parsedLogLevel;
    }
    
    var builder = Host.CreateApplicationBuilder();

    // Prepare file logging location  
    var customLogDir = Environment.GetEnvironmentVariable("LSP_USE_LOG_DIR");
    var localAppDataPath = customLogDir ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var logDirectory = Path.Combine(localAppDataPath, "lsp-use");
    Directory.CreateDirectory(logDirectory);
    var logFile = Path.Combine(logDirectory, $"lsp-use-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log");

    // Configure logging (we need to keep stdin clean so no Console.WriteLine)
    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(new FileLoggerProvider(logFile));
    builder.Logging.SetMinimumLevel(effectiveLogLevel);

    // Configuration injected in memory
    var config = new Dictionary<string, string?>
    {
        ["Lsp:Command"] = languageServerDllPath,
        ["Lsp:Arguments:0"] = "--logLevel=Information",
        ["Lsp:Arguments:1"] = $"--extensionLogDirectory={RoslynExtensionsLogsPath}",
        ["Lsp:Arguments:2"] = "--stdio",
        ["Lsp:WorkspacePath"] = workspace.FullName,
    };
    
    // Add solution path if provided
    if (solution != null)
    {
        config["Lsp:SolutionPath"] = solution.FullName;
    }
    
    builder.Configuration.AddInMemoryCollection(config);
    
    // Add project paths if specified
    if (projects != null && projects.Any())
    {
        var projectConfig = new Dictionary<string, string?>();
        int index = 0;
        foreach (var project in projects)
        {
            projectConfig[$"Lsp:ProjectPaths:{index}"] = project.FullName;
            index++;
        }
        builder.Configuration.AddInMemoryCollection(projectConfig);
    }

    builder.Services.Configure<LanguageServerProcessConfiguration>(
        builder.Configuration.GetSection("Lsp"));

    builder.Services.AddSingleton<ILspNotificationHandler, WindowNotificationHandler>()
        .AddSingleton<ILspNotificationHandler, DiagnosticsNotificationHandler>()
        .AddSingleton<ILspNotificationHandler, WorkspaceNotificationHandler>()
        .AddSingleton<ILspNotificationHandler, ClientCapabilityRegistrationHandler>();

    builder.Services.AddSingleton<IApplicationService, ApplicationService>();

    builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

    logger.LogInformation("[Program] Starting application with workspace: {Workspace}, solution: {Solution}, projects: {Projects}",
        workspace.FullName, solution?.FullName ?? "(none)", projects != null ? string.Join(", ", projects.Select(p => p.FullName)) : "(none)");

    try
    {
        logger.LogTrace("Resolving DI...");

        var service = host.Services.GetRequiredService<IApplicationService>();

        logger.LogTrace("Initializing...");

        // TODO: Move to StartAsync in the hosted service?
        await service.InitialiseAsync();

        logger.LogDebug("Initialized successfully. Starting host... (workspace may still be loading)");

        // We no longer wait for the workspace to finish loading at start-up. All
        // application service entry points now guard against a loading workspace
        // and return a dedicated error until it is ready.

        await host.RunAsync();

        logger.LogInformation("Shutting down");
        return 0;
    }
    catch (Exception e)
    {
        logger.LogError(e, "Failed to initialize");
        return 1;
    }
}

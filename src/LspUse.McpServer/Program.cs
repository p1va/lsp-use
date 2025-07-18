using LspUse.Application;
using LspUse.Application.Configuration;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.McpServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string LanguageServerExecutable = "Microsoft.CodeAnalysis.LanguageServer";

var lspDirectory = Path.Combine(AppContext.BaseDirectory, "lsp");

var languageServerDllPath = Path.Combine(lspDirectory, LanguageServerExecutable);

const string RoslynExtensionsLogsPath = "logs";

var builder = Host.CreateApplicationBuilder(args);

// Prepare file logging location
var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var logDirectory = Path.Combine(localAppDataPath, "lsp-use");
Directory.CreateDirectory(logDirectory);
var logFile = Path.Combine(logDirectory, $"lsp-use-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log");

// Configure logging (we need to keep stdin clean so no Console.WriteLine)
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new FileLoggerProvider(logFile));
builder.Logging.SetMinimumLevel(LogLevel.Trace); //TODO : Info

// Configuration injected in memory
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Lsp:Command"] = languageServerDllPath,
    ["Lsp:Arguments:0"] = "--logLevel=Information",
    ["Lsp:Arguments:1"] = $"--extensionLogDirectory={RoslynExtensionsLogsPath}",
    ["Lsp:Arguments:2"] = "--stdio"
});

// Configuration from command line
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--workspace"] = "Lsp:WorkspacePath",
    ["--sln"] = "Lsp:SolutionPath",
    ["--csproj"] = "Lsp:ProjectPaths"
});

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

logger.LogInformation("[Program] Starting application with {Args}", string.Join(" ", args));

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
}
catch (Exception e)
{
    logger.LogError(e, "Failed to initialize");

    throw;
}

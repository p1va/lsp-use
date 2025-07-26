using System.CommandLine;
using LspUse.Application;
using LspUse.Application.Configuration;
using LspUse.LanguageServerClient;
using LspUse.LanguageServerClient.Handlers;
using LspUse.McpServer;
using LspUse.McpServer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configure CLI arguments
var workspaceOption = new Option<DirectoryInfo?>("--workspace", "-w")
{
    Description = "Path to the workspace directory (defaults to current directory)",
};

var solutionOption = new Option<FileInfo?>("--sln")
{
    Description = "Path to the csharp solution file (.sln)",
};

var projectsOption = new Option<IEnumerable<FileInfo>>("--projects")
{
    Description =
        "Paths to csharp project files (.csproj), can be specified multiple times or as space-separated values",
    AllowMultipleArgumentsPerToken = true,
};

var lspOption = new Option<string?>("--lsp")
{
    Description = "LSP server to use (e.g., csharp, typescript, python). Overrides auto-detection.",
};

var commandOption = new Option<string?>("--command")
{
    Description = "Direct command to execute for the language server (overrides --language)",
};

var listLspsOption = new Option<bool>("--list-lsps")
{
    Description = "List all available LSP profiles and exit",
};

var logLevelOption = new Option<LogLevel>("--log-level", "-l")
{
    Description = "Set the logging level (Trace, Debug, Information, Warning, Error, Critical)",
    DefaultValueFactory = _ => LogLevel.Information,
};

var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config",
    "lsp-use",
    "lsps.yaml"
);

var logDirectory = Environment.GetEnvironmentVariable("LSP_USE_LOG_DIR") ??
                   Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                       "lsp-use"
                   );

var rootCommand = new CustomRootCommand("lsp-use",
    "An MCP server to bring Language Server functionality to coding agents"
)
{
    workspaceOption,
    solutionOption,
    projectsOption,
    lspOption,
    commandOption,
    listLspsOption,
    logLevelOption,
};

// Check for help argument and display colored help
if (args.Length > 0 &&
    (args[0] == "--help" || args[0] == "-h" || args[0] == "-?" || args[0] == "help"))
{
    DisplayColoredHelp(configPath, logDirectory);
    Console.WriteLine();
    // Let System.CommandLine handle the rest of the help display
}

// Set the action for the root command
rootCommand.SetAction(async (parseResult, cancellationToken) =>
    {
        var workspace = parseResult.GetValue(workspaceOption) ??
                        new DirectoryInfo(Directory.GetCurrentDirectory());
        var solution = parseResult.GetValue(solutionOption);
        var projects = parseResult.GetValue(projectsOption);
        var lsp = parseResult.GetValue(lspOption);
        var command = parseResult.GetValue(commandOption);
        var listLsps = parseResult.GetValue(listLspsOption);
        var logLevel = parseResult.GetValue(logLevelOption);

        // Handle --list-lsps flag
        if (listLsps)
            return await ListAvailableLsps();

        // Determine LSP server configuration using proper DI
        var tempServices = new ServiceCollection()
            .AddLogging(builder => builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Warning)
            )
            .AddSingleton<ILspConfigurationLoader, YamlLspConfigurationLoader>()
            .AddSingleton<LspConfigurationService>()
            .BuildServiceProvider();

        var configService = tempServices.GetRequiredService<LspConfigurationService>();
        var resolver = await configService.CreateResolverAsync();

        LspProfile? profile = null;
        string? resolvedLsp = null;

        // Priority: --command > --lsp > auto-detection > fallback validation
        if (!string.IsNullOrWhiteSpace(command))
        {
            // Direct command override
            profile = new LspProfile
            {
                Command = command,
            };
            resolvedLsp = "custom";
        }
        else if (!string.IsNullOrWhiteSpace(lsp))
        {
            // Explicit LSP selection
            profile = resolver.GetProfile(lsp);

            if (profile == null)
            {
                Console.Error.WriteLine(
                    $"Error: LSP '{lsp}' not found. Use --list-lsps to see available options."
                );

                return 1;
            }

            resolvedLsp = lsp;
        }
        else
        {
            // Auto-detection
            resolvedLsp = resolver.AutoDetectLsp(workspace.FullName);
            if (resolvedLsp != null)
                profile = resolver.GetProfile(resolvedLsp);
        }

        // Fallback validation for backward compatibility
        if (profile == null)
        {
            // Check if this looks like the old C# mode
            if (solution != null || (projects != null && projects.Any()))
            {
                profile = resolver.GetProfile("csharp");
                resolvedLsp = "csharp";
            }
        }

        if (profile == null)
        {
            Console.Error.WriteLine("❌ Error: No LSP server configured. Either:");
            Console.Error.WriteLine("  1️⃣  Use --lsp <name> to specify an LSP server");
            Console.Error.WriteLine("  2️⃣  Use --command <command> for a direct command");
            Console.Error.WriteLine("  3️⃣  Ensure workspace contains detectable files");
            Console.Error.WriteLine("  4️⃣  For C#: Use --sln or --projects (legacy mode)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("💡 Use --list-lsps to see available LSP profiles.");

            return 1;
        }

        return await RunApplication(workspace, solution, projects, profile, resolvedLsp!, logLevel);
    }
);

// Parse and invoke
var parseResult = rootCommand.Parse(args);

return await parseResult.InvokeAsync();

// Display colored help information
static void DisplayColoredHelp(string configPath, string logDirectory)
{
    // Simple ASCII box around lsp-use - clean white text
    Console.WriteLine("┌─────────────┐");
    Console.WriteLine("│   lsp-use   │");
    Console.WriteLine("└─────────────┘");
    Console.WriteLine();

    Console.WriteLine("Configuration:");

    // Current directory first
    Console.Write("  📁 Current directory:   ");
    WriteColored(Directory.GetCurrentDirectory(), ConsoleColor.Green, true);

    // Log files second  
    Console.Write("  📁 Log files location:  ");
    WriteColored(logDirectory, ConsoleColor.Yellow);
    Console.WriteLine("/");

    // Config file last with status check
    Console.Write("  📁 Custom LSP profiles:  ");
    WriteColored(configPath, ConsoleColor.Yellow);

    // Check if config file exists and show status
    if (File.Exists(configPath))
    {
        Console.Write(" ");
        WriteColored("✅", ConsoleColor.Green);
    }
    else
    {
        Console.Write(" ");
        WriteColored("⚠️ (not found)", ConsoleColor.Red);
    }

    Console.WriteLine();
    Console.WriteLine();

    Console.WriteLine("  💡 Example configuration:");
    Console.WriteLine("    lsps:");
    Console.WriteLine("      typescript:");
    Console.WriteLine("        command: \"typescript-language-server --stdio\"");
    Console.WriteLine("        extensions:");
    Console.WriteLine("          \".ts\": \"typescript\"");
    Console.WriteLine("          \".tsx\": \"typescriptreact\"");
    Console.WriteLine("        workspace_files: [\"package.json\", \"tsconfig.json\"]");
}

// Helper method for colored console output
static void WriteColored(string text, ConsoleColor color = ConsoleColor.Cyan, bool newLine = false)
{
    var original = Console.ForegroundColor;
    Console.ForegroundColor = color;
    if (newLine)
        Console.WriteLine(text);
    else
        Console.Write(text);
    Console.ForegroundColor = original;
}

static async Task<int> ListAvailableLsps()
{
    var tempServices = new ServiceCollection()
        .AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Warning)
        )
        .AddSingleton<ILspConfigurationLoader, YamlLspConfigurationLoader>()
        .AddSingleton<LspConfigurationService>()
        .BuildServiceProvider();

    var configService = tempServices.GetRequiredService<LspConfigurationService>();
    var resolver = await configService.CreateResolverAsync();

    Console.WriteLine("Available LSP profiles:");

    foreach (var lsp in resolver.GetAvailableLsps())
    {
        var profile = resolver.GetProfile(lsp);
        Console.Write("  ");
        WriteColored(lsp, ConsoleColor.Cyan);
        Console.Write(": ");

        var command = profile?.Command ?? "N/A";
        // Trim long commands for better readability
        if (command.Length > 60)
            command = "..." + command.Substring(command.Length - 57);
        WriteColored(command, ConsoleColor.Yellow, true);
    }

    return 0;
}

static string ResolveLanguageServerCommand(string command, string lspName)
{
    // If the command is just a filename (no path separators), check if it's an embedded LSP
    if (!command.Contains('/') && !command.Contains('\\'))
    {
        var lspDirectory = Path.Combine(AppContext.BaseDirectory, "lsp");
        var embeddedPath = Path.Combine(lspDirectory, command);

        if (File.Exists(embeddedPath))
            return embeddedPath;
    }

    // Return the command as-is (could be a full path or command in PATH)
    return command;
}

static async Task<int> RunApplication(DirectoryInfo workspace,
    FileInfo? csharpSolution,
    IEnumerable<FileInfo>? csharpProjects,
    LspProfile profile,
    string lspName,
    LogLevel logLevel)
{
    // Parse the LSP profile command into command and arguments
    var (command, arguments) = profile.GetCommandAndArgs();

    // For embedded LSPs (like C#), resolve relative paths to the embedded LSP directory
    var resolvedCommand = ResolveLanguageServerCommand(command, lspName);

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
    var localAppDataPath = customLogDir ??
                           Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData
                           );
    var logDirectory = Path.Combine(localAppDataPath, "lsp-use");
    Directory.CreateDirectory(logDirectory);

    // Include LSP name in log file for better organization
    var logFile = Path.Combine(logDirectory,
        $"lsp-use-{lspName}-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log"
    );

    // Configure logging (we need to keep stdin clean so no Console.WriteLine)
    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(new FileLoggerProvider(logFile));
    builder.Logging.SetMinimumLevel(effectiveLogLevel);

    // Configuration injected in memory from LSP profile
    var config = new Dictionary<string, string?>
    {
        ["Lsp:Command"] = resolvedCommand,
        ["Lsp:WorkingDirectory"] = workspace.FullName,
        ["Lsp:ProfileName"] = lspName,
    };

    // Add arguments from the language profile
    for (var i = 0; i < arguments.Length; i++)
        config[$"Lsp:Arguments:{i}"] = arguments[i];

    // Add environment variables from the language profile
    if (profile.Environment != null)
    {
        for (var i = 0; i < profile.Environment.Count; i++)
        {
            var kvp = profile.Environment.ElementAt(i);
            config[$"Lsp:Environment:{kvp.Key}"] = kvp.Value;
        }
    }

    // Add solution path if provided
    if (csharpSolution != null)
        config["Lsp:SolutionPath"] = csharpSolution.FullName;

    builder.Configuration.AddInMemoryCollection(config);

    // Add project paths if specified
    if (csharpProjects != null && csharpProjects.Any())
    {
        var projectConfig = new Dictionary<string, string?>();
        var index = 0;

        foreach (var project in csharpProjects)
        {
            projectConfig[$"Lsp:ProjectPaths:{index}"] = project.FullName;
            index++;
        }

        builder.Configuration.AddInMemoryCollection(projectConfig);
    }

    builder.Services.Configure<LanguageServerConfiguration>(
        builder.Configuration.GetSection("Lsp")
    );
    
    // Bind to both client level and application level configs even if they inherit from each others
    // Need to be improved
    builder.Services.Configure<LanguageServerProcessConfiguration>(
        builder.Configuration.GetSection("Lsp")
    );

    // Register LSP configuration services
    builder
        .Services.AddSingleton<ILspConfigurationLoader, YamlLspConfigurationLoader>()
        .AddSingleton<LspConfigurationService>()
        .AddSingleton<LanguageIdMapper>(provider =>
            {
                var configService = provider.GetRequiredService<LspConfigurationService>();
                var resolver = configService
                    .CreateResolverAsync()
                    .GetAwaiter()
                    .GetResult();
                var logger = provider.GetRequiredService<ILogger<LanguageIdMapper>>();

                return new LanguageIdMapper(resolver, logger);
            }
        );

    builder
        .Services
        .AddSingleton<IRpcLocalTarget, WindowNotificationHandler>()
        .AddSingleton<IRpcLocalTarget, DiagnosticsNotificationHandler>()
        .AddSingleton<IRpcLocalTarget, WorkspaceNotificationHandler>()
        .AddSingleton<IRpcLocalTarget, ClientCapabilityRegistrationHandler>()
        .AddSingleton<IRpcLocalTarget, DefaultNotificationHandler>()
        .AddSingleton<IRpcLocalTarget, DefaultRequestHandler>();

    builder.Services.AddSingleton<ILanguageServerProcess, LanguageServerProcess>();
    builder.Services.AddSingleton<ILanguageServerManager, LanguageServerManager>();
    builder.Services.AddSingleton<IApplicationService, ApplicationService>();

    builder
        .Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var host = builder.Build();

    var logger = host
        .Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger<Program>();

    logger.LogInformation(
        "Starting application with workspace: {Workspace}, solution: {Solution}, projects: {Projects}",
        workspace.FullName,
        csharpSolution?.FullName ?? "(none)",
        csharpProjects != null
            ? string.Join(", ", csharpProjects.Select(p => p.FullName))
            : "(none)"
    );

    try
    {
        logger.LogTrace("Resolving DI...");

        var service = host.Services.GetRequiredService<IApplicationService>();

        logger.LogTrace("Initializing...");

        // TODO: Move to StartAsync in the hosted service?
        await service.InitialiseAsync();

        logger.LogDebug(
            "Initialized successfully. Starting host... (workspace may still be loading)"
        );

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

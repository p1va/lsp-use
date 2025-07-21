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
    Description = "Path to the workspace directory (defaults to current directory)"
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

var languageOption = new Option<string?>("--language")
{
    Description = "Language server to use (e.g., csharp, typescript, python). Overrides auto-detection."
};

var commandOption = new Option<string?>("--command")
{
    Description = "Direct command to execute for the language server (overrides --language)"
};

var listLanguagesOption = new Option<bool>("--list-languages")
{
    Description = "List all available language profiles and exit"
};

var logLevelOption = new Option<LogLevel>("--log-level", "-l")
{
    Description = "Set the logging level (Trace, Debug, Information, Warning, Error, Critical)",
    DefaultValueFactory = _ => LogLevel.Information
};

var configPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config",
    "lsp-use",
    "languages.yaml");

var logDirectory = Environment.GetEnvironmentVariable("LSP_USE_LOG_DIR") ?? 
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lsp-use");

var rootCommand = new RootCommand($"""
An MCP server to bring Language Server functionality to LLMs

Configuration:
  Custom language profiles: {configPath}
  Log files location:       {logDirectory}/

  Example configuration:
    languages:
      typescript:
        command: "typescript-language-server --stdio"
        extensions: [".ts", ".tsx"]
        workspace_files: ["package.json", "tsconfig.json"]
""")
{
    workspaceOption,
    solutionOption,
    projectsOption,
    languageOption,
    commandOption,
    listLanguagesOption,
    logLevelOption
};

// Set the action for the root command
rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var workspace = parseResult.GetValue(workspaceOption) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
    var solution = parseResult.GetValue(solutionOption);
    var projects = parseResult.GetValue(projectsOption);
    var language = parseResult.GetValue(languageOption);
    var command = parseResult.GetValue(commandOption);
    var listLanguages = parseResult.GetValue(listLanguagesOption);
    var logLevel = parseResult.GetValue(logLevelOption);

    // Handle --list-languages flag
    if (listLanguages)
    {
        return await ListAvailableLanguages();
    }

    // Determine language server configuration using proper DI
    var tempServices = new ServiceCollection()
        .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
        .AddSingleton<ILanguageConfigurationLoader, YamlLanguageConfigurationLoader>()
        .AddSingleton<LanguageConfigurationService>()
        .BuildServiceProvider();

    var configService = tempServices.GetRequiredService<LanguageConfigurationService>();
    var resolver = await configService.CreateResolverAsync();

    LanguageProfile? profile = null;
    string? resolvedLanguage = null;

    // Priority: --command > --language > auto-detection > fallback validation
    if (!string.IsNullOrWhiteSpace(command))
    {
        // Direct command override
        profile = new LanguageProfile { Command = command };
        resolvedLanguage = "custom";
    }
    else if (!string.IsNullOrWhiteSpace(language))
    {
        // Explicit language selection
        profile = resolver.GetProfile(language);
        if (profile == null)
        {
            Console.Error.WriteLine($"Error: Language '{language}' not found. Use --list-languages to see available options.");
            return 1;
        }
        resolvedLanguage = language;
    }
    else
    {
        // Auto-detection
        resolvedLanguage = resolver.AutoDetectLanguage(workspace.FullName);
        if (resolvedLanguage != null)
        {
            profile = resolver.GetProfile(resolvedLanguage);
        }
    }

    // Fallback validation for backward compatibility
    if (profile == null)
    {
        // Check if this looks like the old C# mode
        if (solution != null || (projects != null && projects.Any()))
        {
            profile = resolver.GetProfile("csharp");
            resolvedLanguage = "csharp";
        }
    }

    if (profile == null)
    {
        Console.Error.WriteLine("Error: No language server configured. Either:");
        Console.Error.WriteLine("  1. Use --language <name> to specify a language");
        Console.Error.WriteLine("  2. Use --command <command> for a direct command");
        Console.Error.WriteLine("  3. Ensure workspace contains detectable files");
        Console.Error.WriteLine("  4. For C#: Use --sln or --projects (legacy mode)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Use --list-languages to see available language profiles.");
        return 1;
    }

    return await RunApplication(workspace, solution, projects, profile, resolvedLanguage!, logLevel);
});

// Parse and invoke
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

static async Task<int> ListAvailableLanguages()
{
    var tempServices = new ServiceCollection()
        .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
        .AddSingleton<ILanguageConfigurationLoader, YamlLanguageConfigurationLoader>()
        .AddSingleton<LanguageConfigurationService>()
        .BuildServiceProvider();

    var configService = tempServices.GetRequiredService<LanguageConfigurationService>();
    var resolver = await configService.CreateResolverAsync();
    
    Console.WriteLine("Available language profiles:");
    foreach (var language in resolver.GetAvailableLanguages())
    {
        var profile = resolver.GetProfile(language);
        Console.WriteLine($"  {language}: {profile?.Command}");
    }
    
    return 0;
}


static string ResolveLanguageServerCommand(string command, string languageName)
{
    // If the command is just a filename (no path separators), check if it's an embedded LSP
    if (!command.Contains('/') && !command.Contains('\\'))
    {
        var lspDirectory = Path.Combine(AppContext.BaseDirectory, "lsp");
        var embeddedPath = Path.Combine(lspDirectory, command);
        
        if (File.Exists(embeddedPath))
        {
            return embeddedPath;
        }
    }
    
    // Return the command as-is (could be a full path or command in PATH)
    return command;
}

static async Task<int> RunApplication(DirectoryInfo workspace, FileInfo? solution, IEnumerable<FileInfo>? projects, LanguageProfile profile, string languageName, LogLevel logLevel)
{
    // Parse the language profile command into command and arguments
    var (command, arguments) = profile.GetCommandAndArgs();

    // For embedded LSPs (like C#), resolve relative paths to the embedded LSP directory
    var resolvedCommand = ResolveLanguageServerCommand(command, languageName);

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

    // Configuration injected in memory from language profile
    var config = new Dictionary<string, string?>
    {
        ["Lsp:Command"] = resolvedCommand,
        ["Lsp:WorkspacePath"] = workspace.FullName,
    };

    // Add arguments from the language profile
    for (int i = 0; i < arguments.Length; i++)
    {
        config[$"Lsp:Arguments:{i}"] = arguments[i];
    }

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
        var index = 0;
        foreach (var project in projects)
        {
            projectConfig[$"Lsp:ProjectPaths:{index}"] = project.FullName;
            index++;
        }
        builder.Configuration.AddInMemoryCollection(projectConfig);
    }

    builder.Services.Configure<LanguageServerProcessConfiguration>(
        builder.Configuration.GetSection("Lsp"));

    // Register language configuration services
    builder.Services.AddSingleton<ILanguageConfigurationLoader, YamlLanguageConfigurationLoader>()
        .AddSingleton<LanguageConfigurationService>();

    builder.Services.AddSingleton<ILspNotificationHandler, WindowNotificationHandler>()
        .AddSingleton<ILspNotificationHandler, DiagnosticsNotificationHandler>()
        .AddSingleton<ILspNotificationHandler, WorkspaceNotificationHandler>()
        .AddSingleton<ILspNotificationHandler, ClientCapabilityRegistrationHandler>();

    builder.Services.AddSingleton<IApplicationService, ApplicationService>();

    builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

    logger.LogInformation("Starting application with workspace: {Workspace}, solution: {Solution}, projects: {Projects}",
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


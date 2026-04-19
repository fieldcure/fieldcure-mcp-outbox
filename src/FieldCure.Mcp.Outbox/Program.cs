using System.Reflection;
using System.Text;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.Credentials;
using FieldCure.Mcp.Outbox.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Length > 0)
{
    // CLI mode: set UTF-8 for Korean output in console window
    Console.OutputEncoding = Encoding.UTF8;

    return args[0].ToLowerInvariant() switch
    {
        "add" => await SetupRunner.RunAddAsync(args[1..]),
        "list" => await SetupRunner.RunListAsync(),
        "remove" => await SetupRunner.RunRemoveAsync(args[1..]),
        _ => PrintUsage(),
    };
}

// MCP stdio server mode
var builder = Host.CreateApplicationBuilder(Array.Empty<string>());

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Suppress WTelegramClient console logging in MCP mode
WTelegram.Helpers.Log = (_, _) => { };

builder.Services
    .AddSingleton<ChannelStore>()
    .AddSingleton<OAuthTokenStore>()
    .AddSingleton<OutboxSecretResolver>()
    .AddHttpClient()
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-outbox",
            Title = "FieldCure Outbox",
            Description = "Multi-channel messaging — Slack, Telegram, Email (SMTP/Graph), KakaoTalk, Discord",
            Version = GetPublicVersion(),
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
return 0;

static int PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  fieldcure-mcp-outbox                    Start MCP server (stdio)");
    Console.Error.WriteLine("  fieldcure-mcp-outbox add <type>         Add a messaging channel");
    Console.Error.WriteLine("  fieldcure-mcp-outbox list               List configured channels");
    Console.Error.WriteLine("  fieldcure-mcp-outbox remove <id>        Remove a channel");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Channel types: slack, telegram, gmail, naver, smtp, kakaotalk, microsoft, discord");
    return 1;
}

/// <summary>
/// Returns the user-facing server version. Strips the SemVer 2.0 build-metadata
/// suffix (<c>+&lt;commit-sha&gt;</c>) that the .NET SDK auto-appends to
/// <see cref="AssemblyInformationalVersionAttribute"/>; that hash is only useful
/// to developers and just adds noise in client UIs. The assembly attribute
/// itself still carries the full string for diagnostic logs and debuggers.
/// </summary>
static string GetPublicVersion()
{
    var info = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion;
    if (string.IsNullOrEmpty(info)) return "0.0.0";
    var plus = info.IndexOf('+');
    return plus > 0 ? info[..plus] : info;
}

using System.Text;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length > 0)
{
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
    .AddSingleton<CredentialManager>()
    .AddHttpClient()
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-outbox",
            Version = "0.1.0",
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
    Console.Error.WriteLine("Channel types: slack, telegram, gmail, outlook, microsoft365, naver, smtp, kakaotalk");
    return 1;
}

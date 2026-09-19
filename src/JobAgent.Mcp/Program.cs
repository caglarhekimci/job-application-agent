using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace JobAgent.Mcp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        var store = RuntimeStore.FromProcessConfiguration();
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<LocalHostBridgeClient>();
        var mcp = builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithRequestFilters(filters =>
            {
                filters.AddCallToolFilter(next => async (context, cancellationToken) =>
                {
                    var allowed = context.Params?.Name switch
                    {
                        "runtime_get_capabilities" => Array.Empty<string>(),
                        "profile_get_summary" => ["profileRef"],
                        "application_get_status" => ["applicationRef"],
                        "application_create_draft" => [],
                        "application_prepare_review" or "application_execute_approved" => ["applicationRef"],
                        _ => null
                    };
                    if (allowed is not null && context.Params?.Arguments is { } arguments &&
                        arguments.Keys.Any(key => !allowed.Contains(key, StringComparer.Ordinal)))
                    {
                        return new CallToolResult
                        {
                            IsError = true,
                            Content = [new TextContentBlock { Text = "Unknown tool argument." }]
                        };
                    }
                    return await next(context, cancellationToken);
                });
            })
            .WithTools<ReadOnlyTools>();
        if (store.SyntheticCommandsEnabled) mcp.WithTools<SyntheticCommandTools>();
        await builder.Build().RunAsync();
    }
}

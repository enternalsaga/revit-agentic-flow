using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace RevitMcpServer;

public class Program
{
    public const string PipeName = "revit-mcp";

    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "revit-mcp-server",
                    Version = "2.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();
        await host.RunAsync();
    }
}

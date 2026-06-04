using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class RagTools
{
    private static async Task<string> SendCommand(string command, object? parameters = null)
    {
        try
        {
            using var client = new PipeClient(Program.PipeName);
            return await client.SendCommandAsync(command, parameters);
        }
        catch (Exception ex)
        {
            return ErrorFormatter.FormatForLlm(command, ex);
        }
    }

    [McpServerTool(Name = "search_revit_api")]
    [Description("Search local Revit API documentation for class and member information. Uses BM25 keyword matching against RevitAPI.xml. Returns top-3 most relevant API docs.")]
    public static async Task<string> SearchRevitApi(
        [Description("Search query — use API class names, method names, or keywords. Examples: 'FilteredElementCollector', 'create wall', 'Transaction'.")] string query)
    {
        return await SendCommand("search_revit_api", new { query });
    }
}

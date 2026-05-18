using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class MepTools
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

    [McpServerTool(Name = "check_sprinkler_coverage")]
    [Description("Analyze sprinkler coverage on a specific level.")]
    public static async Task<string> CheckSprinklerCoverage(
        [Description("Level ElementId to analyze.")] long levelId,
        [Description("Coverage radius per sprinkler in millimeters.")] double coverageRadius = 2000)
    {
        return await SendCommand("check_sprinkler_coverage", new { levelId, coverageRadius });
    }

    [McpServerTool(Name = "count_pipe_sprinklers")]
    [Description("Count all sprinklers downstream of a pipe and check sizing rules.")]
    public static async Task<string> CountPipeSprinklers([Description("Pipe element ID to analyze.")] long pipeId)
    {
        return await SendCommand("count_pipe_sprinklers", new { pipeId });
    }

    [McpServerTool(Name = "trace_mep_connections")]
    [Description("Trace MEP network connections starting from a specific element.")]
    public static async Task<string> TraceMepConnections(
        [Description("MEP element ID to start tracing from.")] long elementId,
        [Description("Maximum traversal depth.")] int maxDepth = 50)
    {
        return await SendCommand("trace_mep_connections", new { elementId, maxDepth });
    }

    [McpServerTool(Name = "batch_change_materials")]
    [Description("Find and replace a material across elements in the project.")]
    public static async Task<string> BatchChangeMaterials(
        [Description("Exact material name to replace.")] string searchMaterialName,
        [Description("Exact replacement material name.")] string replaceMaterialName,
        [Description("Optional Revit category to limit the search.")] string? category = null,
        [Description("If true, counts occurrences without changing the model.")] bool dryRun = false)
    {
        return await SendCommand("batch_change_materials", new { category, searchMaterialName, replaceMaterialName, dryRun });
    }
}

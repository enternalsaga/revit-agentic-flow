using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class DataExtractionTools
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

    [McpServerTool(Name = "get_project_info")]
    [Description("Get project information from the current Revit project, including project name, number, address, status, client, and other metadata.")]
    public static async Task<string> GetProjectInfo()
    {
        return await SendCommand("get_project_info");
    }

    [McpServerTool(Name = "get_views")]
    [Description("Get a list of all views and sheets in the current Revit project, including view names, types, and associated levels.")]
    public static async Task<string> GetViews(
        [Description("Whether to include view templates. Defaults to false.")] bool includeTemplates = false)
    {
        return await SendCommand("get_views", new { includeTemplates });
    }

    [McpServerTool(Name = "get_levels_detail")]
    [Description("Get detailed information about all levels in the current Revit project, including elevations, building story status, and element counts per level.")]
    public static async Task<string> GetLevelsDetail()
    {
        return await SendCommand("get_levels_detail");
    }

    [McpServerTool(Name = "get_grids")]
    [Description("Get a list of all grid lines in the current Revit project with their names and endpoint coordinates.")]
    public static async Task<string> GetGrids()
    {
        return await SendCommand("get_grids");
    }

    [McpServerTool(Name = "get_categories")]
    [Description("Get a list of all element categories in the current Revit project with element counts.")]
    public static async Task<string> GetCategories(
        [Description("Whether to include categories with zero elements. Defaults to false.")] bool includeEmpty = false)
    {
        return await SendCommand("get_categories", new { includeEmpty });
    }

    [McpServerTool(Name = "get_warnings")]
    [Description("Get warnings in the current Revit project, including warning descriptions and related element IDs.")]
    public static async Task<string> GetWarnings(
        [Description("Maximum number of warnings to return. Defaults to 200.")] int limit = 200)
    {
        return await SendCommand("get_warnings", new { limit });
    }

    [McpServerTool(Name = "get_schedules")]
    [Description("Get a list of all schedules in the current Revit project with their names and types.")]
    public static async Task<string> GetSchedules()
    {
        return await SendCommand("get_schedules");
    }

    [McpServerTool(Name = "get_schedule_data")]
    [Description("Get the data content of a specific schedule by name as a table with headers and rows.")]
    public static async Task<string> GetScheduleData(
        [Description("The name of the schedule to extract data from.")] string scheduleName)
    {
        return await SendCommand("get_schedule_data", new { scheduleName });
    }

    [McpServerTool(Name = "analyze_model_statistics")]
    [Description("Analyze model complexity and composition with element counts, category breakdowns, and level-by-level distribution.")]
    public static async Task<string> AnalyzeModelStatistics(
        [Description("Whether to include detailed breakdown by family and type within each category. Defaults to true.")] bool includeDetailedTypes = true)
    {
        return await SendCommand("analyze_model_statistics", new { includeDetailedTypes });
    }

    [McpServerTool(Name = "get_material_quantities")]
    [Description("Calculate material quantities and takeoffs from the current Revit project.")]
    public static async Task<string> GetMaterialQuantities(
        [Description("Optional list of Revit category names to filter by, such as 'OST_Walls', 'OST_Floors', or 'OST_Roofs'.")] string[]? categoryFilters = null,
        [Description("Whether to only analyze currently selected elements. Defaults to false.")] bool selectedElementsOnly = false)
    {
        return await SendCommand("get_material_quantities", new
        {
            categoryFilters,
            selectedElementsOnly
        });
    }
}

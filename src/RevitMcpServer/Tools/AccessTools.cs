using System.ComponentModel;
using ModelContextProtocol.Server;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class AccessTools
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

    [McpServerTool(Name = "say_hello")]
    [Description("Display a greeting dialog in Revit. Useful for testing the connection between the MCP server and Revit.")]
    public static async Task<string> SayHello(
        [Description("Optional custom message to display in the dialog. Defaults to 'Hello MCP!'.")] string? message = null)
    {
        return await SendCommand("say_hello", new { message });
    }

    [McpServerTool(Name = "get_current_view_info")]
    [Description("Get detailed information about the current active Revit view, including view type, name, scale, and other properties.")]
    public static async Task<string> GetCurrentViewInfo()
    {
        return await SendCommand("get_current_view_info");
    }

    [McpServerTool(Name = "get_current_view_elements")]
    [Description("Get elements from the current active Revit view, optionally filtered by model or annotation categories.")]
    public static async Task<string> GetCurrentViewElements(
        [Description("List of Revit model category names, such as 'OST_Walls', 'OST_Doors', or 'OST_Floors'.")] string[]? modelCategoryList = null,
        [Description("List of Revit annotation category names, such as 'OST_Dimensions', 'OST_WallTags', or 'OST_TextNotes'.")] string[]? annotationCategoryList = null,
        [Description("Whether to include hidden elements in the results. Defaults to false.")] bool includeHidden = false,
        [Description("Maximum number of elements to return. Defaults to 100.")] int limit = 100)
    {
        return await SendCommand("get_current_view_elements", new
        {
            modelCategoryList = modelCategoryList ?? [],
            annotationCategoryList = annotationCategoryList ?? [],
            includeHidden,
            limit
        });
    }

    [McpServerTool(Name = "get_selected_elements")]
    [Description("Get elements currently selected in Revit.")]
    public static async Task<string> GetSelectedElements(
        [Description("Maximum number of elements to return. Defaults to 100.")] int limit = 100)
    {
        return await SendCommand("get_selected_elements", new { limit });
    }

    [McpServerTool(Name = "get_available_family_types")]
    [Description("Get available family types in the current Revit project, optionally filtered by category and family name.")]
    public static async Task<string> GetAvailableFamilyTypes(
        [Description("List of Revit category names to filter by, such as 'OST_Walls', 'OST_Doors', or 'OST_Furniture'.")] string[]? categoryList = null,
        [Description("Filter family types by family name using a partial match.")] string? familyNameFilter = null,
        [Description("Maximum number of family types to return. Defaults to 100.")] int limit = 100)
    {
        return await SendCommand("get_available_family_types", new
        {
            categoryList = categoryList ?? [],
            familyNameFilter = familyNameFilter ?? "",
            limit
        });
    }

    [McpServerTool(Name = "snapshot_workspace")]
    [Description("Capture the active Revit workspace with view metadata, optional image export, visible element summaries, and current selection.")]
    public static async Task<string> SnapshotWorkspace(
        [Description("Export the active Revit view to an image file. Defaults to true.")] bool includeImage = true,
        [Description("Include visible element summaries from the active view. Defaults to true.")] bool includeVisibleElements = true,
        [Description("Include currently selected elements. Defaults to true.")] bool includeSelection = true,
        [Description("Maximum number of visible elements to return. Defaults to 100.")] int elementLimit = 100,
        [Description("Image export pixel size for the longest side. Defaults to 1600.")] int pixelSize = 1600,
        [Description("Optional directory for exported images. Defaults to the system temp RevitMCP folder.")] string? outputDirectory = null)
    {
        return await SendCommand("snapshot_workspace", new
        {
            includeImage,
            includeVisibleElements,
            includeSelection,
            elementLimit,
            pixelSize,
            outputDirectory
        });
    }

    [McpServerTool(Name = "switch_view")]
    [Description("Switch the active view in Revit by view name or ElementId. If neither is provided, switches to the default {3D} view.")]
    public static async Task<string> SwitchView(
        [Description("Name of the target view, such as '{3D}', 'Level 1', or 'Ground Floor'. Partial match is supported.")] string? viewName = null,
        [Description("ElementId of the target view. Takes precedence over viewName.")] long? viewId = null)
    {
        return await SendCommand("switch_view", new { viewName, viewId });
    }

    [McpServerTool(Name = "get_element_parameters")]
    [Description("Get all parameters of a specific Revit element by ElementId, including parameter names, values, types, and read-only state.")]
    public static async Task<string> GetElementParameters(
        [Description("The ElementId of the element to get parameters from.")] long elementId,
        [Description("Whether to include read-only parameters. Defaults to true.")] bool includeReadOnly = true)
    {
        return await SendCommand("get_element_parameters", new { elementId, includeReadOnly });
    }

    [McpServerTool(Name = "get_elements_parameter_values")]
    [Description("Extract a specific parameter value from multiple elements filtered by category.")]
    public static async Task<string> GetElementsParameterValues(
        [Description("The Revit category name to filter elements, such as 'Pipes', 'Walls', or 'Doors'.")] string category,
        [Description("The name of the parameter to extract values from.")] string parameterName,
        [Description("Maximum number of elements to return. Defaults to 500.")] int limit = 500)
    {
        return await SendCommand("get_elements_parameter_values", new { category, parameterName, limit });
    }

    [McpServerTool(Name = "get_elements_summary")]
    [Description("Get a statistical summary of elements grouped by type or parameter value, with optional numeric aggregation.")]
    public static async Task<string> GetElementsSummary(
        [Description("The Revit category name, such as 'Pipes', 'Walls', or 'Doors'.")] string category,
        [Description("Parameter name to group by or aggregate. If not specified, groups by element type name.")] string? parameterName = null,
        [Description("Aggregation method for numeric parameters: count, sum, average, min, or max. Defaults to count.")] string aggregation = "count")
    {
        return await SendCommand("get_elements_summary", new { category, parameterName, aggregation });
    }
}

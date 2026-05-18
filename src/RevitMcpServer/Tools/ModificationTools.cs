using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class ModificationTools
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

    [McpServerTool(Name = "delete_element")]
    [Description("Delete one or more elements from the Revit model by element IDs.")]
    public static async Task<string> DeleteElement([Description("Element IDs to delete.")] string[] elementIds)
    {
        return await SendCommand("delete_element", new { elementIds });
    }

    [McpServerTool(Name = "operate_element")]
    [Description("Operate on Revit elements: select, color, transparency, delete, hide, isolate, and related actions.")]
    public static async Task<string> OperateElement([Description("Operation payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("operate_element", new { data });
    }

    [McpServerTool(Name = "color_elements")]
    [Description("Color elements in the current view based on a category and parameter value.")]
    public static async Task<string> ColorElements(
        [Description("Category name to color.")] string categoryName,
        [Description("Parameter name used for grouping and coloring.")] string parameterName,
        [Description("Whether to use a gradient color scheme.")] bool useGradient = false,
        [Description("Optional custom RGB color array matching the TypeScript schema.")] JsonElement? customColors = null)
    {
        return await SendCommand("color_splash", new { categoryName, parameterName, useGradient, customColors });
    }

    [McpServerTool(Name = "set_element_parameter")]
    [Description("Set a parameter value on a specific Revit element.")]
    public static async Task<string> SetElementParameter(
        [Description("ElementId of the element to modify.")] long elementId,
        [Description("Parameter name to set.")] string parameterName,
        [Description("Value to set as string, number, or boolean.")] JsonElement value)
    {
        return await SendCommand("set_element_parameter", new { elementId, parameterName, value });
    }

    [McpServerTool(Name = "set_parameter_bulk")]
    [Description("Set a parameter value for multiple elements simultaneously.")]
    public static async Task<string> SetParameterBulk([Description("Bulk parameter payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("set_parameter_bulk", new { data });
    }

    [McpServerTool(Name = "filter_and_set_parameter")]
    [Description("Find elements with a filter parameter/value and set a different parameter for all matches.")]
    public static async Task<string> FilterAndSetParameter([Description("Filter and update payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("filter_and_set_parameter", new { data });
    }

    [McpServerTool(Name = "copy_parameters")]
    [Description("Copy parameter values from one source element to multiple target elements.")]
    public static async Task<string> CopyParameters([Description("Copy-parameters payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("copy_parameters", new { data });
    }

    [McpServerTool(Name = "assign_parameter_between_levels")]
    [Description("Find elements between two levels and set a parameter value for them.")]
    public static async Task<string> AssignParameterBetweenLevels([Description("Assignment payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("assign_parameter_between_levels", new { data });
    }

    [McpServerTool(Name = "tag_all_walls")]
    [Description("Create tags for all walls in the current active view.")]
    public static async Task<string> TagAllWalls()
    {
        return await SendCommand("tag_walls");
    }

    [McpServerTool(Name = "tag_all_rooms")]
    [Description("Create tags for all rooms in the current active view.")]
    public static async Task<string> TagAllRooms()
    {
        return await SendCommand("tag_rooms");
    }

    [McpServerTool(Name = "toggle_revit_links")]
    [Description("Toggle the visibility of all Revit links in the current view.")]
    public static async Task<string> ToggleRevitLinks([Description("Whether links should be visible. If omitted, toggles current state.")] bool? visible = null)
    {
        return await SendCommand("toggle_revit_links", new { visible });
    }

    [McpServerTool(Name = "find_and_select")]
    [Description("Find elements by category or parameter filter and highlight them in the active Revit view.")]
    public static async Task<string> FindAndSelect([Description("Find/select payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("find_and_select", new { data });
    }

    [McpServerTool(Name = "measure_distance")]
    [Description("Measure 2D and 3D distance between two specific elements, or the first two selected elements.")]
    public static async Task<string> MeasureDistance(
        [Description("First element ID. Optional if using selected elements.")] long? elementId1 = null,
        [Description("Second element ID. Optional if using selected elements.")] long? elementId2 = null)
    {
        return await SendCommand("measure_distance", new { elementId1, elementId2 });
    }

    [McpServerTool(Name = "modify_element")]
    [Description("Modify an element with a payload matching the legacy TypeScript tool contract.")]
    public static async Task<string> ModifyElement([Description("Modification payload.")] JsonElement data)
    {
        return await SendCommand("modify_element", new { data });
    }
}

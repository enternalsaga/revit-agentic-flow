using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class UtilityTools
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

    private static string NotAvailable(string toolName)
    {
        return JsonConvert.SerializeObject(new
        {
            success = false,
            tool = toolName,
            message = "This TypeScript-local database/module helper has not been ported to the Phase 1 C# server."
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "send_code_to_revit")]
    [Description("Send C# code to Revit for execution.")]
    public static async Task<string> SendCodeToRevit(
        [Description("C# code to execute inside the Revit command template.")] string code,
        [Description("Optional execution parameters.")] string[]? parameters = null,
        [Description("Transaction mode: auto or none.")] string transactionMode = "auto")
    {
        return await SendCommand("send_code_to_revit", new
        {
            code,
            parameters = parameters ?? [],
            transactionMode
        });
    }

    [McpServerTool(Name = "list_available_commands")]
    [Description("List available MCP tools and Revit-side commands from command.json.")]
    public static string ListAvailableCommands()
    {
        var commandJsonPath = Path.Combine(AppContext.BaseDirectory, "command.json");
        if (!File.Exists(commandJsonPath))
            commandJsonPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RevitMcpCommandSet", "command.json"));

        JArray commands = [];
        if (File.Exists(commandJsonPath))
        {
            var parsed = JObject.Parse(File.ReadAllText(commandJsonPath));
            commands = parsed["commands"] as JArray ?? [];
        }

        var result = new
        {
            mcpTools = new[]
            {
                "say_hello", "get_current_view_info", "get_current_view_elements", "get_selected_elements",
                "get_available_family_types", "snapshot_workspace", "switch_view", "get_element_parameters",
                "get_elements_parameter_values", "get_elements_summary", "get_project_info", "get_views",
                "get_levels_detail", "get_grids", "get_categories", "get_warnings", "get_schedules",
                "get_schedule_data", "analyze_model_statistics", "get_material_quantities", "create_grid",
                "create_custom_grid", "create_level", "create_room", "create_line_based_element",
                "create_point_based_element", "create_surface_based_element", "create_parametric_door",
                "create_structural_column", "create_structural_framing_system", "create_brace",
                "create_sloped_roof", "create_curtain_wall", "create_dimensions", "create_model_snapshot",
                "edit_wall_profile", "delete_element", "operate_element", "color_elements",
                "set_element_parameter", "set_parameter_bulk", "filter_and_set_parameter", "copy_parameters",
                "assign_parameter_between_levels", "tag_all_walls", "tag_all_rooms", "toggle_revit_links",
                "find_and_select", "measure_distance", "modify_element", "check_sprinkler_coverage",
                "count_pipe_sprinklers", "trace_mep_connections", "batch_change_materials",
                "send_code_to_revit", "list_available_commands", "search_modules", "use_module",
                "store_project_data", "store_room_data", "query_stored_data", "export_room_data",
                "get_worksets", "get_worksets_detail", "get_linked_models", "get_selected_summary",
                "compare_model_snapshot", "purge_analysis", "verify_elements", "ai_element_filter"
            },
            revitCommands = commands.Select(c => new
            {
                name = c["commandName"]?.ToString(),
                description = c["description"]?.ToString()
            }),
            revitCommandCount = commands.Count,
            guidance = "Use dedicated MCP tools instead of send_code_to_revit when a dedicated tool exists."
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    [McpServerTool(Name = "search_modules")]
    [Description("Search local reusable code modules. Not available in the Phase 1 C# server.")]
    public static string SearchModules([Description("Search query.")] string query)
    {
        return NotAvailable("search_modules");
    }

    [McpServerTool(Name = "use_module")]
    [Description("Load a local reusable code module. Not available in the Phase 1 C# server.")]
    public static string UseModule([Description("Module name or ID.")] string moduleName)
    {
        return NotAvailable("use_module");
    }

    [McpServerTool(Name = "store_project_data")]
    [Description("Store project metadata. Not available in the Phase 1 C# server.")]
    public static string StoreProjectData([Description("Project metadata payload.")] JsonElement data)
    {
        return NotAvailable("store_project_data");
    }

    [McpServerTool(Name = "store_room_data")]
    [Description("Store room metadata. Not available in the Phase 1 C# server.")]
    public static string StoreRoomData([Description("Room metadata payload.")] JsonElement data)
    {
        return NotAvailable("store_room_data");
    }

    [McpServerTool(Name = "query_stored_data")]
    [Description("Query stored project/room metadata. Not available in the Phase 1 C# server.")]
    public static string QueryStoredData([Description("Query type.")] string query_type, [Description("Project ID.")] int? project_id = null, [Description("Project name.")] string? project_name = null)
    {
        return NotAvailable("query_stored_data");
    }

    [McpServerTool(Name = "export_room_data")]
    [Description("Export all room data from the current Revit project.")]
    public static async Task<string> ExportRoomData()
    {
        return await SendCommand("export_room_data");
    }

    [McpServerTool(Name = "get_worksets")]
    [Description("Get a list of all worksets in the current Revit project.")]
    public static async Task<string> GetWorksets()
    {
        return await SendCommand("get_worksets");
    }

    [McpServerTool(Name = "get_worksets_detail")]
    [Description("Get detailed workset statistics by category.")]
    public static async Task<string> GetWorksetsDetail()
    {
        return await SendCommand("get_worksets_detail");
    }

    [McpServerTool(Name = "get_linked_models")]
    [Description("Get information about all linked Revit models in the current project.")]
    public static async Task<string> GetLinkedModels()
    {
        return await SendCommand("get_linked_models");
    }

    [McpServerTool(Name = "get_selected_summary")]
    [Description("Get a statistical summary of currently selected elements.")]
    public static async Task<string> GetSelectedSummary()
    {
        return await SendCommand("get_selected_summary");
    }

    [McpServerTool(Name = "compare_model_snapshot")]
    [Description("Compare two model snapshots generated by create_model_snapshot.")]
    public static string CompareModelSnapshot(
        [Description("Older snapshot JSON object.")] JsonElement snapshot1,
        [Description("Newer snapshot JSON object.")] JsonElement snapshot2)
    {
        static JObject AsElements(JsonElement snapshot)
        {
            var token = JToken.Parse(snapshot.GetRawText());
            var elements = token["elements"] ?? token;
            return elements as JObject ?? new JObject();
        }

        var older = AsElements(snapshot1);
        var newer = AsElements(snapshot2);
        var added = new JArray();
        var removed = new JArray();
        var modified = new JArray();

        foreach (var property in newer.Properties())
        {
            if (older[property.Name] == null)
            {
                added.Add(new JObject { ["id"] = property.Name, ["info"] = property.Value });
            }
            else if (!JToken.DeepEquals(older[property.Name]?["hash"], property.Value["hash"]))
            {
                modified.Add(new JObject
                {
                    ["id"] = property.Name,
                    ["oldHash"] = older[property.Name]?["hash"],
                    ["newHash"] = property.Value["hash"],
                    ["info"] = property.Value
                });
            }
        }

        foreach (var property in older.Properties())
        {
            if (newer[property.Name] == null)
                removed.Add(new JObject { ["id"] = property.Name, ["info"] = property.Value });
        }

        return JsonConvert.SerializeObject(new
        {
            addedCount = added.Count,
            removedCount = removed.Count,
            modifiedCount = modified.Count,
            added,
            removed,
            modified
        }, Formatting.Indented);
    }

    [McpServerTool(Name = "purge_analysis")]
    [Description("Analyze the model to find unused elements like view templates and filters.")]
    public static async Task<string> PurgeAnalysis([Description("If true, only returns unused elements. If false, deletes them.")] bool analyzeOnly = true)
    {
        return await SendCommand("purge_analysis", new { analyzeOnly });
    }

    [McpServerTool(Name = "verify_elements")]
    [Description("Check if a list of element IDs still exist in the current Revit model.")]
    public static async Task<string> VerifyElements([Description("Element IDs to verify.")] long[] elementIds)
    {
        return await SendCommand("verify_elements", new { elementIds });
    }

    [McpServerTool(Name = "ai_element_filter")]
    [Description("Retrieve detailed element information from Revit using AI-oriented filters.")]
    public static async Task<string> AiElementFilter([Description("Filter configuration matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("ai_element_filter", new { data });
    }
}

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitMcpServer.Pipes;
using RevitMcpServer.Utils;

namespace RevitMcpServer.Tools;

public static class CreationTools
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

    [McpServerTool(Name = "create_grid")]
    [Description("Create a grid system in Revit with smart X/Y spacing generation. All units are in millimeters.")]
    public static async Task<string> CreateGrid(
        [Description("Number of grid lines along X-axis.")] int xCount,
        [Description("Spacing between X-axis grid lines in millimeters.")] double xSpacing,
        [Description("Number of grid lines along Y-axis.")] int yCount,
        [Description("Spacing between Y-axis grid lines in millimeters.")] double ySpacing,
        [Description("Starting label for X-axis grids.")] string xStartLabel = "A",
        [Description("Naming style for X-axis: alphabetic or numeric.")] string xNamingStyle = "alphabetic",
        [Description("Starting label for Y-axis grids.")] string yStartLabel = "1",
        [Description("Naming style for Y-axis: alphabetic or numeric.")] string yNamingStyle = "numeric",
        [Description("Minimum X extent in millimeters.")] double xExtentMin = 0,
        [Description("Maximum X extent in millimeters.")] double xExtentMax = 50000,
        [Description("Minimum Y extent in millimeters.")] double yExtentMin = 0,
        [Description("Maximum Y extent in millimeters.")] double yExtentMax = 50000,
        [Description("Grid elevation in millimeters.")] double elevation = 0,
        [Description("Starting position for first X-axis grid in millimeters.")] double xStartPosition = 0,
        [Description("Starting position for first Y-axis grid in millimeters.")] double yStartPosition = 0)
    {
        return await SendCommand("create_grid", new
        {
            xCount,
            xSpacing,
            xStartLabel,
            xNamingStyle,
            yCount,
            ySpacing,
            yStartLabel,
            yNamingStyle,
            xExtentMin,
            xExtentMax,
            yExtentMin,
            yExtentMax,
            elevation,
            xStartPosition,
            yStartPosition
        });
    }

    [McpServerTool(Name = "create_custom_grid")]
    [Description("Create a grid system with custom non-uniform spacing per axis.")]
    public static async Task<string> CreateCustomGrid(
        [Description("Axis definitions and custom spacings matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_custom_grid", new { data });
    }

    [McpServerTool(Name = "create_level")]
    [Description("Create one or more levels in Revit at specified elevations.")]
    public static async Task<string> CreateLevel(
        [Description("Array of level objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_level", new { data });
    }

    [McpServerTool(Name = "create_room")]
    [Description("Create and place rooms in Revit at specified locations.")]
    public static async Task<string> CreateRoom(
        [Description("Array of room objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_room", new { data });
    }

    [McpServerTool(Name = "create_line_based_element")]
    [Description("Create line-based elements such as walls, beams, or pipes.")]
    public static async Task<string> CreateLineBasedElement(
        [Description("Array of line-based element objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_line_based_element", new { data });
    }

    [McpServerTool(Name = "create_point_based_element")]
    [Description("Create point-based elements such as doors, windows, or furniture.")]
    public static async Task<string> CreatePointBasedElement(
        [Description("Array of point-based element objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_point_based_element", new { data });
    }

    [McpServerTool(Name = "create_surface_based_element")]
    [Description("Create surface-based elements such as floors, ceilings, or roofs.")]
    public static async Task<string> CreateSurfaceBasedElement(
        [Description("Array of surface-based element objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_surface_based_element", new { data });
    }

    [McpServerTool(Name = "create_parametric_door")]
    [Description("Create a door with specific width and height dimensions.")]
    public static async Task<string> CreateParametricDoor(
        [Description("Door creation payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_parametric_door", new { data });
    }

    [McpServerTool(Name = "create_structural_column")]
    [Description("Create one or more structural columns in Revit.")]
    public static async Task<string> CreateStructuralColumn(
        [Description("Array of structural column objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_structural_column", new { data });
    }

    [McpServerTool(Name = "create_structural_framing_system")]
    [Description("Create a structural beam framing system in Revit.")]
    public static async Task<string> CreateStructuralFramingSystem(
        [Description("Framing system payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_structural_framing_system", new { data });
    }

    [McpServerTool(Name = "create_brace")]
    [Description("Create one or more structural braces in Revit.")]
    public static async Task<string> CreateBrace(
        [Description("Array of brace objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_brace", new { data });
    }

    [McpServerTool(Name = "create_sloped_roof")]
    [Description("Create one or more roofs with slope control in Revit.")]
    public static async Task<string> CreateSlopedRoof(
        [Description("Array of sloped roof objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_sloped_roof", new { data });
    }

    [McpServerTool(Name = "create_curtain_wall")]
    [Description("Create one or more curtain walls in Revit.")]
    public static async Task<string> CreateCurtainWall(
        [Description("Array of curtain wall objects matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_curtain_wall", new { data });
    }

    [McpServerTool(Name = "create_stacked_wall")]
    [Description("Create one or more native stacked walls. Use this for vertical facade bands such as brick + louver + cladding instead of separate wall strips.")]
    public static async Task<string> CreateStackedWall(
        [Description("Array of stacked wall objects matching the TypeScript tool schema. Requires an existing native Stacked Wall type by typeId or typeName.")] JsonElement data)
    {
        return await SendCommand("create_stacked_wall", new { data });
    }

    [McpServerTool(Name = "create_or_update_basic_wall_type")]
    [Description("Create or update native Basic Wall types with editable compound layers, material functions, thicknesses, and materials. Does not use split regions.")]
    public static async Task<string> CreateOrUpdateBasicWallType(
        [Description("Array of basic wall type definitions. Each layer thickness is in millimeters and maps to a native CompoundStructureLayer.")] JsonElement data)
    {
        return await SendCommand("create_or_update_basic_wall_type", new { data });
    }

    [McpServerTool(Name = "inspect_stacked_wall_type")]
    [Description("Inspect an existing native Stacked Wall type by creating a temporary rolled-back sample wall and reporting its member Basic Wall types.")]
    public static async Task<string> InspectStackedWallType(
        [Description("Stacked wall type lookup options: typeId, typeName, sampleHeight, sampleLength, and baseLevel in millimeters.")] JsonElement data)
    {
        return await SendCommand("inspect_stacked_wall_type", data);
    }

    [McpServerTool(Name = "create_dimensions")]
    [Description("Create dimension annotations in the current Revit view.")]
    public static async Task<string> CreateDimensions(
        [Description("Dimension creation payload matching the TypeScript tool schema.")] JsonElement data)
    {
        return await SendCommand("create_dimensions", new { data });
    }

    [McpServerTool(Name = "create_model_snapshot")]
    [Description("Create a hashed snapshot of model elements for later comparison.")]
    public static async Task<string> CreateModelSnapshot(
        [Description("Optional category names to include.")] string[]? categories = null,
        [Description("Whether to include geometry hashes.")] bool includeGeometry = true,
        [Description("Whether to include parameter hashes.")] bool includeParameters = true)
    {
        return await SendCommand("create_model_snapshot", new
        {
            categories = categories ?? [],
            includeGeometry,
            includeParameters
        });
    }

    [McpServerTool(Name = "edit_wall_profile")]
    [Description("Edit the profile sketch of an existing wall by providing a new closed loop of lines.")]
    public static async Task<string> EditWallProfile(
        [Description("Wall ElementId to edit.")] long wallId,
        [Description("Closed profile loop matching the TypeScript tool schema.")] JsonElement profile)
    {
        return await SendCommand("edit_wall_profile", new { wallId, profile });
    }
}

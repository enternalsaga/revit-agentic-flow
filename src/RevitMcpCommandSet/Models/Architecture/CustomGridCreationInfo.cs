using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Architecture;

/// <summary>
/// Model for creating a custom grid system with non-uniform spacing.
/// Each axis accepts explicit position+label pairs instead of uniform spacing.
/// </summary>
public class CustomGridCreationInfo
{
    /// <summary>
    /// Array of X-axis grid definitions (vertical grid lines)
    /// </summary>
    [JsonProperty("xGrids")]
    public List<GridLineDefinition> XGrids { get; set; } = new List<GridLineDefinition>();

    /// <summary>
    /// Array of Y-axis grid definitions (horizontal grid lines)
    /// </summary>
    [JsonProperty("yGrids")]
    public List<GridLineDefinition> YGrids { get; set; } = new List<GridLineDefinition>();

    /// <summary>
    /// Minimum extent along X-axis in mm (where Y-axis grids start)
    /// </summary>
    [JsonProperty("xExtentMin")]
    public double XExtentMin { get; set; } = 0;

    /// <summary>
    /// Maximum extent along X-axis in mm (where Y-axis grids end)
    /// </summary>
    [JsonProperty("xExtentMax")]
    public double XExtentMax { get; set; } = 50000;

    /// <summary>
    /// Minimum extent along Y-axis in mm (where X-axis grids start)
    /// </summary>
    [JsonProperty("yExtentMin")]
    public double YExtentMin { get; set; } = 0;

    /// <summary>
    /// Maximum extent along Y-axis in mm (where X-axis grids end)
    /// </summary>
    [JsonProperty("yExtentMax")]
    public double YExtentMax { get; set; } = 50000;

    /// <summary>
    /// Elevation for grid lines in mm (Z-coordinate)
    /// </summary>
    [JsonProperty("elevation")]
    public double Elevation { get; set; } = 0;

    /// <summary>
    /// Validates the custom grid creation parameters
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (XGrids == null || XGrids.Count == 0)
        {
            errorMessage = "xGrids must contain at least one grid definition";
            return false;
        }

        if (YGrids == null || YGrids.Count == 0)
        {
            errorMessage = "yGrids must contain at least one grid definition";
            return false;
        }

        foreach (var grid in XGrids)
        {
            if (string.IsNullOrWhiteSpace(grid.Label))
            {
                errorMessage = "All xGrids must have a non-empty label";
                return false;
            }
        }

        foreach (var grid in YGrids)
        {
            if (string.IsNullOrWhiteSpace(grid.Label))
            {
                errorMessage = "All yGrids must have a non-empty label";
                return false;
            }
        }

        if (XExtentMin >= XExtentMax)
        {
            errorMessage = "xExtentMin must be less than xExtentMax";
            return false;
        }

        if (YExtentMin >= YExtentMax)
        {
            errorMessage = "yExtentMin must be less than yExtentMax";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}

/// <summary>
/// Definition of a single grid line with label and position
/// </summary>
public class GridLineDefinition
{
    /// <summary>
    /// Label for this grid line (e.g., "A", "1", "A'")
    /// </summary>
    [JsonProperty("label")]
    public string Label { get; set; }

    /// <summary>
    /// Absolute position of this grid line in mm
    /// </summary>
    [JsonProperty("position")]
    public double Position { get; set; }
}

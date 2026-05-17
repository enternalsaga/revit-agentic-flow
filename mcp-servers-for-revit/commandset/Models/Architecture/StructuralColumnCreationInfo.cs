using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Architecture;

/// <summary>
/// Model for creating structural columns with base/top level control.
/// </summary>
public class StructuralColumnCreationInfo
{
    /// <summary>
    /// Array of columns to create
    /// </summary>
    [JsonProperty("data")]
    public List<StructuralColumnData> Data { get; set; } = new List<StructuralColumnData>();
}

/// <summary>
/// Data for a single structural column
/// </summary>
public class StructuralColumnData
{
    /// <summary>
    /// Location point where the column will be placed (mm)
    /// </summary>
    [JsonProperty("locationPoint")]
    public ColumnPoint LocationPoint { get; set; }

    /// <summary>
    /// ElementId of the column family type. -1 for default type.
    /// </summary>
    [JsonProperty("typeId")]
    public int TypeId { get; set; } = -1;

    /// <summary>
    /// Elevation of the base level in mm
    /// </summary>
    [JsonProperty("baseLevelElevation")]
    public double BaseLevelElevation { get; set; } = 0;

    /// <summary>
    /// Offset from the base level in mm
    /// </summary>
    [JsonProperty("baseOffset")]
    public double BaseOffset { get; set; } = 0;

    /// <summary>
    /// Elevation of the top level in mm. If -1, uses next level above base.
    /// </summary>
    [JsonProperty("topLevelElevation")]
    public double TopLevelElevation { get; set; } = -1;

    /// <summary>
    /// Offset from the top level in mm
    /// </summary>
    [JsonProperty("topOffset")]
    public double TopOffset { get; set; } = 0;

    /// <summary>
    /// Rotation angle in degrees
    /// </summary>
    [JsonProperty("rotation")]
    public double Rotation { get; set; } = 0;

    /// <summary>
    /// Optional column width in mm (for parametric sizing)
    /// </summary>
    [JsonProperty("width")]
    public double? Width { get; set; }

    /// <summary>
    /// Optional column depth in mm (for parametric sizing)
    /// </summary>
    [JsonProperty("depth")]
    public double? Depth { get; set; }
}

/// <summary>
/// Simple 3D point for column placement
/// </summary>
public class ColumnPoint
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }

    [JsonProperty("z")]
    public double Z { get; set; }
}

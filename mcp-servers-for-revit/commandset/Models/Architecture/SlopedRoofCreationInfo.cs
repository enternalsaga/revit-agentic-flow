using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Architecture;

/// <summary>
/// Model for creating sloped roofs with per-edge slope control.
/// </summary>
public class SlopedRoofCreationInfo
{
    /// <summary>
    /// Array of sloped roofs to create
    /// </summary>
    [JsonProperty("data")]
    public List<SlopedRoofData> Data { get; set; } = new List<SlopedRoofData>();
}

/// <summary>
/// Data for a single sloped roof
/// </summary>
public class SlopedRoofData
{
    /// <summary>
    /// Description of the roof
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = "Roof";

    /// <summary>
    /// ElementId of the roof type. -1 for default type.
    /// </summary>
    [JsonProperty("typeId")]
    public int TypeId { get; set; } = -1;

    /// <summary>
    /// Elevation of the base level in mm
    /// </summary>
    [JsonProperty("baseLevelElevation")]
    public double BaseLevelElevation { get; set; }

    /// <summary>
    /// Offset from the base level in mm
    /// </summary>
    [JsonProperty("baseOffset")]
    public double BaseOffset { get; set; } = 0;

    /// <summary>
    /// Boundary edges with slope control
    /// </summary>
    [JsonProperty("boundary")]
    public List<SlopedRoofEdge> Boundary { get; set; } = new List<SlopedRoofEdge>();

    /// <summary>
    /// Roof overhang distance in mm
    /// </summary>
    [JsonProperty("overhang")]
    public double Overhang { get; set; } = 0;
}

/// <summary>
/// A single boundary edge of a sloped roof with slope definition
/// </summary>
public class SlopedRoofEdge
{
    /// <summary>
    /// Start point of the edge (mm)
    /// </summary>
    [JsonProperty("p0")]
    public RoofPoint P0 { get; set; }

    /// <summary>
    /// End point of the edge (mm)
    /// </summary>
    [JsonProperty("p1")]
    public RoofPoint P1 { get; set; }

    /// <summary>
    /// Whether this edge defines a slope
    /// </summary>
    [JsonProperty("definesSlope")]
    public bool DefinesSlope { get; set; } = false;

    /// <summary>
    /// Slope angle in degrees from horizontal
    /// </summary>
    [JsonProperty("slopeAngle")]
    public double SlopeAngle { get; set; } = 0;
}

/// <summary>
/// Simple 3D point for roof boundary
/// </summary>
public class RoofPoint
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }

    [JsonProperty("z")]
    public double Z { get; set; }
}

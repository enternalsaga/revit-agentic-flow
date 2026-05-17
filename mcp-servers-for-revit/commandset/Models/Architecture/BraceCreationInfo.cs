using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.Architecture;

/// <summary>
/// Model for creating structural braces.
/// </summary>
public class BraceCreationInfo
{
    [JsonProperty("data")]
    public List<BraceData> Data { get; set; } = new List<BraceData>();
}

public class BraceData
{
    [JsonProperty("startPoint")]
    public JZPoint StartPoint { get; set; }

    [JsonProperty("endPoint")]
    public JZPoint EndPoint { get; set; }

    [JsonProperty("baseLevelElevation")]
    public double BaseLevelElevation { get; set; } = 0;

    [JsonProperty("typeId")]
    public int TypeId { get; set; } = -1;
}

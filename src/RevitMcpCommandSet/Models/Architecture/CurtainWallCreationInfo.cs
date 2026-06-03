using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.Architecture;

/// <summary>
/// Model for creating curtain walls.
/// </summary>
public class CurtainWallCreationInfo
{
    [JsonProperty("data")]
    public List<CurtainWallData> Data { get; set; } = new List<CurtainWallData>();
}

public class CurtainWallData
{
    [JsonProperty("startPoint")]
    public JZPoint StartPoint { get; set; }

    [JsonProperty("endPoint")]
    public JZPoint EndPoint { get; set; }

    [JsonProperty("height")]
    public double Height { get; set; }

    [JsonProperty("baseLevel")]
    public double BaseLevel { get; set; }

    [JsonProperty("baseOffset")]
    public double BaseOffset { get; set; } = 0;

    [JsonProperty("typeId")]
    public int TypeId { get; set; } = -1;
}

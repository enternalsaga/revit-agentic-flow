using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.Architecture;

public class StackedWallCreationInfo
{
    [JsonProperty("data")]
    public List<StackedWallData> Data { get; set; } = new List<StackedWallData>();
}

public class StackedWallData
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
    public long TypeId { get; set; } = -1;

    [JsonProperty("typeName")]
    public string TypeName { get; set; } = "";

    [JsonProperty("structural")]
    public bool Structural { get; set; } = false;

    [JsonProperty("flip")]
    public bool Flip { get; set; } = false;
}

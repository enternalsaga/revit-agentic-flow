using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class FindAndSelectResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("isolated")] public bool Isolated { get; set; }
    }

    public class MeasureDistanceResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("distance2D")] public double? Distance2D { get; set; }
        [JsonProperty("distance3D")] public double? Distance3D { get; set; }
        [JsonProperty("element1")] public long ElementId1 { get; set; }
        [JsonProperty("element2")] public long ElementId2 { get; set; }
    }
}

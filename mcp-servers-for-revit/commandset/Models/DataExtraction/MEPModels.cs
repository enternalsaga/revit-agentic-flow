using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class MepTraceResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("totalConnected")] public int TotalConnected { get; set; }
        [JsonProperty("connectedElements")] public List<long> ConnectedElements { get; set; } = new List<long>();
    }

    public class PipeSprinklerCountResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("sprinklerCount")] public int SprinklerCount { get; set; }
        [JsonProperty("pipeDiameter")] public double PipeDiameter { get; set; }
        [JsonProperty("satisfiesNFPA")] public bool SatisfiesNFPA { get; set; }
        [JsonProperty("requiredDiameter")] public double RequiredDiameter { get; set; }
    }

    public class SprinklerCoverageResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("totalSprinklers")] public int TotalSprinklers { get; set; }
        [JsonProperty("coverageRadius")] public double CoverageRadius { get; set; }
        [JsonProperty("coveredArea")] public double CoveredArea { get; set; }
    }
}

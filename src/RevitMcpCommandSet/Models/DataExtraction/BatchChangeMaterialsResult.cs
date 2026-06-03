using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class BatchChangeMaterialsResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("dryRun")] public bool DryRun { get; set; }
        [JsonProperty("instancesUpdated")] public int InstancesUpdated { get; set; }
        [JsonProperty("typesUpdated")] public int TypesUpdated { get; set; }
        [JsonProperty("compoundStructuresUpdated")] public int CompoundStructuresUpdated { get; set; }
    }
}

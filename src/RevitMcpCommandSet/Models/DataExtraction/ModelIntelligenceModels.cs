using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class VerifyElementsResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("existingIds")] public List<long> ExistingIds { get; set; } = new List<long>();
        [JsonProperty("missingIds")] public List<long> MissingIds { get; set; } = new List<long>();
    }

    public class ModelSnapshotResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
        [JsonProperty("elements")] public Dictionary<string, SnapshotElementInfo> Elements { get; set; } = new Dictionary<string, SnapshotElementInfo>();
    }

    public class SnapshotElementInfo
    {
        [JsonProperty("hash")] public string Hash { get; set; }
        [JsonProperty("category")] public string Category { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }
}

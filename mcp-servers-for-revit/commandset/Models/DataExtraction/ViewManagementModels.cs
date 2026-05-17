using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class ToggleRevitLinksResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("linksToggled")] public int LinksToggled { get; set; }
    }

    public class PurgeAnalysisResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("unusedViewTemplates")] public List<string> UnusedViewTemplates { get; set; } = new List<string>();
        [JsonProperty("unusedFilters")] public List<string> UnusedFilters { get; set; } = new List<string>();
        [JsonProperty("deleted")] public bool Deleted { get; set; }
    }
}

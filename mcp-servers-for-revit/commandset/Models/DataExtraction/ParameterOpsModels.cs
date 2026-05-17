using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class ParameterOperationResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("updatedCount")] public int UpdatedCount { get; set; }
        [JsonProperty("failedCount")] public int FailedCount { get; set; }
        [JsonProperty("details")] public List<string> Details { get; set; } = new();
    }

    public class FilterAndSetResult : ParameterOperationResult
    {
        [JsonProperty("dryRun")] public bool DryRun { get; set; }
        [JsonProperty("matchedCount")] public int MatchedCount { get; set; }
        [JsonProperty("matchedIds")] public List<long> MatchedIds { get; set; } = new();
    }

    public class CopyParametersResult : ParameterOperationResult
    {
        [JsonProperty("sourceId")] public long SourceId { get; set; }
        [JsonProperty("targetIds")] public List<long> TargetIds { get; set; } = new();
        [JsonProperty("copiedParameters")] public List<string> CopiedParameters { get; set; } = new();
    }

    public class AssignBetweenLevelsResult : ParameterOperationResult
    {
        [JsonProperty("dryRun")] public bool DryRun { get; set; }
        [JsonProperty("lowerLevelId")] public long LowerLevelId { get; set; }
        [JsonProperty("upperLevelId")] public long UpperLevelId { get; set; }
    }
}

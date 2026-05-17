using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    // ===== Worksets =====
    public class WorksetModel
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("isOpen")] public bool IsOpen { get; set; }
        [JsonProperty("isDefault")] public bool IsDefault { get; set; }
        [JsonProperty("owner")] public string Owner { get; set; }
    }

    public class GetWorksetsResult
    {
        [JsonProperty("isWorkshared")] public bool IsWorkshared { get; set; }
        [JsonProperty("totalWorksets")] public int TotalWorksets { get; set; }
        [JsonProperty("worksets")] public List<WorksetModel> Worksets { get; set; } = new();
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ===== Worksets Detail =====
    public class WorksetCategoryCount
    {
        [JsonProperty("categoryName")] public string CategoryName { get; set; }
        [JsonProperty("count")] public int Count { get; set; }
    }

    public class WorksetDetailModel
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("totalElements")] public int TotalElements { get; set; }
        [JsonProperty("categories")] public List<WorksetCategoryCount> Categories { get; set; } = new();
    }

    public class GetWorksetsDetailResult
    {
        [JsonProperty("isWorkshared")] public bool IsWorkshared { get; set; }
        [JsonProperty("worksets")] public List<WorksetDetailModel> Worksets { get; set; } = new();
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ===== Grids =====
    public class GridModel
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("startX")] public double StartX { get; set; }
        [JsonProperty("startY")] public double StartY { get; set; }
        [JsonProperty("endX")] public double EndX { get; set; }
        [JsonProperty("endY")] public double EndY { get; set; }
        [JsonProperty("isCurved")] public bool IsCurved { get; set; }
    }

    public class GetGridsResult
    {
        [JsonProperty("totalGrids")] public int TotalGrids { get; set; }
        [JsonProperty("grids")] public List<GridModel> Grids { get; set; } = new();
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ===== Levels Detail =====
    public class LevelDetailModel
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("elevation")] public double Elevation { get; set; }
        [JsonProperty("isBuildingStory")] public bool IsBuildingStory { get; set; }
        [JsonProperty("elementCount")] public int ElementCount { get; set; }
    }

    public class GetLevelsDetailResult
    {
        [JsonProperty("totalLevels")] public int TotalLevels { get; set; }
        [JsonProperty("levels")] public List<LevelDetailModel> Levels { get; set; } = new();
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ===== Schedules =====
    public class ScheduleModel
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("scheduleType")] public string ScheduleType { get; set; }
    }

    public class GetSchedulesResult
    {
        [JsonProperty("totalSchedules")] public int TotalSchedules { get; set; }
        [JsonProperty("schedules")] public List<ScheduleModel> Schedules { get; set; } = new();
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ===== Schedule Data =====
    public class GetScheduleDataResult
    {
        [JsonProperty("scheduleName")] public string ScheduleName { get; set; }
        [JsonProperty("headers")] public List<string> Headers { get; set; } = new();
        [JsonProperty("rows")] public List<List<string>> Rows { get; set; } = new();
        [JsonProperty("totalRows")] public int TotalRows { get; set; }
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    // ===== Linked Models =====
    public class LinkedModelInfo
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("filePath")] public string FilePath { get; set; }
        [JsonProperty("isLoaded")] public bool IsLoaded { get; set; }
        [JsonProperty("linkType")] public string LinkType { get; set; }
    }

    public class GetLinkedModelsResult
    {
        [JsonProperty("totalLinks")] public int TotalLinks { get; set; }
        [JsonProperty("links")] public List<LinkedModelInfo> Links { get; set; } = new();
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }
}

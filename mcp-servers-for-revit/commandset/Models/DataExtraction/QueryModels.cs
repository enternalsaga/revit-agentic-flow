using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    /// <summary>
    /// Model for project information extraction
    /// </summary>
    public class ProjectInfoModel
    {
        [JsonProperty("projectName")]
        public string ProjectName { get; set; }

        [JsonProperty("projectNumber")]
        public string ProjectNumber { get; set; }

        [JsonProperty("clientName")]
        public string ClientName { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("buildingName")]
        public string BuildingName { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("organizationName")]
        public string OrganizationName { get; set; }

        [JsonProperty("organizationDescription")]
        public string OrganizationDescription { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("issueDate")]
        public string IssueDate { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; }
    }

    /// <summary>
    /// Result container for project info
    /// </summary>
    public class GetProjectInfoResult
    {
        [JsonProperty("projectInfo")]
        public ProjectInfoModel ProjectInfo { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Model for category count
    /// </summary>
    public class CategoryCountModel
    {
        [JsonProperty("categoryName")]
        public string CategoryName { get; set; }

        [JsonProperty("builtInCategory")]
        public string BuiltInCategory { get; set; }

        [JsonProperty("elementCount")]
        public int ElementCount { get; set; }
    }

    /// <summary>
    /// Result container for categories list
    /// </summary>
    public class GetCategoriesResult
    {
        [JsonProperty("totalCategories")]
        public int TotalCategories { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("categories")]
        public List<CategoryCountModel> Categories { get; set; } = new List<CategoryCountModel>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Model for a single parameter
    /// </summary>
    public class ParameterModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("storageType")]
        public string StorageType { get; set; }

        [JsonProperty("isReadOnly")]
        public bool IsReadOnly { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }

        [JsonProperty("isShared")]
        public bool IsShared { get; set; }
    }

    /// <summary>
    /// Result container for element parameters
    /// </summary>
    public class GetElementParametersResult
    {
        [JsonProperty("elementId")]
        public long ElementId { get; set; }

        [JsonProperty("elementCategory")]
        public string ElementCategory { get; set; }

        [JsonProperty("elementTypeName")]
        public string ElementTypeName { get; set; }

        [JsonProperty("parameters")]
        public List<ParameterModel> Parameters { get; set; } = new List<ParameterModel>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Model for element parameter value extraction
    /// </summary>
    public class ElementParameterValueModel
    {
        [JsonProperty("elementId")]
        public long ElementId { get; set; }

        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }

    /// <summary>
    /// Result container for elements parameter values
    /// </summary>
    public class GetElementsParameterValuesResult
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("parameterName")]
        public string ParameterName { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("elements")]
        public List<ElementParameterValueModel> Elements { get; set; } = new List<ElementParameterValueModel>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Model for summary group
    /// </summary>
    public class SummaryGroupModel
    {
        [JsonProperty("groupName")]
        public string GroupName { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("sum")]
        public double? Sum { get; set; }

        [JsonProperty("average")]
        public double? Average { get; set; }

        [JsonProperty("min")]
        public double? Min { get; set; }

        [JsonProperty("max")]
        public double? Max { get; set; }
    }

    /// <summary>
    /// Result container for elements summary
    /// </summary>
    public class GetElementsSummaryResult
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("groups")]
        public List<SummaryGroupModel> Groups { get; set; } = new List<SummaryGroupModel>();

        [JsonProperty("grandTotal")]
        public double? GrandTotal { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Model for a view
    /// </summary>
    public class ViewModel
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("viewType")]
        public string ViewType { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("isTemplate")]
        public bool IsTemplate { get; set; }

        [JsonProperty("sheetNumber")]
        public string SheetNumber { get; set; }
    }

    /// <summary>
    /// Result container for views list
    /// </summary>
    public class GetViewsResult
    {
        [JsonProperty("totalViews")]
        public int TotalViews { get; set; }

        [JsonProperty("totalSheets")]
        public int TotalSheets { get; set; }

        [JsonProperty("views")]
        public List<ViewModel> Views { get; set; } = new List<ViewModel>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Model for a warning
    /// </summary>
    public class WarningModel
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("severity")]
        public string Severity { get; set; }

        [JsonProperty("elementIds")]
        public List<long> ElementIds { get; set; } = new List<long>();
    }

    /// <summary>
    /// Result container for warnings list
    /// </summary>
    public class GetWarningsResult
    {
        [JsonProperty("totalWarnings")]
        public int TotalWarnings { get; set; }

        [JsonProperty("warnings")]
        public List<WarningModel> Warnings { get; set; } = new List<WarningModel>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}

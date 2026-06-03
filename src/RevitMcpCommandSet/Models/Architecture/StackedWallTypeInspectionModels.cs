using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Architecture;

public class StackedWallTypeInspectionInfo
{
    [JsonProperty("typeId")]
    public long TypeId { get; set; } = -1;

    [JsonProperty("typeName")]
    public string TypeName { get; set; } = "";

    [JsonProperty("sampleHeight")]
    public double SampleHeight { get; set; } = 6000;

    [JsonProperty("sampleLength")]
    public double SampleLength { get; set; } = 2000;

    [JsonProperty("baseLevel")]
    public double BaseLevel { get; set; } = 0;
}

public class StackedWallTypeInspectionResult
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";
    public double SampleHeight { get; set; }
    public List<StackedWallMemberResult> Members { get; set; } = new List<StackedWallMemberResult>();
}

public class StackedWallMemberResult
{
    public long MemberId { get; set; }
    public long WallTypeId { get; set; }
    public string WallTypeName { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
    public double MinZ { get; set; }
    public double MaxZ { get; set; }
}

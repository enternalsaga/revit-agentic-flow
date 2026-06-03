using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Architecture;

public class BasicWallTypeCreationInfo
{
    [JsonProperty("data")]
    public List<BasicWallTypeData> Data { get; set; } = new List<BasicWallTypeData>();
}

public class BasicWallTypeData
{
    [JsonProperty("typeName")]
    public string TypeName { get; set; } = "";

    [JsonProperty("baseTypeId")]
    public long BaseTypeId { get; set; } = -1;

    [JsonProperty("baseTypeName")]
    public string BaseTypeName { get; set; } = "";

    [JsonProperty("updateExisting")]
    public bool UpdateExisting { get; set; } = true;

    [JsonProperty("layers")]
    public List<BasicWallTypeLayerData> Layers { get; set; } = new List<BasicWallTypeLayerData>();

    [JsonProperty("structuralMaterialLayerIndex")]
    public int StructuralMaterialLayerIndex { get; set; } = -1;

    [JsonProperty("variableLayerIndex")]
    public int VariableLayerIndex { get; set; } = -1;
}

public class BasicWallTypeLayerData
{
    [JsonProperty("thickness")]
    public double Thickness { get; set; }

    [JsonProperty("function")]
    public string Function { get; set; } = "Structure";

    [JsonProperty("materialId")]
    public long MaterialId { get; set; } = -1;

    [JsonProperty("materialName")]
    public string MaterialName { get; set; } = "";

    [JsonProperty("createMaterialIfMissing")]
    public bool CreateMaterialIfMissing { get; set; } = true;

    [JsonProperty("color")]
    public string Color { get; set; } = "";

    [JsonProperty("transparency")]
    public int Transparency { get; set; } = -1;
}

public class BasicWallTypeResult
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";
    public double TotalThickness { get; set; }
    public List<BasicWallTypeLayerResult> Layers { get; set; } = new List<BasicWallTypeLayerResult>();
}

public class BasicWallTypeLayerResult
{
    public int Index { get; set; }
    public double Thickness { get; set; }
    public string Function { get; set; } = "";
    public long MaterialId { get; set; }
    public string MaterialName { get; set; } = "";
}

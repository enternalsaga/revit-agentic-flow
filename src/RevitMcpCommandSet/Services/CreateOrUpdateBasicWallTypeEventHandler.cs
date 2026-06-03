using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services
{
    public class CreateOrUpdateBasicWallTypeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private readonly List<string> _warnings = new List<string>();

        public List<BasicWallTypeData> WallTypesData { get; private set; }
        public AIResult<List<BasicWallTypeResult>> Result { get; private set; }

        public void SetParameters(List<BasicWallTypeData> data)
        {
            WallTypesData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var results = new List<BasicWallTypeResult>();
                _warnings.Clear();

                using (Transaction trans = new Transaction(doc, "Create Or Update Basic Wall Types"))
                {
                    trans.Start();

                    foreach (var data in WallTypesData)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(data.TypeName))
                            {
                                _warnings.Add("Skipped wall type with empty typeName.");
                                continue;
                            }

                            if (data.Layers == null || data.Layers.Count == 0)
                            {
                                _warnings.Add($"Skipped '{data.TypeName}' because layers are empty.");
                                continue;
                            }

                            WallType wallType = ResolveTargetWallType(data);
                            var layers = new List<CompoundStructureLayer>();

                            foreach (var layerData in data.Layers)
                            {
                                MaterialFunctionAssignment function = ParseLayerFunction(layerData.Function);
                                double width = layerData.Thickness / 304.8;
                                if (function != MaterialFunctionAssignment.Membrane && width <= 0)
                                    throw new ArgumentException($"Layer thickness must be > 0mm for function {function}.");

                                ElementId materialId = ResolveMaterialId(layerData);
                                layers.Add(new CompoundStructureLayer(width, function, materialId));
                            }

                            CompoundStructure compoundStructure = CompoundStructure.CreateSimpleCompoundStructure(layers);
                            if (data.StructuralMaterialLayerIndex >= 0 && data.StructuralMaterialLayerIndex < layers.Count)
                                compoundStructure.StructuralMaterialIndex = data.StructuralMaterialLayerIndex;
                            if (data.VariableLayerIndex >= 0 && data.VariableLayerIndex < layers.Count)
                                compoundStructure.VariableLayerIndex = data.VariableLayerIndex;

                            wallType.SetCompoundStructure(compoundStructure);
                            results.Add(ToResult(wallType));
                        }
                        catch (Exception ex)
                        {
                            _warnings.Add($"Failed '{data.TypeName}': {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                string message = $"Created/updated {results.Count} basic wall type(s).";
                if (_warnings.Count > 0)
                    message += "\n\nNotes:\n  - " + string.Join("\n  - ", _warnings);

                Result = new AIResult<List<BasicWallTypeResult>>
                {
                    Success = results.Count > 0,
                    Message = message,
                    Response = results
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<BasicWallTypeResult>>
                {
                    Success = false,
                    Message = $"Failed to create/update basic wall types: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create/update basic wall types: {ex.Message}");
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Create Or Update Basic Wall Types";
        }

        private WallType ResolveTargetWallType(BasicWallTypeData data)
        {
            WallType existing = FindBasicWallTypeByName(data.TypeName);
            if (existing != null)
            {
                if (data.UpdateExisting)
                    return existing;

                throw new InvalidOperationException($"Wall type '{data.TypeName}' already exists and updateExisting is false.");
            }

            WallType baseType = ResolveBaseBasicWallType(data);
            WallType duplicated = baseType.Duplicate(data.TypeName) as WallType;
            if (duplicated == null)
                throw new InvalidOperationException($"Could not duplicate base wall type '{baseType.Name}'.");

            return duplicated;
        }

        private WallType ResolveBaseBasicWallType(BasicWallTypeData data)
        {
            if (data.BaseTypeId > 0)
            {
                Element element = doc.GetElement(RevitIdUtils.ToElementId(data.BaseTypeId));
                if (element is WallType wallType && wallType.Kind == WallKind.Basic)
                    return wallType;
                _warnings.Add($"baseTypeId {data.BaseTypeId} is not a basic wall type. Falling back.");
            }

            if (!string.IsNullOrWhiteSpace(data.BaseTypeName))
            {
                WallType byName = FindBasicWallTypeByName(data.BaseTypeName);
                if (byName != null)
                    return byName;
                _warnings.Add($"Base basic wall type '{data.BaseTypeName}' was not found. Falling back.");
            }

            WallType firstBasic = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

            if (firstBasic == null)
                throw new InvalidOperationException("No basic wall type exists in this project.");

            return firstBasic;
        }

        private WallType FindBasicWallTypeByName(string typeName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic && string.Equals(wt.Name, typeName, StringComparison.OrdinalIgnoreCase));
        }

        private MaterialFunctionAssignment ParseLayerFunction(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                return MaterialFunctionAssignment.Structure;

            string normalized = functionName.Replace(" ", "").Replace("_", "").Replace("-", "");
            foreach (MaterialFunctionAssignment value in Enum.GetValues(typeof(MaterialFunctionAssignment)))
            {
                if (string.Equals(value.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            throw new ArgumentException($"Unsupported material function '{functionName}'.");
        }

        private ElementId ResolveMaterialId(BasicWallTypeLayerData layerData)
        {
            if (layerData.MaterialId > 0)
            {
                Element materialElement = doc.GetElement(RevitIdUtils.ToElementId(layerData.MaterialId));
                if (materialElement is Material)
                    return materialElement.Id;
                _warnings.Add($"materialId {layerData.MaterialId} is not a Material. Falling back.");
            }

            if (string.IsNullOrWhiteSpace(layerData.MaterialName))
                return ElementId.InvalidElementId;

            Material material = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => string.Equals(m.Name, layerData.MaterialName, StringComparison.OrdinalIgnoreCase));

            if (material == null)
            {
                if (!layerData.CreateMaterialIfMissing)
                    throw new InvalidOperationException($"Material '{layerData.MaterialName}' not found.");

                ElementId materialId = Material.Create(doc, layerData.MaterialName);
                material = doc.GetElement(materialId) as Material;
            }

            if (material != null)
                ApplyMaterialOverrides(material, layerData);

            return material == null ? ElementId.InvalidElementId : material.Id;
        }

        private void ApplyMaterialOverrides(Material material, BasicWallTypeLayerData layerData)
        {
            if (!string.IsNullOrWhiteSpace(layerData.Color))
            {
                Color color = ParseColor(layerData.Color);
                material.Color = color;
                material.SurfaceForegroundPatternColor = color;
                material.CutForegroundPatternColor = color;
            }

            if (layerData.Transparency >= 0)
                material.Transparency = Math.Max(0, Math.Min(100, layerData.Transparency));
        }

        private Color ParseColor(string value)
        {
            string hex = value.Trim();
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6)
                throw new ArgumentException($"Color '{value}' must be #RRGGBB.");

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color(r, g, b);
        }

        private BasicWallTypeResult ToResult(WallType wallType)
        {
            var result = new BasicWallTypeResult
            {
                TypeId = wallType.Id.GetValue(),
                TypeName = wallType.Name,
                TotalThickness = wallType.Width * 304.8
            };

            CompoundStructure structure = wallType.GetCompoundStructure();
            if (structure != null)
            {
                IList<CompoundStructureLayer> layers = structure.GetLayers();
                for (int i = 0; i < layers.Count; i++)
                {
                    CompoundStructureLayer layer = layers[i];
                    Material material = layer.MaterialId == ElementId.InvalidElementId ? null : doc.GetElement(layer.MaterialId) as Material;
                    result.Layers.Add(new BasicWallTypeLayerResult
                    {
                        Index = i,
                        Thickness = layer.Width * 304.8,
                        Function = layer.Function.ToString(),
                        MaterialId = layer.MaterialId == ElementId.InvalidElementId ? -1 : layer.MaterialId.GetValue(),
                        MaterialName = material == null ? "" : material.Name
                    });
                }
            }

            return result;
        }
    }
}

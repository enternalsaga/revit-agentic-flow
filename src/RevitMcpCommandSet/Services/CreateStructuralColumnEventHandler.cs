using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CreateStructuralColumnEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<StructuralColumnData> ColumnsData { get; private set; }
        public AIResult<List<StructuralColumnResult>> Result { get; private set; }
        private List<string> _warnings = new List<string>();

        public void SetParameters(List<StructuralColumnData> data)
        {
            ColumnsData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var createdColumns = new List<StructuralColumnResult>();
                _warnings.Clear();

                using (Transaction trans = new Transaction(doc, "Create Structural Columns"))
                {
                    trans.Start();

                    foreach (var colData in ColumnsData)
                    {
                        try
                        {
                            // Step 1: Find or default the column family symbol
                            FamilySymbol columnSymbol = null;

                            if (colData.TypeId > 0)
                            {
                                ElementId typeEleId = new ElementId(colData.TypeId);
                                Element typeEle = doc.GetElement(typeEleId);
                                if (typeEle is FamilySymbol fs)
                                {
                                    columnSymbol = fs;
                                }
                                else
                                {
                                    _warnings.Add($"Requested typeId {colData.TypeId} is not a valid FamilySymbol. Using default.");
                                }
                            }

                            if (columnSymbol == null)
                            {
                                // Try structural columns first
                                columnSymbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault();

                                // Fallback to architectural columns
                                if (columnSymbol == null)
                                {
                                    columnSymbol = new FilteredElementCollector(doc)
                                        .OfClass(typeof(FamilySymbol))
                                        .OfCategory(BuiltInCategory.OST_Columns)
                                        .Cast<FamilySymbol>()
                                        .FirstOrDefault();
                                }

                                if (columnSymbol == null)
                                {
                                    _warnings.Add("No column families loaded in the project. Skipping column.");
                                    continue;
                                }
                            }

                            if (!columnSymbol.IsActive)
                                columnSymbol.Activate();

                            // Step 2: Find base level
                            Level baseLevel = doc.FindNearestLevel(colData.BaseLevelElevation / 304.8);
                            if (baseLevel == null)
                            {
                                _warnings.Add($"Could not find base level near elevation {colData.BaseLevelElevation}mm. Skipping column.");
                                continue;
                            }

                            // Step 3: Find top level
                            Level topLevel = null;
                            if (colData.TopLevelElevation >= 0)
                            {
                                topLevel = doc.FindNearestLevel(colData.TopLevelElevation / 304.8);
                            }
                            else
                            {
                                // Use next level above base
                                topLevel = new FilteredElementCollector(doc)
                                    .OfClass(typeof(Level))
                                    .Cast<Level>()
                                    .Where(l => l.Elevation > baseLevel.Elevation + 0.01)
                                    .OrderBy(l => l.Elevation)
                                    .FirstOrDefault();
                            }

                            if (topLevel == null)
                            {
                                _warnings.Add($"Could not find top level. Using base level as top level for column at ({colData.LocationPoint.X}, {colData.LocationPoint.Y}).");
                                topLevel = baseLevel;
                            }

                            // Step 4: Create the column
                            XYZ locationPoint = new XYZ(
                                colData.LocationPoint.X / 304.8,
                                colData.LocationPoint.Y / 304.8,
                                colData.LocationPoint.Z / 304.8
                            );

                            FamilyInstance column = doc.Create.NewFamilyInstance(
                                locationPoint,
                                columnSymbol,
                                baseLevel,
                                StructuralType.Column
                            );

                            if (column != null)
                            {
                                // Set base level and offset
                                Parameter baseLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                                if (baseLevelParam != null)
                                    baseLevelParam.Set(baseLevel.Id);

                                Parameter baseOffsetParam = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                                if (baseOffsetParam != null)
                                    baseOffsetParam.Set(colData.BaseOffset / 304.8);

                                // Set top level and offset
                                Parameter topLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                                if (topLevelParam != null)
                                    topLevelParam.Set(topLevel.Id);

                                Parameter topOffsetParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                                if (topOffsetParam != null)
                                    topOffsetParam.Set(colData.TopOffset / 304.8);

                                // Set rotation if specified
                                if (Math.Abs(colData.Rotation) > 0.001)
                                {
                                    double rotationRadians = colData.Rotation * Math.PI / 180.0;
                                    Line rotationAxis = Line.CreateBound(
                                        locationPoint,
                                        locationPoint + XYZ.BasisZ
                                    );
                                    ElementTransformUtils.RotateElement(doc, column.Id, rotationAxis, rotationRadians);
                                }

                                // Set width/depth if provided
                                if (colData.Width.HasValue && colData.Width.Value > 0)
                                {
                                    Parameter widthParam = column.LookupParameter("b")
                                        ?? column.LookupParameter("Width")
                                        ?? column.LookupParameter("w");
                                    if (widthParam != null && !widthParam.IsReadOnly)
                                        widthParam.Set(colData.Width.Value / 304.8);
                                }

                                if (colData.Depth.HasValue && colData.Depth.Value > 0)
                                {
                                    Parameter depthParam = column.LookupParameter("h")
                                        ?? column.LookupParameter("Depth")
                                        ?? column.LookupParameter("d");
                                    if (depthParam != null && !depthParam.IsReadOnly)
                                        depthParam.Set(colData.Depth.Value / 304.8);
                                }

                                createdColumns.Add(new StructuralColumnResult
                                {
                                    ElementId = column.Id.GetIntValue(),
                                    FamilyName = columnSymbol.FamilyName,
                                    TypeName = columnSymbol.Name,
                                    BaseLevelName = baseLevel.Name,
                                    TopLevelName = topLevel.Name,
                                    LocationX = colData.LocationPoint.X,
                                    LocationY = colData.LocationPoint.Y
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _warnings.Add($"Failed to create column at ({colData.LocationPoint.X}, {colData.LocationPoint.Y}): {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                string message = $"Successfully created {createdColumns.Count} structural column(s).";
                if (_warnings.Count > 0)
                {
                    message += "\n\n⚠ Warnings:\n  • " + string.Join("\n  • ", _warnings);
                }

                Result = new AIResult<List<StructuralColumnResult>>
                {
                    Success = true,
                    Message = message,
                    Response = createdColumns
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<StructuralColumnResult>>
                {
                    Success = false,
                    Message = $"Failed to create structural columns: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create structural columns: {ex.Message}");
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
            return "Create Structural Columns";
        }
    }

    /// <summary>
    /// Result model for individual structural column creation
    /// </summary>
    public class StructuralColumnResult
    {
        [Newtonsoft.Json.JsonProperty("elementId")]
        public int ElementId { get; set; }

        [Newtonsoft.Json.JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [Newtonsoft.Json.JsonProperty("typeName")]
        public string TypeName { get; set; }

        [Newtonsoft.Json.JsonProperty("baseLevelName")]
        public string BaseLevelName { get; set; }

        [Newtonsoft.Json.JsonProperty("topLevelName")]
        public string TopLevelName { get; set; }

        [Newtonsoft.Json.JsonProperty("locationX")]
        public double LocationX { get; set; }

        [Newtonsoft.Json.JsonProperty("locationY")]
        public double LocationY { get; set; }
    }
}

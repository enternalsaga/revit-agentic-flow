using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services
{
    public class CreateSlopedRoofEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<SlopedRoofData> RoofsData { get; private set; }
        public AIResult<List<SlopedRoofResult>> Result { get; private set; }
        private List<string> _warnings = new List<string>();

        public void SetParameters(List<SlopedRoofData> data)
        {
            RoofsData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var createdRoofs = new List<SlopedRoofResult>();
                _warnings.Clear();

                foreach (var roofData in RoofsData)
                {
                    try
                    {
                        // Step 1: Find roof type
                        RoofType roofType = null;

                        if (roofData.TypeId > 0)
                        {
                            ElementId typeEleId = RevitIdUtils.ToElementId(roofData.TypeId);
                            Element typeEle = doc.GetElement(typeEleId);
                            if (typeEle is RoofType rt)
                            {
                                roofType = rt;
                            }
                            else
                            {
                                _warnings.Add($"Requested roof typeId {roofData.TypeId} is not valid. Using default.");
                            }
                        }

                        if (roofType == null)
                        {
                            roofType = new FilteredElementCollector(doc)
                                .OfClass(typeof(RoofType))
                                .OfCategory(BuiltInCategory.OST_Roofs)
                                .Cast<RoofType>()
                                .FirstOrDefault();

                            if (roofType == null)
                            {
                                _warnings.Add("No roof types available in the project. Skipping roof.");
                                continue;
                            }
                        }

                        // Step 2: Find base level
                        Level baseLevel = doc.FindNearestLevel(roofData.BaseLevelElevation / 304.8);
                        if (baseLevel == null)
                        {
                            _warnings.Add($"Could not find level near elevation {roofData.BaseLevelElevation}mm. Skipping roof '{roofData.Name}'.");
                            continue;
                        }

                        // Step 3: Build boundary curves
                        CurveArray roofCurves = new CurveArray();
                        foreach (var edge in roofData.Boundary)
                        {
                            XYZ p0 = new XYZ(
                                edge.P0.X / 304.8,
                                edge.P0.Y / 304.8,
                                edge.P0.Z / 304.8
                            );
                            XYZ p1 = new XYZ(
                                edge.P1.X / 304.8,
                                edge.P1.Y / 304.8,
                                edge.P1.Z / 304.8
                            );
                            roofCurves.Append(Line.CreateBound(p0, p1));
                        }

                        // Step 4: Create FootPrintRoof
                        using (Transaction trans = new Transaction(doc, $"Create Sloped Roof: {roofData.Name}"))
                        {
                            trans.Start();

                            ModelCurveArray modelCurves = new ModelCurveArray();
                            FootPrintRoof roof = doc.Create.NewFootPrintRoof(
                                roofCurves, baseLevel, roofType, out modelCurves);

                            if (roof != null)
                            {
                                // Step 5: Set slope per edge
                                int curveIndex = 0;
                                foreach (ModelCurve mc in modelCurves)
                                {
                                    if (curveIndex < roofData.Boundary.Count)
                                    {
                                        var edgeDef = roofData.Boundary[curveIndex];

                                        roof.set_DefinesSlope(mc, edgeDef.DefinesSlope);

                                        if (edgeDef.DefinesSlope && Math.Abs(edgeDef.SlopeAngle) > 0.001)
                                        {
                                            // Convert degrees to slope (rise/12" run for Revit internal)
                                            // Revit uses slope as rise per foot of run
                                            double slopeRadians = edgeDef.SlopeAngle * Math.PI / 180.0;
                                            double slopeValue = Math.Tan(slopeRadians) * 12.0; // rise per 12" run
                                            roof.set_SlopeAngle(mc, slopeRadians);
                                        }
                                    }
                                    curveIndex++;
                                }

                                // Step 6: Set base offset
                                double baseOffsetFt = roofData.BaseOffset / 304.8;
                                Parameter offsetParam = roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM);
                                if (offsetParam != null)
                                {
                                    offsetParam.Set(baseOffsetFt);
                                }

                                // Step 7: Set overhang if specified
                                if (roofData.Overhang > 0)
                                {
                                    double overhangFt = roofData.Overhang / 304.8;
                                    foreach (ModelCurve mc in modelCurves)
                                    {
                                        roof.set_Overhang(mc, overhangFt);
                                    }
                                }

                                createdRoofs.Add(new SlopedRoofResult
                                {
                                    ElementId = roof.Id.GetIntValue(),
                                    Name = roofData.Name,
                                    RoofTypeName = roofType.Name,
                                    BaseLevelName = baseLevel.Name,
                                    EdgeCount = roofData.Boundary.Count,
                                    SlopedEdgeCount = roofData.Boundary.Count(e => e.DefinesSlope)
                                });
                            }
                            else
                            {
                                _warnings.Add($"Failed to create roof '{roofData.Name}' — NewFootPrintRoof returned null.");
                            }

                            trans.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        _warnings.Add($"Failed to create roof '{roofData.Name}': {ex.Message}");
                    }
                }

                string message = $"Successfully created {createdRoofs.Count} sloped roof(s).";
                if (_warnings.Count > 0)
                {
                    message += "\n\n⚠ Warnings:\n  • " + string.Join("\n  • ", _warnings);
                }

                Result = new AIResult<List<SlopedRoofResult>>
                {
                    Success = true,
                    Message = message,
                    Response = createdRoofs
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<SlopedRoofResult>>
                {
                    Success = false,
                    Message = $"Failed to create sloped roofs: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create sloped roofs: {ex.Message}");
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
            return "Create Sloped Roofs";
        }
    }

    /// <summary>
    /// Result model for individual sloped roof creation
    /// </summary>
    public class SlopedRoofResult
    {
        [Newtonsoft.Json.JsonProperty("elementId")]
        public int ElementId { get; set; }

        [Newtonsoft.Json.JsonProperty("name")]
        public string Name { get; set; }

        [Newtonsoft.Json.JsonProperty("roofTypeName")]
        public string RoofTypeName { get; set; }

        [Newtonsoft.Json.JsonProperty("baseLevelName")]
        public string BaseLevelName { get; set; }

        [Newtonsoft.Json.JsonProperty("edgeCount")]
        public int EdgeCount { get; set; }

        [Newtonsoft.Json.JsonProperty("slopedEdgeCount")]
        public int SlopedEdgeCount { get; set; }
    }
}

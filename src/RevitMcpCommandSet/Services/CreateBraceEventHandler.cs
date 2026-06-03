using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services
{
    public class CreateBraceEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<BraceData> BracesData { get; private set; }
        public AIResult<List<int>> Result { get; private set; }
        private List<string> _warnings = new List<string>();

        public void SetParameters(List<BraceData> data)
        {
            BracesData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementIds = new List<int>();
                _warnings.Clear();

                using (Transaction trans = new Transaction(doc, "Create Structural Braces"))
                {
                    trans.Start();

                    foreach (var braceData in BracesData)
                    {
                        try
                        {
                            // Step 1: Find or default the framing family symbol
                            FamilySymbol braceSymbol = null;

                            if (braceData.TypeId > 0)
                            {
                                ElementId typeEleId = RevitIdUtils.ToElementId(braceData.TypeId);
                                Element typeEle = doc.GetElement(typeEleId);
                                if (typeEle is FamilySymbol fs)
                                {
                                    braceSymbol = fs;
                                }
                                else
                                {
                                    _warnings.Add($"Requested typeId {braceData.TypeId} is not a valid FamilySymbol. Using default.");
                                }
                            }

                            if (braceSymbol == null)
                            {
                                braceSymbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault();

                                if (braceSymbol == null)
                                {
                                    _warnings.Add("No structural framing families loaded in the project. Skipping brace.");
                                    continue;
                                }
                            }

                            if (!braceSymbol.IsActive)
                                braceSymbol.Activate();

                            // Step 2: Find base level
                            Level baseLevel = doc.FindNearestLevel(braceData.BaseLevelElevation / 304.8);
                            if (baseLevel == null)
                            {
                                _warnings.Add($"Could not find level near elevation {braceData.BaseLevelElevation}mm. Skipping brace.");
                                continue;
                            }

                            // Step 3: Create the brace line
                            XYZ startPt = JZPoint.ToXYZ(braceData.StartPoint);
                            XYZ endPt = JZPoint.ToXYZ(braceData.EndPoint);
                            Line braceLine = Line.CreateBound(startPt, endPt);

                            // Step 4: Create the brace as a structural framing instance with Brace type
                            FamilyInstance brace = doc.Create.NewFamilyInstance(
                                braceLine,
                                braceSymbol,
                                baseLevel,
                                StructuralType.Brace
                            );

                            if (brace != null)
                            {
                                elementIds.Add(brace.Id.GetIntValue());
                            }
                        }
                        catch (Exception ex)
                        {
                            _warnings.Add($"Failed to create brace: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                string message = $"Successfully created {elementIds.Count} structural brace(s).";
                if (_warnings.Count > 0)
                {
                    message += "\n\n⚠ Warnings:\n  • " + string.Join("\n  • ", _warnings);
                }

                Result = new AIResult<List<int>>
                {
                    Success = true,
                    Message = message,
                    Response = elementIds
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<int>>
                {
                    Success = false,
                    Message = $"Failed to create structural braces: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create structural braces: {ex.Message}");
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
            return "Create Structural Braces";
        }
    }
}

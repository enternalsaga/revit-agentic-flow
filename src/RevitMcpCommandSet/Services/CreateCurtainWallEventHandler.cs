using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services
{
    public class CreateCurtainWallEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public List<CurtainWallData> WallsData { get; private set; }
        public AIResult<List<int>> Result { get; private set; }
        private List<string> _warnings = new List<string>();

        public void SetParameters(List<CurtainWallData> data)
        {
            WallsData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementIds = new List<int>();
                _warnings.Clear();

                using (Transaction trans = new Transaction(doc, "Create Curtain Walls"))
                {
                    trans.Start();

                    foreach (var wallData in WallsData)
                    {
                        try
                        {
                            // Step 1: Find or default the curtain wall type
                            WallType curtainWallType = null;

                            if (wallData.TypeId > 0)
                            {
                                ElementId typeEleId = RevitIdUtils.ToElementId(wallData.TypeId);
                                Element typeEle = doc.GetElement(typeEleId);
                                if (typeEle is WallType wt && wt.Kind == WallKind.Curtain)
                                {
                                    curtainWallType = wt;
                                }
                                else
                                {
                                    _warnings.Add($"Requested typeId {wallData.TypeId} is not a valid curtain wall type. Using default.");
                                }
                            }

                            if (curtainWallType == null)
                            {
                                curtainWallType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(wt => wt.Kind == WallKind.Curtain);

                                if (curtainWallType == null)
                                {
                                    _warnings.Add("No curtain wall types available in the project. Skipping.");
                                    continue;
                                }
                            }

                            // Step 2: Find base level
                            Level baseLevel = doc.FindNearestLevel(wallData.BaseLevel / 304.8);
                            if (baseLevel == null)
                            {
                                _warnings.Add($"Could not find level near elevation {wallData.BaseLevel}mm. Skipping.");
                                continue;
                            }

                            // Step 3: Create the baseline
                            XYZ startPt = JZPoint.ToXYZ(wallData.StartPoint);
                            XYZ endPt = JZPoint.ToXYZ(wallData.EndPoint);
                            Line baseline = Line.CreateBound(startPt, endPt);

                            // Step 4: Calculate base offset (mm -> ft)
                            double baseOffset = (wallData.BaseOffset + wallData.BaseLevel) / 304.8 - baseLevel.Elevation;

                            // Step 5: Create the wall
                            Wall wall = Wall.Create(
                                doc,
                                baseline,
                                curtainWallType.Id,
                                baseLevel.Id,
                                wallData.Height / 304.8,
                                baseOffset,
                                false,
                                false
                            );

                            if (wall != null)
                            {
                                elementIds.Add(wall.Id.GetIntValue());
                            }
                        }
                        catch (Exception ex)
                        {
                            _warnings.Add($"Failed to create curtain wall: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                string message = $"Successfully created {elementIds.Count} curtain wall(s).";
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
                    Message = $"Failed to create curtain walls: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create curtain walls: {ex.Message}");
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
            return "Create Curtain Walls";
        }
    }
}

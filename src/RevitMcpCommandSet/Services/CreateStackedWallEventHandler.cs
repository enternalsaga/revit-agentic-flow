using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services
{
    public class CreateStackedWallEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private readonly List<string> _warnings = new List<string>();

        public List<StackedWallData> WallsData { get; private set; }
        public AIResult<List<int>> Result { get; private set; }

        public void SetParameters(List<StackedWallData> data)
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

                using (Transaction trans = new Transaction(doc, "Create Stacked Walls"))
                {
                    trans.Start();

                    foreach (var wallData in WallsData)
                    {
                        try
                        {
                            WallType stackedWallType = ResolveStackedWallType(wallData);
                            if (stackedWallType == null)
                            {
                                _warnings.Add("No stacked wall type found. Create/load a native Stacked Wall type in Revit first; command did not fall back to separate walls.");
                                continue;
                            }

                            Level baseLevel = doc.FindNearestLevel(wallData.BaseLevel / 304.8);
                            if (baseLevel == null)
                            {
                                _warnings.Add($"Could not find level near elevation {wallData.BaseLevel}mm. Skipping stacked wall.");
                                continue;
                            }

                            XYZ startPt = JZPoint.ToXYZ(wallData.StartPoint);
                            XYZ endPt = JZPoint.ToXYZ(wallData.EndPoint);
                            if (startPt.IsAlmostEqualTo(endPt))
                            {
                                _warnings.Add("Stacked wall startPoint and endPoint are identical. Skipping.");
                                continue;
                            }

                            double baseOffset = (wallData.BaseOffset + wallData.BaseLevel) / 304.8 - baseLevel.Elevation;
                            Line baseline = Line.CreateBound(startPt, endPt);

                            Wall wall = Wall.Create(
                                doc,
                                baseline,
                                stackedWallType.Id,
                                baseLevel.Id,
                                wallData.Height / 304.8,
                                baseOffset,
                                wallData.Flip,
                                wallData.Structural
                            );

                            if (wall == null)
                            {
                                _warnings.Add($"Revit returned null while creating stacked wall with type '{stackedWallType.Name}'.");
                                continue;
                            }

                            doc.Regenerate();
                            elementIds.Add(wall.Id.GetIntValue());

                            if (!wall.IsStackedWall)
                            {
                                _warnings.Add($"Created wall {wall.Id.GetValue()} with type '{stackedWallType.Name}', but Revit does not report it as stacked.");
                                continue;
                            }

                            IList<ElementId> memberIds = wall.GetStackedWallMemberIds();
                            string memberIdText = memberIds.Count == 0
                                ? "none"
                                : string.Join(", ", memberIds.Select(id => id.GetValue()));
                            _warnings.Add($"Stacked wall {wall.Id.GetValue()} member wall IDs: {memberIdText}.");
                        }
                        catch (Exception ex)
                        {
                            _warnings.Add($"Failed to create stacked wall: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                string message = $"Successfully created {elementIds.Count} stacked wall(s).";
                if (_warnings.Count > 0)
                {
                    message += "\n\nNotes:\n  - " + string.Join("\n  - ", _warnings);
                }

                Result = new AIResult<List<int>>
                {
                    Success = elementIds.Count > 0,
                    Message = message,
                    Response = elementIds
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<int>>
                {
                    Success = false,
                    Message = $"Failed to create stacked walls: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create stacked walls: {ex.Message}");
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
            return "Create Stacked Walls";
        }

        private WallType ResolveStackedWallType(StackedWallData wallData)
        {
            if (wallData.TypeId > 0)
            {
                Element typeElement = doc.GetElement(RevitIdUtils.ToElementId(wallData.TypeId));
                if (typeElement is WallType wallType && wallType.Kind == WallKind.Stacked)
                    return wallType;

                _warnings.Add($"Requested typeId {wallData.TypeId} is not a valid stacked wall type.");
            }

            var stackedTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(wt => wt.Kind == WallKind.Stacked)
                .ToList();

            if (!string.IsNullOrWhiteSpace(wallData.TypeName))
            {
                WallType exact = stackedTypes.FirstOrDefault(wt =>
                    string.Equals(wt.Name, wallData.TypeName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact;

                WallType contains = stackedTypes.FirstOrDefault(wt =>
                    wt.Name.IndexOf(wallData.TypeName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (contains != null)
                    return contains;

                _warnings.Add($"No stacked wall type matched typeName '{wallData.TypeName}'.");
            }

            return stackedTypes.FirstOrDefault();
        }
    }
}

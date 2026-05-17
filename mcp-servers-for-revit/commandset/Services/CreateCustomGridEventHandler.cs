using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CreateCustomGridEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public CustomGridCreationInfo Parameters { get; private set; }
        public AIResult<List<GridCreationResult>> Result { get; private set; }

        public void SetParameters(CustomGridCreationInfo parameters)
        {
            Parameters = parameters;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                if (!Parameters.Validate(out string validationError))
                {
                    Result = new AIResult<List<GridCreationResult>>
                    {
                        Success = false,
                        Message = $"Validation failed: {validationError}",
                        Response = null
                    };
                    return;
                }

                List<GridCreationResult> createdGrids = new List<GridCreationResult>();

                // Get existing grid names for duplicate checking
                var existingGridNames = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .Select(g => g.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                using (Transaction trans = new Transaction(doc, "Create Custom Grid System"))
                {
                    trans.Start();

                    // Create X-axis grids (vertical lines, parallel to Y-axis)
                    foreach (var gridDef in Parameters.XGrids)
                    {
                        string uniqueLabel = GetUniqueGridName(gridDef.Label, existingGridNames);
                        existingGridNames.Add(uniqueLabel);

                        XYZ startPoint = new XYZ(
                            gridDef.Position / 304.8,
                            Parameters.YExtentMin / 304.8,
                            Parameters.Elevation / 304.8
                        );

                        XYZ endPoint = new XYZ(
                            gridDef.Position / 304.8,
                            Parameters.YExtentMax / 304.8,
                            Parameters.Elevation / 304.8
                        );

                        Line gridLine = Line.CreateBound(startPoint, endPoint);
                        Grid grid = Grid.Create(doc, gridLine);
                        grid.Name = uniqueLabel;

#if REVIT2024_OR_GREATER
                        long gridId = grid.Id.Value;
#else
                        long gridId = grid.Id.IntegerValue;
#endif

                        createdGrids.Add(new GridCreationResult
                        {
                            ElementId = gridId,
                            Name = uniqueLabel,
                            OriginalName = gridDef.Label,
                            WasRenamed = gridDef.Label != uniqueLabel,
                            Axis = "X",
                            Position = gridDef.Position
                        });
                    }

                    // Create Y-axis grids (horizontal lines, parallel to X-axis)
                    foreach (var gridDef in Parameters.YGrids)
                    {
                        string uniqueLabel = GetUniqueGridName(gridDef.Label, existingGridNames);
                        existingGridNames.Add(uniqueLabel);

                        XYZ startPoint = new XYZ(
                            Parameters.XExtentMin / 304.8,
                            gridDef.Position / 304.8,
                            Parameters.Elevation / 304.8
                        );

                        XYZ endPoint = new XYZ(
                            Parameters.XExtentMax / 304.8,
                            gridDef.Position / 304.8,
                            Parameters.Elevation / 304.8
                        );

                        Line gridLine = Line.CreateBound(startPoint, endPoint);
                        Grid grid = Grid.Create(doc, gridLine);
                        grid.Name = uniqueLabel;

#if REVIT2024_OR_GREATER
                        long gridId = grid.Id.Value;
#else
                        long gridId = grid.Id.IntegerValue;
#endif

                        createdGrids.Add(new GridCreationResult
                        {
                            ElementId = gridId,
                            Name = uniqueLabel,
                            OriginalName = gridDef.Label,
                            WasRenamed = gridDef.Label != uniqueLabel,
                            Axis = "Y",
                            Position = gridDef.Position
                        });
                    }

                    trans.Commit();
                }

                int renamedCount = createdGrids.Count(g => g.WasRenamed);
                string message = $"Successfully created {createdGrids.Count} grids " +
                                 $"({Parameters.XGrids.Count} X-axis + {Parameters.YGrids.Count} Y-axis)";

                if (renamedCount > 0)
                {
                    message += $". {renamedCount} grid(s) were renamed to avoid duplicates.";
                }

                Result = new AIResult<List<GridCreationResult>>
                {
                    Success = true,
                    Message = message,
                    Response = createdGrids
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<GridCreationResult>>
                {
                    Success = false,
                    Message = $"Failed to create custom grids: {ex.Message}",
                    Response = null
                };
                TaskDialog.Show("Error", $"Failed to create custom grids: {ex.Message}");
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Get unique grid name by auto-incrementing if duplicate exists
        /// </summary>
        private string GetUniqueGridName(string baseName, HashSet<string> existingNames)
        {
            string candidateName = baseName;
            int counter = 1;

            while (existingNames.Contains(candidateName))
            {
                candidateName = $"{baseName}_{counter}";
                counter++;
            }

            return candidateName;
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Create Custom Grid System";
        }
    }
}

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetGridsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetGridsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters()
        {
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                var gridModels = new List<GridModel>();

                var grids = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>();

                foreach (Grid grid in grids)
                {
                    var curve = grid.Curve;
                    var start = curve.GetEndPoint(0);
                    var end = curve.GetEndPoint(1);

                    // Convert feet to mm
                    gridModels.Add(new GridModel
                    {
#if REVIT2024_OR_GREATER
                        Id = grid.Id.Value,
#else
                        Id = grid.Id.IntegerValue,
#endif
                        Name = grid.Name,
                        StartX = start.X * 304.8,
                        StartY = start.Y * 304.8,
                        EndX = end.X * 304.8,
                        EndY = end.Y * 304.8,
                        IsCurved = !(curve is Line)
                    });
                }

                ResultInfo = new GetGridsResult
                {
                    TotalGrids = gridModels.Count,
                    Grids = gridModels.OrderBy(g => g.Name).ToList(),
                    Success = true,
                    Message = $"Found {gridModels.Count} grids"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetGridsResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Grids";
    }
}

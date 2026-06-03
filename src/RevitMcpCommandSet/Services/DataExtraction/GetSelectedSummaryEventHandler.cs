using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetSelectedSummaryEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public AIResult<object> ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;
                var selectedIds = uiDoc.Selection.GetElementIds();

                if (selectedIds.Count == 0)
                {
                    ResultInfo = new AIResult<object> { Success = true, Message = "No elements selected", Response = new { total = 0, summary = new List<object>() } };
                    return;
                }

                var summary = selectedIds.Select(id => doc.GetElement(id))
                    .Where(e => e != null)
                    .GroupBy(e => new { Category = e.Category?.Name ?? "None", Type = e.Name })
                    .Select(g => new
                    {
                        Category = g.Key.Category,
                        Type = g.Key.Type,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                ResultInfo = new AIResult<object>
                {
                    Success = true,
                    Message = $"Summarized {selectedIds.Count} elements.",
                    Response = new
                    {
                        total = selectedIds.Count,
                        summary = summary
                    }
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new AIResult<object> { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Selected Summary";
    }
}

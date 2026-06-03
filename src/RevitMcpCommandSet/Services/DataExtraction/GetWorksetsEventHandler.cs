using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetWorksetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetWorksetsResult ResultInfo { get; private set; }
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
                if (!doc.IsWorkshared)
                {
                    ResultInfo = new GetWorksetsResult
                    {
                        IsWorkshared = false, Success = true,
                        Message = "Project is not workshared"
                    };
                    return;
                }

                var worksets = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .ToWorksets();

                var models = new List<WorksetModel>();
                foreach (var ws in worksets)
                {
                    models.Add(new WorksetModel
                    {
                        Id = ws.Id.IntegerValue,
                        Name = ws.Name,
                        Kind = ws.Kind.ToString(),
                        IsOpen = ws.IsOpen,
                        IsDefault = ws.IsDefaultWorkset,
                        Owner = ws.Owner ?? ""
                    });
                }

                ResultInfo = new GetWorksetsResult
                {
                    IsWorkshared = true,
                    TotalWorksets = models.Count,
                    Worksets = models,
                    Success = true,
                    Message = $"Found {models.Count} worksets"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetWorksetsResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Worksets";
    }
}

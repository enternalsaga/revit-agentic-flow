using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetWarningsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private int _limit;
        public GetWarningsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(int limit = 200)
        {
            _limit = limit;
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
                var warningModels = new List<WarningModel>();
                var warnings = doc.GetWarnings();
                int count = 0;

                foreach (var warning in warnings)
                {
                    if (count >= _limit) break;
                    var elementIds = new List<long>();
                    foreach (var elemId in warning.GetFailingElements())
                    {
#if REVIT2024_OR_GREATER
                        elementIds.Add(elemId.Value);
#else
                        elementIds.Add(elemId.IntegerValue);
#endif
                    }
                    foreach (var elemId in warning.GetAdditionalElements())
                    {
#if REVIT2024_OR_GREATER
                        elementIds.Add(elemId.Value);
#else
                        elementIds.Add(elemId.IntegerValue);
#endif
                    }
                    warningModels.Add(new WarningModel
                    {
                        Description = warning.GetDescriptionText() ?? "",
                        Severity = warning.GetSeverity().ToString(),
                        ElementIds = elementIds
                    });
                    count++;
                }

                ResultInfo = new GetWarningsResult
                {
                    TotalWarnings = warnings.Count(),
                    Warnings = warningModels,
                    Success = true,
                    Message = $"Found {warnings.Count()} warnings (showing {warningModels.Count})"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetWarningsResult
                {
                    Success = false,
                    Message = $"Error getting warnings: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Warnings";
    }
}

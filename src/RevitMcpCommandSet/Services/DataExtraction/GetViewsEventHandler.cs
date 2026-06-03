using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetViewsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private bool _includeTemplates;

        public GetViewsResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(bool includeTemplates = false)
        {
            _includeTemplates = includeTemplates;
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
                var viewModels = new List<Models.DataExtraction.ViewModel>();
                int sheetCount = 0;

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>();

                foreach (View view in views)
                {
                    if (!_includeTemplates && view.IsTemplate) continue;

                    bool isSheet = view is ViewSheet;
                    if (isSheet) sheetCount++;

                    viewModels.Add(new Models.DataExtraction.ViewModel
                    {
#if REVIT2024_OR_GREATER
                        Id = view.Id.Value,
#else
                        Id = view.Id.IntegerValue,
#endif
                        Name = view.Name ?? "",
                        ViewType = view.ViewType.ToString(),
                        Level = view.GenLevel?.Name ?? "",
                        IsTemplate = view.IsTemplate,
                        SheetNumber = isSheet ? ((ViewSheet)view).SheetNumber : null
                    });
                }

                ResultInfo = new GetViewsResult
                {
                    TotalViews = viewModels.Count - sheetCount,
                    TotalSheets = sheetCount,
                    Views = viewModels.OrderBy(v => v.ViewType).ThenBy(v => v.Name).ToList(),
                    Success = true,
                    Message = $"Found {viewModels.Count - sheetCount} views and {sheetCount} sheets"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetViewsResult
                {
                    Success = false,
                    Message = $"Error getting views: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Views";
    }
}

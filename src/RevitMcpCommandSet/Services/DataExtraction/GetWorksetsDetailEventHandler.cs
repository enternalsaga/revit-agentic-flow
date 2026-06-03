using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetWorksetsDetailEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetWorksetsDetailResult ResultInfo { get; private set; }
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
                    ResultInfo = new GetWorksetsDetailResult
                    {
                        IsWorkshared = false, Success = true,
                        Message = "Project is not workshared"
                    };
                    return;
                }

                var worksets = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .ToWorksets()
                    .ToDictionary(ws => ws.Id.IntegerValue, ws => new WorksetDetailModel
                    {
                        Id = ws.Id.IntegerValue,
                        Name = ws.Name
                    });

                // Count elements per workset per category
                var elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (var elem in elements)
                {
                    var wsParam = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (wsParam == null) continue;
                    int wsId = wsParam.AsInteger();
                    if (!worksets.ContainsKey(wsId)) continue;

                    string catName = elem.Category?.Name ?? "Uncategorized";
                    var ws = worksets[wsId];
                    ws.TotalElements++;

                    var existing = ws.Categories.FirstOrDefault(c => c.CategoryName == catName);
                    if (existing != null)
                        existing.Count++;
                    else
                        ws.Categories.Add(new WorksetCategoryCount { CategoryName = catName, Count = 1 });
                }

                // Sort categories
                foreach (var ws in worksets.Values)
                    ws.Categories = ws.Categories.OrderByDescending(c => c.Count).ToList();

                ResultInfo = new GetWorksetsDetailResult
                {
                    IsWorkshared = true,
                    Worksets = worksets.Values.OrderBy(w => w.Name).ToList(),
                    Success = true,
                    Message = $"Analyzed {worksets.Count} worksets"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetWorksetsDetailResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Worksets Detail";
    }
}

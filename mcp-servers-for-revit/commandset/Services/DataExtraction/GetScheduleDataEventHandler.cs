using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetScheduleDataEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _scheduleName;
        public GetScheduleDataResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new(false);

        public void SetParameters(string scheduleName)
        {
            _scheduleName = scheduleName;
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

                var schedule = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .FirstOrDefault(s => s.Name.Equals(_scheduleName, StringComparison.OrdinalIgnoreCase));

                if (schedule == null)
                {
                    ResultInfo = new GetScheduleDataResult
                    {
                        ScheduleName = _scheduleName,
                        Success = false,
                        Message = $"Schedule '{_scheduleName}' not found"
                    };
                    return;
                }

                var tableData = schedule.GetTableData();
                var sectionData = tableData.GetSectionData(SectionType.Body);
                int rows = sectionData.NumberOfRows;
                int cols = sectionData.NumberOfColumns;

                // Extract headers
                var headers = new List<string>();
                var headerSection = tableData.GetSectionData(SectionType.Header);
                if (headerSection != null && headerSection.NumberOfRows > 0)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        try { headers.Add(schedule.GetCellText(SectionType.Header, 0, c)); }
                        catch { headers.Add($"Column {c + 1}"); }
                    }
                }
                else
                {
                    for (int c = 0; c < cols; c++)
                        headers.Add($"Column {c + 1}");
                }

                // Extract rows
                var dataRows = new List<List<string>>();
                for (int r = 0; r < rows; r++)
                {
                    var row = new List<string>();
                    for (int c = 0; c < cols; c++)
                    {
                        try { row.Add(schedule.GetCellText(SectionType.Body, r, c)); }
                        catch { row.Add(""); }
                    }
                    dataRows.Add(row);
                }

                ResultInfo = new GetScheduleDataResult
                {
                    ScheduleName = _scheduleName,
                    Headers = headers,
                    Rows = dataRows,
                    TotalRows = rows,
                    Success = true,
                    Message = $"Extracted {rows} rows from '{_scheduleName}'"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetScheduleDataResult
                {
                    ScheduleName = _scheduleName,
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Schedule Data";
    }
}

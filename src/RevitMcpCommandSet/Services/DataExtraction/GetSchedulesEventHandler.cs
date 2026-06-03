using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetSchedulesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public GetSchedulesResult ResultInfo { get; private set; }
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
                var scheduleModels = new List<ScheduleModel>();

                var schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTemplate);

                foreach (ViewSchedule schedule in schedules)
                {
                    string schedType = "Schedule";
                    if (schedule.Definition.IsKeySchedule) schedType = "Key Schedule";
                    else if (schedule.IsTitleblockRevisionSchedule) schedType = "Revision Schedule";

                    scheduleModels.Add(new ScheduleModel
                    {
#if REVIT2024_OR_GREATER
                        Id = schedule.Id.Value,
#else
                        Id = schedule.Id.IntegerValue,
#endif
                        Name = schedule.Name,
                        ScheduleType = schedType
                    });
                }

                ResultInfo = new GetSchedulesResult
                {
                    TotalSchedules = scheduleModels.Count,
                    Schedules = scheduleModels.OrderBy(s => s.Name).ToList(),
                    Success = true,
                    Message = $"Found {scheduleModels.Count} schedules"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetSchedulesResult { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { TaskCompleted = true; _resetEvent.Set(); }
        }

        public string GetName() => "Get Schedules";
    }
}

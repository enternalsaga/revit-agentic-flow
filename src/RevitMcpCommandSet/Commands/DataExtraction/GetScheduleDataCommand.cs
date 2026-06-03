using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetScheduleDataCommand : ExternalEventCommandBase
    {
        private GetScheduleDataEventHandler _handler => (GetScheduleDataEventHandler)Handler;
        public override string CommandName => "get_schedule_data";

        public GetScheduleDataCommand(UIApplication uiApp)
            : base(new GetScheduleDataEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string scheduleName = parameters?["scheduleName"]?.Value<string>();
                if (string.IsNullOrEmpty(scheduleName))
                    throw new ArgumentException("scheduleName is required");

                _handler.SetParameters(scheduleName);
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get schedule data operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get schedule data: {ex.Message}"); }
        }
    }
}

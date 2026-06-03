using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetSchedulesCommand : ExternalEventCommandBase
    {
        private GetSchedulesEventHandler _handler => (GetSchedulesEventHandler)Handler;
        public override string CommandName => "get_schedules";

        public GetSchedulesCommand(UIApplication uiApp)
            : base(new GetSchedulesEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get schedules operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get schedules: {ex.Message}"); }
        }
    }
}

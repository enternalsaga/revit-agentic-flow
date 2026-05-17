using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetSelectedSummaryCommand : ExternalEventCommandBase
    {
        private GetSelectedSummaryEventHandler _handler => (GetSelectedSummaryEventHandler)Handler;
        public override string CommandName => "get_selected_summary";

        public GetSelectedSummaryCommand(UIApplication uiApp)
            : base(new GetSelectedSummaryEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get selected summary operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get selected summary: {ex.Message}"); }
        }
    }
}

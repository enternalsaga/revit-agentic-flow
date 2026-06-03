using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetWorksetsDetailCommand : ExternalEventCommandBase
    {
        private GetWorksetsDetailEventHandler _handler => (GetWorksetsDetailEventHandler)Handler;
        public override string CommandName => "get_worksets_detail";

        public GetWorksetsDetailCommand(UIApplication uiApp)
            : base(new GetWorksetsDetailEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get worksets detail operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get worksets detail: {ex.Message}"); }
        }
    }
}

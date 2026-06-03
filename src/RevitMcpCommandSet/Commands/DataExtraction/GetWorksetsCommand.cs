using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetWorksetsCommand : ExternalEventCommandBase
    {
        private GetWorksetsEventHandler _handler => (GetWorksetsEventHandler)Handler;
        public override string CommandName => "get_worksets";

        public GetWorksetsCommand(UIApplication uiApp)
            : base(new GetWorksetsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get worksets operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get worksets: {ex.Message}"); }
        }
    }
}

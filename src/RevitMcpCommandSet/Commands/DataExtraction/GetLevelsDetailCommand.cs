using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetLevelsDetailCommand : ExternalEventCommandBase
    {
        private GetLevelsDetailEventHandler _handler => (GetLevelsDetailEventHandler)Handler;
        public override string CommandName => "get_levels_detail";

        public GetLevelsDetailCommand(UIApplication uiApp)
            : base(new GetLevelsDetailEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get levels detail operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get levels detail: {ex.Message}"); }
        }
    }
}

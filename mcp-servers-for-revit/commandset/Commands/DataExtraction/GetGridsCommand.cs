using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetGridsCommand : ExternalEventCommandBase
    {
        private GetGridsEventHandler _handler => (GetGridsEventHandler)Handler;
        public override string CommandName => "get_grids";

        public GetGridsCommand(UIApplication uiApp)
            : base(new GetGridsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get grids operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get grids: {ex.Message}"); }
        }
    }
}

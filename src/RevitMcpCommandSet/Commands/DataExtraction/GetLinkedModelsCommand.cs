using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetLinkedModelsCommand : ExternalEventCommandBase
    {
        private GetLinkedModelsEventHandler _handler => (GetLinkedModelsEventHandler)Handler;
        public override string CommandName => "get_linked_models";

        public GetLinkedModelsCommand(UIApplication uiApp)
            : base(new GetLinkedModelsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Get linked models operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to get linked models: {ex.Message}"); }
        }
    }
}

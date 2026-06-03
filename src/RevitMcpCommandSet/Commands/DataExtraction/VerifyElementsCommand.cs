using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;
using System.Linq;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class VerifyElementsCommand : ExternalEventCommandBase
    {
        private VerifyElementsEventHandler _handler => (VerifyElementsEventHandler)Handler;
        public override string CommandName => "verify_elements";

        public VerifyElementsCommand(UIApplication uiApp)
            : base(new VerifyElementsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var elementIdsArray = parameters?["elementIds"] as JArray;
                if (elementIdsArray == null || !elementIdsArray.Any())
                    throw new ArgumentException("elementIds is required");

                var ids = elementIdsArray.Select(t => t.Value<long>()).ToList();

                _handler.SetParameters(ids);
                if (RaiseAndWaitForCompletion(15000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Verify elements operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to verify elements: {ex.Message}"); }
        }
    }
}

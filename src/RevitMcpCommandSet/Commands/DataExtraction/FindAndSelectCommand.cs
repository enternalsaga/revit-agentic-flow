using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class FindAndSelectCommand : ExternalEventCommandBase
    {
        private FindAndSelectEventHandler _handler => (FindAndSelectEventHandler)Handler;
        public override string CommandName => "find_and_select";

        public FindAndSelectCommand(UIApplication uiApp)
            : base(new FindAndSelectEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters?["category"]?.Value<string>();
                string parameterName = parameters?["parameterName"]?.Value<string>();
                string parameterValue = parameters?["parameterValue"]?.Value<string>();
                bool isolate = parameters?["isolate"]?.Value<bool>() ?? false;

                _handler.SetParameters(category, parameterName, parameterValue, isolate);
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Find and select operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to find and select: {ex.Message}"); }
        }
    }
}

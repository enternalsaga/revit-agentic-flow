using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class SetParameterBulkCommand : ExternalEventCommandBase
    {
        private SetParameterBulkEventHandler _handler => (SetParameterBulkEventHandler)Handler;
        public override string CommandName => "set_parameter_bulk";

        public SetParameterBulkCommand(UIApplication uiApp)
            : base(new SetParameterBulkEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var elementIdsArray = parameters?["elementIds"] as JArray;
                string parameterName = parameters?["parameterName"]?.Value<string>();
                string valueStr = parameters?["value"]?.Value<string>();

                if (elementIdsArray == null || string.IsNullOrEmpty(parameterName))
                    throw new ArgumentException("elementIds and parameterName are required");

                var ids = elementIdsArray.Select(t => t.Value<long>()).ToList();

                _handler.SetParameters(ids, parameterName, valueStr);
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Set parameter bulk operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to bulk set parameters: {ex.Message}"); }
        }
    }
}

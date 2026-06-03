using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class SetElementParameterCommand : ExternalEventCommandBase
    {
        private SetElementParameterEventHandler _handler => (SetElementParameterEventHandler)Handler;

        public override string CommandName => "set_element_parameter";

        public SetElementParameterCommand(UIApplication uiApp)
            : base(new SetElementParameterEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long elementId = parameters?["elementId"]?.Value<long>() ?? -1;
                string parameterName = parameters?["parameterName"]?.Value<string>();
                var value = parameters?["value"];

                if (elementId <= 0)
                    throw new ArgumentException("elementId is required and must be positive");
                if (string.IsNullOrEmpty(parameterName))
                    throw new ArgumentException("parameterName is required");
                if (value == null)
                    throw new ArgumentException("value is required");

                _handler.SetParameters(elementId, parameterName, value);

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Set element parameter operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set element parameter: {ex.Message}");
            }
        }
    }
}

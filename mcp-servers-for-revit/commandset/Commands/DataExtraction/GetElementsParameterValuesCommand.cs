using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetElementsParameterValuesCommand : ExternalEventCommandBase
    {
        private GetElementsParameterValuesEventHandler _handler => (GetElementsParameterValuesEventHandler)Handler;

        public override string CommandName => "get_elements_parameter_values";

        public GetElementsParameterValuesCommand(UIApplication uiApp)
            : base(new GetElementsParameterValuesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters?["category"]?.Value<string>();
                string parameterName = parameters?["parameterName"]?.Value<string>();
                int limit = parameters?["limit"]?.Value<int>() ?? 500;

                if (string.IsNullOrEmpty(category))
                    throw new ArgumentException("category is required");
                if (string.IsNullOrEmpty(parameterName))
                    throw new ArgumentException("parameterName is required");

                _handler.SetParameters(category, parameterName, limit);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get elements parameter values operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get elements parameter values: {ex.Message}");
            }
        }
    }
}

using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetElementParametersCommand : ExternalEventCommandBase
    {
        private GetElementParametersEventHandler _handler => (GetElementParametersEventHandler)Handler;

        public override string CommandName => "get_element_parameters";

        public GetElementParametersCommand(UIApplication uiApp)
            : base(new GetElementParametersEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long elementId = parameters?["elementId"]?.Value<long>() ?? -1;
                bool includeReadOnly = parameters?["includeReadOnly"]?.Value<bool>() ?? true;

                if (elementId <= 0)
                    throw new ArgumentException("elementId is required and must be positive");

                _handler.SetParameters(elementId, includeReadOnly);

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get element parameters operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get element parameters: {ex.Message}");
            }
        }
    }
}

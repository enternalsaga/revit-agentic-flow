using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetWarningsCommand : ExternalEventCommandBase
    {
        private GetWarningsEventHandler _handler => (GetWarningsEventHandler)Handler;

        public override string CommandName => "get_warnings";

        public GetWarningsCommand(UIApplication uiApp)
            : base(new GetWarningsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int limit = parameters?["limit"]?.Value<int>() ?? 200;
                _handler.SetParameters(limit);

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get warnings operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get warnings: {ex.Message}");
            }
        }
    }
}

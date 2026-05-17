using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetViewsCommand : ExternalEventCommandBase
    {
        private GetViewsEventHandler _handler => (GetViewsEventHandler)Handler;

        public override string CommandName => "get_views";

        public GetViewsCommand(UIApplication uiApp)
            : base(new GetViewsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                bool includeTemplates = parameters?["includeTemplates"]?.Value<bool>() ?? false;
                _handler.SetParameters(includeTemplates);

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get views operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get views: {ex.Message}");
            }
        }
    }
}

using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class ToggleRevitLinksCommand : ExternalEventCommandBase
    {
        private ToggleRevitLinksEventHandler _handler => (ToggleRevitLinksEventHandler)Handler;
        public override string CommandName => "toggle_revit_links";

        public ToggleRevitLinksCommand(UIApplication uiApp)
            : base(new ToggleRevitLinksEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                bool visible = parameters?["visible"]?.Value<bool>() ?? true;

                _handler.SetParameters(visible);
                if (RaiseAndWaitForCompletion(15000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Toggle Revit links operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to toggle Revit links: {ex.Message}"); }
        }
    }
}

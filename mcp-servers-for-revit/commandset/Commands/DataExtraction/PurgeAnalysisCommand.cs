using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class PurgeAnalysisCommand : ExternalEventCommandBase
    {
        private PurgeAnalysisEventHandler _handler => (PurgeAnalysisEventHandler)Handler;
        public override string CommandName => "purge_analysis";

        public PurgeAnalysisCommand(UIApplication uiApp)
            : base(new PurgeAnalysisEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                bool analyzeOnly = parameters?["analyzeOnly"]?.Value<bool>() ?? true;

                _handler.SetParameters(analyzeOnly);
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Purge analysis operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to purge analysis: {ex.Message}"); }
        }
    }
}

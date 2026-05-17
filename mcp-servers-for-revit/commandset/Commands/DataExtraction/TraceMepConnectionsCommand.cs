using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class TraceMepConnectionsCommand : ExternalEventCommandBase
    {
        private TraceMepConnectionsEventHandler _handler => (TraceMepConnectionsEventHandler)Handler;
        public override string CommandName => "trace_mep_connections";

        public TraceMepConnectionsCommand(UIApplication uiApp)
            : base(new TraceMepConnectionsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? elementId = parameters?["elementId"]?.Value<long>();
                int maxDepth = parameters?["maxDepth"]?.Value<int>() ?? 50;

                if (!elementId.HasValue) throw new ArgumentException("elementId is required");

                _handler.SetParameters(elementId.Value, maxDepth);
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Trace MEP connections operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to trace MEP connections: {ex.Message}"); }
        }
    }
}

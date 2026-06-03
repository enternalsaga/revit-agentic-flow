using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class CountPipeSprinklersCommand : ExternalEventCommandBase
    {
        private CountPipeSprinklersEventHandler _handler => (CountPipeSprinklersEventHandler)Handler;
        public override string CommandName => "count_pipe_sprinklers";

        public CountPipeSprinklersCommand(UIApplication uiApp)
            : base(new CountPipeSprinklersEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? pipeId = parameters?["pipeId"]?.Value<long>();

                if (!pipeId.HasValue) throw new ArgumentException("pipeId is required");

                _handler.SetParameters(pipeId.Value);
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Count pipe sprinklers operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to count pipe sprinklers: {ex.Message}"); }
        }
    }
}

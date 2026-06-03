using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetElementsSummaryCommand : ExternalEventCommandBase
    {
        private GetElementsSummaryEventHandler _handler => (GetElementsSummaryEventHandler)Handler;

        public override string CommandName => "get_elements_summary";

        public GetElementsSummaryCommand(UIApplication uiApp)
            : base(new GetElementsSummaryEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters?["category"]?.Value<string>();
                string parameterName = parameters?["parameterName"]?.Value<string>();
                string aggregation = parameters?["aggregation"]?.Value<string>() ?? "count";

                if (string.IsNullOrEmpty(category))
                    throw new ArgumentException("category is required");

                _handler.SetParameters(category, parameterName, aggregation);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get elements summary operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get elements summary: {ex.Message}");
            }
        }
    }
}

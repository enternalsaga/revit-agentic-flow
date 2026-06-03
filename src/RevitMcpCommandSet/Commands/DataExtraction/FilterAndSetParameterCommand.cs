using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class FilterAndSetParameterCommand : ExternalEventCommandBase
    {
        private FilterAndSetParameterEventHandler _handler => (FilterAndSetParameterEventHandler)Handler;
        public override string CommandName => "filter_and_set_parameter";

        public FilterAndSetParameterCommand(UIApplication uiApp)
            : base(new FilterAndSetParameterEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters?["category"]?.Value<string>();
                string filterParam = parameters?["filterParameter"]?.Value<string>();
                string filterValue = parameters?["filterValue"]?.Value<string>();
                string setParam = parameters?["setParameter"]?.Value<string>();
                string setValue = parameters?["setValue"]?.Value<string>();
                bool dryRun = parameters?["dryRun"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(filterParam) || string.IsNullOrEmpty(setParam))
                    throw new ArgumentException("filterParameter and setParameter are required");

                _handler.SetParameters(category, filterParam, filterValue, setParam, setValue, dryRun);
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Filter and set operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to filter and set: {ex.Message}"); }
        }
    }
}

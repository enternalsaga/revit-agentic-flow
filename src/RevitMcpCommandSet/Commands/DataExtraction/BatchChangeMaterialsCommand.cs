using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class BatchChangeMaterialsCommand : ExternalEventCommandBase
    {
        private BatchChangeMaterialsEventHandler _handler => (BatchChangeMaterialsEventHandler)Handler;
        public override string CommandName => "batch_change_materials";

        public BatchChangeMaterialsCommand(UIApplication uiApp)
            : base(new BatchChangeMaterialsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters?["category"]?.Value<string>();
                string searchMaterialName = parameters?["searchMaterialName"]?.Value<string>();
                string replaceMaterialName = parameters?["replaceMaterialName"]?.Value<string>();
                bool dryRun = parameters?["dryRun"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(searchMaterialName) || string.IsNullOrEmpty(replaceMaterialName))
                    throw new ArgumentException("searchMaterialName and replaceMaterialName are required.");

                _handler.SetParameters(category, searchMaterialName, replaceMaterialName, dryRun);
                if (RaiseAndWaitForCompletion(120000)) // Can be slow for entire project
                    return _handler.ResultInfo;
                throw new TimeoutException("Batch change materials operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to batch change materials: {ex.Message}"); }
        }
    }
}

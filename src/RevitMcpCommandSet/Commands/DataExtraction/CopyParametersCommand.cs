using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class CopyParametersCommand : ExternalEventCommandBase
    {
        private CopyParametersEventHandler _handler => (CopyParametersEventHandler)Handler;
        public override string CommandName => "copy_parameters";

        public CopyParametersCommand(UIApplication uiApp)
            : base(new CopyParametersEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? sourceId = parameters?["sourceElementId"]?.Value<long>();
                var targetIdsArray = parameters?["targetElementIds"] as JArray;
                var paramNamesArray = parameters?["parameterNames"] as JArray;
                bool overwrite = parameters?["overwrite"]?.Value<bool>() ?? true;

                if (!sourceId.HasValue || targetIdsArray == null)
                    throw new ArgumentException("sourceElementId and targetElementIds are required");

                var targetIds = targetIdsArray.Select(t => t.Value<long>()).ToList();
                var paramNames = paramNamesArray?.Select(t => t.Value<string>()).ToList();

                _handler.SetParameters(sourceId.Value, targetIds, paramNames, overwrite);
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Copy parameters operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to copy parameters: {ex.Message}"); }
        }
    }
}

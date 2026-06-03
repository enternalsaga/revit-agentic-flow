using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class AssignParameterBetweenLevelsCommand : ExternalEventCommandBase
    {
        private AssignParameterBetweenLevelsEventHandler _handler => (AssignParameterBetweenLevelsEventHandler)Handler;
        public override string CommandName => "assign_parameter_between_levels";

        public AssignParameterBetweenLevelsCommand(UIApplication uiApp)
            : base(new AssignParameterBetweenLevelsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? lowerLevelId = parameters?["lowerLevelId"]?.Value<long>();
                long? upperLevelId = parameters?["upperLevelId"]?.Value<long>();
                string parameterName = parameters?["parameterName"]?.Value<string>();
                string valueStr = parameters?["value"]?.Value<string>();
                string category = parameters?["category"]?.Value<string>();
                bool dryRun = parameters?["dryRun"]?.Value<bool>() ?? false;

                if (!lowerLevelId.HasValue || !upperLevelId.HasValue || string.IsNullOrEmpty(parameterName))
                    throw new ArgumentException("lowerLevelId, upperLevelId, and parameterName are required");

                _handler.SetParameters(lowerLevelId.Value, upperLevelId.Value, parameterName, valueStr, category, dryRun);
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Assign parameter between levels operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to assign parameter between levels: {ex.Message}"); }
        }
    }
}

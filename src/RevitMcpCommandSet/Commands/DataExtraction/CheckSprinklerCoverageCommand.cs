using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class CheckSprinklerCoverageCommand : ExternalEventCommandBase
    {
        private CheckSprinklerCoverageEventHandler _handler => (CheckSprinklerCoverageEventHandler)Handler;
        public override string CommandName => "check_sprinkler_coverage";

        public CheckSprinklerCoverageCommand(UIApplication uiApp)
            : base(new CheckSprinklerCoverageEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? levelId = parameters?["levelId"]?.Value<long>();
                double coverageRadius = parameters?["coverageRadius"]?.Value<double>() ?? 2000.0;

                if (!levelId.HasValue) throw new ArgumentException("levelId is required");

                _handler.SetParameters(levelId.Value, coverageRadius);
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Check sprinkler coverage operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to check sprinkler coverage: {ex.Message}"); }
        }
    }
}

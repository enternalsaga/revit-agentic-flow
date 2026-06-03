using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class MeasureDistanceCommand : ExternalEventCommandBase
    {
        private MeasureDistanceEventHandler _handler => (MeasureDistanceEventHandler)Handler;
        public override string CommandName => "measure_distance";

        public MeasureDistanceCommand(UIApplication uiApp)
            : base(new MeasureDistanceEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? id1 = parameters?["elementId1"]?.Value<long>();
                long? id2 = parameters?["elementId2"]?.Value<long>();

                _handler.SetParameters(id1, id2);
                if (RaiseAndWaitForCompletion(30000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Measure distance operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to measure distance: {ex.Message}"); }
        }
    }
}

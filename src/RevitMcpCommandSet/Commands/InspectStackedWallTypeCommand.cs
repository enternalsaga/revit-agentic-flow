using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands
{
    public class InspectStackedWallTypeCommand : ExternalEventCommandBase
    {
        private InspectStackedWallTypeEventHandler _handler => (InspectStackedWallTypeEventHandler)Handler;

        public override string CommandName => "inspect_stacked_wall_type";

        public InspectStackedWallTypeCommand(UIApplication uiApp)
            : base(new InspectStackedWallTypeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                StackedWallTypeInspectionInfo data = parameters.ToObject<StackedWallTypeInspectionInfo>() ?? new StackedWallTypeInspectionInfo();
                _handler.SetParameters(data);

                if (RaiseAndWaitForCompletion(15000))
                    return _handler.Result;

                throw new TimeoutException("Stacked wall type inspection operation timed out");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to inspect stacked wall type: {ex.Message}");
            }
        }
    }
}

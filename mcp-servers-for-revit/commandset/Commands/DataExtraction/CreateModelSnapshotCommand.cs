using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class CreateModelSnapshotCommand : ExternalEventCommandBase
    {
        private CreateModelSnapshotEventHandler _handler => (CreateModelSnapshotEventHandler)Handler;
        public override string CommandName => "create_model_snapshot";

        public CreateModelSnapshotCommand(UIApplication uiApp)
            : base(new CreateModelSnapshotEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters?["category"]?.Value<string>();

                _handler.SetParameters(category);
                // Snapshot can take a long time, allow 60 seconds
                if (RaiseAndWaitForCompletion(60000))
                    return _handler.ResultInfo;
                throw new TimeoutException("Create model snapshot operation timed out");
            }
            catch (Exception ex) { throw new Exception($"Failed to create model snapshot: {ex.Message}"); }
        }
    }
}

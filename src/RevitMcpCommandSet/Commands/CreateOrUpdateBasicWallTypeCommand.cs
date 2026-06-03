using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands
{
    public class CreateOrUpdateBasicWallTypeCommand : ExternalEventCommandBase
    {
        private CreateOrUpdateBasicWallTypeEventHandler _handler => (CreateOrUpdateBasicWallTypeEventHandler)Handler;

        public override string CommandName => "create_or_update_basic_wall_type";

        public CreateOrUpdateBasicWallTypeCommand(UIApplication uiApp)
            : base(new CreateOrUpdateBasicWallTypeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                BasicWallTypeCreationInfo data = parameters.ToObject<BasicWallTypeCreationInfo>();
                if (data == null || data.Data == null || data.Data.Count == 0)
                    throw new ArgumentNullException(nameof(data), "Basic wall type creation data is null or empty");

                _handler.SetParameters(data.Data);

                if (RaiseAndWaitForCompletion(15000))
                    return _handler.Result;

                throw new TimeoutException("Basic wall type creation operation timed out");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create or update basic wall types: {ex.Message}");
            }
        }
    }
}

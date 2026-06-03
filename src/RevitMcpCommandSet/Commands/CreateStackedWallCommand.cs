using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands
{
    public class CreateStackedWallCommand : ExternalEventCommandBase
    {
        private CreateStackedWallEventHandler _handler => (CreateStackedWallEventHandler)Handler;

        public override string CommandName => "create_stacked_wall";

        public CreateStackedWallCommand(UIApplication uiApp)
            : base(new CreateStackedWallEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                StackedWallCreationInfo data = parameters.ToObject<StackedWallCreationInfo>();

                if (data == null || data.Data == null || data.Data.Count == 0)
                    throw new ArgumentNullException(nameof(data), "Stacked wall creation data is null or empty");

                _handler.SetParameters(data.Data);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Stacked wall creation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create stacked walls: {ex.Message}");
            }
        }
    }
}

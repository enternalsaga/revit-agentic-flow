using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class CreateCurtainWallCommand : ExternalEventCommandBase
    {
        private CreateCurtainWallEventHandler _handler => (CreateCurtainWallEventHandler)Handler;

        public override string CommandName => "create_curtain_wall";

        public CreateCurtainWallCommand(UIApplication uiApp)
            : base(new CreateCurtainWallEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                CurtainWallCreationInfo data = parameters.ToObject<CurtainWallCreationInfo>();

                if (data == null || data.Data == null || data.Data.Count == 0)
                    throw new ArgumentNullException(nameof(data), "Curtain wall creation data is null or empty");

                _handler.SetParameters(data.Data);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Curtain wall creation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create curtain walls: {ex.Message}");
            }
        }
    }
}

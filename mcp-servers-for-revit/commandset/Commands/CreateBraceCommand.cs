using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class CreateBraceCommand : ExternalEventCommandBase
    {
        private CreateBraceEventHandler _handler => (CreateBraceEventHandler)Handler;

        public override string CommandName => "create_brace";

        public CreateBraceCommand(UIApplication uiApp)
            : base(new CreateBraceEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                BraceCreationInfo data = parameters.ToObject<BraceCreationInfo>();

                if (data == null || data.Data == null || data.Data.Count == 0)
                    throw new ArgumentNullException(nameof(data), "Brace creation data is null or empty");

                _handler.SetParameters(data.Data);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Brace creation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create braces: {ex.Message}");
            }
        }
    }
}

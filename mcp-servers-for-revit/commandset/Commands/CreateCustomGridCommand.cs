using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class CreateCustomGridCommand : ExternalEventCommandBase
    {
        private CreateCustomGridEventHandler _handler => (CreateCustomGridEventHandler)Handler;

        /// <summary>
        /// Command name for MCP protocol
        /// </summary>
        public override string CommandName => "create_custom_grid";

        /// <summary>
        /// Constructor
        /// </summary>
        public CreateCustomGridCommand(UIApplication uiApp)
            : base(new CreateCustomGridEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                CustomGridCreationInfo data = parameters.ToObject<CustomGridCreationInfo>();

                if (data == null)
                    throw new ArgumentNullException(nameof(data), "Custom grid creation data is null");

                if (!data.Validate(out string validationError))
                {
                    throw new ArgumentException($"Invalid custom grid parameters: {validationError}");
                }

                _handler.SetParameters(data);

                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Custom grid creation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create custom grid system: {ex.Message}");
            }
        }
    }
}

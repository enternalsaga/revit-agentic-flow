using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands
{
    public class CreateSlopedRoofCommand : ExternalEventCommandBase
    {
        private CreateSlopedRoofEventHandler _handler => (CreateSlopedRoofEventHandler)Handler;

        /// <summary>
        /// Command name for MCP protocol
        /// </summary>
        public override string CommandName => "create_sloped_roof";

        /// <summary>
        /// Constructor
        /// </summary>
        public CreateSlopedRoofCommand(UIApplication uiApp)
            : base(new CreateSlopedRoofEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                SlopedRoofCreationInfo data = parameters.ToObject<SlopedRoofCreationInfo>();

                if (data == null || data.Data == null || data.Data.Count == 0)
                    throw new ArgumentNullException(nameof(data), "Sloped roof creation data is null or empty");

                _handler.SetParameters(data.Data);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Sloped roof creation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create sloped roofs: {ex.Message}");
            }
        }
    }
}

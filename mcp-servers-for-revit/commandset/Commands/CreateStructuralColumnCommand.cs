using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Architecture;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class CreateStructuralColumnCommand : ExternalEventCommandBase
    {
        private CreateStructuralColumnEventHandler _handler => (CreateStructuralColumnEventHandler)Handler;

        /// <summary>
        /// Command name for MCP protocol
        /// </summary>
        public override string CommandName => "create_structural_column";

        /// <summary>
        /// Constructor
        /// </summary>
        public CreateStructuralColumnCommand(UIApplication uiApp)
            : base(new CreateStructuralColumnEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                StructuralColumnCreationInfo data = parameters.ToObject<StructuralColumnCreationInfo>();

                if (data == null || data.Data == null || data.Data.Count == 0)
                    throw new ArgumentNullException(nameof(data), "Structural column creation data is null or empty");

                _handler.SetParameters(data.Data);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Structural column creation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create structural columns: {ex.Message}");
            }
        }
    }
}

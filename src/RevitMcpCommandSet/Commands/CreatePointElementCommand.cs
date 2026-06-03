using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMcpSdk;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands
{
    public class CreatePointElementCommand :    ExternalEventCommandBase
    {
        private CreatePointElementEventHandler _handler => (CreatePointElementEventHandler)Handler;

        /// <summary>
        /// ????
        /// </summary>
        public override string CommandName => "create_point_based_element";

        /// <summary>
        /// ????
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public CreatePointElementCommand(UIApplication uiApp)
            : base(new CreatePointElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                List<PointElement> data = new List<PointElement>();
                // ????
                data = parameters["data"].ToObject<List<PointElement>>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI??????");

                // ?????????
                _handler.SetParameters(data);

                // ???????????
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("??????????");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"????????: {ex.Message}");
            }
        }
    }

}

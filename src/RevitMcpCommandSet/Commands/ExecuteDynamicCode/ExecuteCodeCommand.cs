using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMcpSdk;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode
{
    /// <summary>
    /// ??????????
    /// </summary>
    public class ExecuteCodeCommand : ExternalEventCommandBase
    {
        private ExecuteCodeEventHandler _handler => (ExecuteCodeEventHandler)Handler;

        public override string CommandName => "send_code_to_revit";

        public ExecuteCodeCommand(UIApplication uiApp)
            : base(new ExecuteCodeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // ????
                if (!parameters.ContainsKey("code"))
                {
                    throw new ArgumentException("Missing required parameter: 'code'");
                }

                // ???????
                string code = parameters["code"].Value<string>();
                JArray parametersArray = parameters["parameters"] as JArray;
                object[] executionParameters = parametersArray?.ToObject<object[]>() ?? Array.Empty<object>();
                string transactionMode = parameters["transactionMode"]?.Value<string>() ?? ExecuteCodeEventHandler.TransactionModeAuto;

                // ??????
                _handler.SetExecutionParameters(code, executionParameters, transactionMode);

                // ???????????
                if (RaiseAndWaitForCompletion(60000)) // 1????
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("??????");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"??????: {ex.Message}", ex);
            }
        }
    }
}

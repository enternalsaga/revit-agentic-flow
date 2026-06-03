using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMcpSdk;
using System;

namespace RevitMCPCommandSet.Commands.Access
{
    public class SwitchViewCommand : ExternalEventCommandBase
    {
        private SwitchViewEventHandler _handler => (SwitchViewEventHandler)Handler;

        public override string CommandName => "switch_view";

        public SwitchViewCommand(UIApplication uiApp)
            : base(new SwitchViewEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            _handler.ViewName = parameters?["viewName"]?.Value<string>();
            _handler.ViewId = parameters?["viewId"]?.Value<int?>();

            if (RaiseAndWaitForCompletion(10000))
            {
                return _handler.Result;
            }
            else
            {
                throw new TimeoutException("Switch view timed out");
            }
        }
    }
}

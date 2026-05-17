using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;
using System;

namespace RevitMCPCommandSet.Commands
{
    public class CreateParametricDoorCommand : ExternalEventCommandBase
    {
        private CreateParametricDoorHandler _handler => (CreateParametricDoorHandler)Handler;

        public override string CommandName => "create_parametric_door";

        public CreateParametricDoorCommand(UIApplication uiApp) 
            : base(new CreateParametricDoorHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                double width = parameters["width"]?.Value<double>() ?? 3.0;
                double height = parameters["height"]?.Value<double>() ?? 7.0;
                int hostWallId = parameters["hostWallId"]?.Value<int>() ?? -1;
                double locationParameter = parameters["locationParameter"]?.Value<double>() ?? 0.5;

                _handler.SetParameters(width, height, hostWallId, locationParameter);

                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Create parametric door operation timed out.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create parametric door: {ex.Message}");
            }
        }
    }
}

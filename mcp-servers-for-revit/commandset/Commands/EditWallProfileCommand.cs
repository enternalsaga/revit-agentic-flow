using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands
{
    public class EditWallProfileCommand : ExternalEventCommandBase
    {
        private EditWallProfileHandler _handler => (EditWallProfileHandler)Handler;

        public override string CommandName => "edit_wall_profile";

        public EditWallProfileCommand(UIApplication uiApp) 
            : base(new EditWallProfileHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int wallId = parameters["wallId"]?.Value<int>() ?? -1;
                JArray pointsArray = parameters["profilePoints"] as JArray;
                
                _handler.SetParameters(wallId, pointsArray);

                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Edit wall profile operation timed out.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to edit wall profile: {ex.Message}");
            }
        }
    }
}

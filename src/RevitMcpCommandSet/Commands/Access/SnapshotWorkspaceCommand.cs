using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Workspace;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access
{
    public class SnapshotWorkspaceCommand : ExternalEventCommandBase
    {
        private WorkspaceSnapshotEventHandler _handler => (WorkspaceSnapshotEventHandler)Handler;

        public override string CommandName => "snapshot_workspace";

        public SnapshotWorkspaceCommand(UIApplication uiApp)
            : base(new WorkspaceSnapshotEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            var request = new WorkspaceSnapshotRequest
            {
                IncludeImage = parameters?["includeImage"]?.Value<bool?>() ?? true,
                IncludeVisibleElements = parameters?["includeVisibleElements"]?.Value<bool?>() ?? true,
                IncludeSelection = parameters?["includeSelection"]?.Value<bool?>() ?? true,
                ElementLimit = parameters?["elementLimit"]?.Value<int?>() ?? 100,
                PixelSize = parameters?["pixelSize"]?.Value<int?>() ?? 1600,
                OutputDirectory = parameters?["outputDirectory"]?.Value<string>()
            };

            _handler.SetParameters(request);

            if (RaiseAndWaitForCompletion(60000))
            {
                return _handler.ResultInfo;
            }

            throw new TimeoutException("Snapshot workspace operation timed out");
        }
    }
}

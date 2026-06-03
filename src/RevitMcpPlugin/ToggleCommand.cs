using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpPlugin.Core;

namespace RevitMcpPlugin;

[Transaction(TransactionMode.Manual)]
public class ToggleCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (App.Service == null)
        {
            App.InitService(commandData.Application);
            App.Service!.Start();
            TaskDialog.Show("Revit MCP", $"Server started on pipe '{PipeService.PipeName}'.");
        }
        else if (App.Service.IsRunning)
        {
            App.StopService();
            TaskDialog.Show("Revit MCP", "Server stopped.");
        }
        else
        {
            App.Service.Start();
            TaskDialog.Show("Revit MCP", $"Server started on pipe '{PipeService.PipeName}'.");
        }

        return Result.Succeeded;
    }
}

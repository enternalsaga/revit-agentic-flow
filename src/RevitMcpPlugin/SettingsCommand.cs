using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpPlugin.Configuration;

namespace RevitMcpPlugin;

[Transaction(TransactionMode.Manual)]
public class SettingsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        TaskDialog.Show("Revit MCP", $"Configuration directory:\n{PathManager.GetCommandsDirectoryPath()}");
        return Result.Succeeded;
    }
}

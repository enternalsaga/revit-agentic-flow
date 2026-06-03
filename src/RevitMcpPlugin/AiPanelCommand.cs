using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpPlugin.AI;

namespace RevitMcpPlugin;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AiPanelCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var pane = commandData.Application.GetDockablePane(AiPanelProvider.PanelId);
            if (pane == null)
            {
                message = "AI Chat panel not found.";
                return Result.Failed;
            }
            if (pane.IsShown()) pane.Hide();
            else pane.Show();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}

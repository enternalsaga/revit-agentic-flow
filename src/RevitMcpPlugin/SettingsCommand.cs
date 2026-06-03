using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpPlugin.Configuration;
using RevitMcpPlugin.UI;

namespace RevitMcpPlugin;

[Transaction(TransactionMode.Manual)]
public class SettingsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var configurationManager = new ConfigurationManager();
        var window = new SettingsWindow(
            configurationManager,
            () => App.Service?.IsRunning == true,
            log =>
            {
                App.InitService(commandData.Application, log);
                App.Service!.Start();
                return App.Service.IsRunning;
            },
            App.StopService);
        var helper = new System.Windows.Interop.WindowInteropHelper(window)
        {
            Owner = commandData.Application.MainWindowHandle
        };

        window.ShowDialog();
        return Result.Succeeded;
    }
}

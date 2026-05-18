using Autodesk.Revit.UI;
using RevitMcpPlugin.Configuration;
using RevitMcpPlugin.Core;

namespace RevitMcpPlugin;

public class App : IExternalApplication
{
    internal static PipeService? Service { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        var panel = application.CreateRibbonPanel("Revit MCP");

        var toggleData = new PushButtonData(
            "RevitMcpToggle",
            "MCP Switch",
            typeof(App).Assembly.Location,
            typeof(ToggleCommand).FullName);
        panel.AddItem(toggleData);

        var settingsData = new PushButtonData(
            "RevitMcpSettings",
            "Settings",
            typeof(App).Assembly.Location,
            typeof(SettingsCommand).FullName);
        panel.AddItem(settingsData);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        Service?.Stop();
        return Result.Succeeded;
    }

    internal static void InitService(UIApplication uiApp)
    {
        var registry = new CommandRegistry();
        var executor = new CommandExecutor(registry);
        Service = new PipeService(registry, executor);

        ExternalEventManager.Instance.Initialize(uiApp);

        var configManager = new ConfigurationManager();
        configManager.LoadConfiguration();

        var commandManager = new CommandManager(registry, configManager, uiApp, uiApp.Application.VersionNumber);
        commandManager.LoadCommands();
    }
}

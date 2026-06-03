using Autodesk.Revit.UI;
using RevitMcpPlugin.AI;
using RevitMcpPlugin.Configuration;
using RevitMcpPlugin.Core;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitMcpPlugin;

public class App : IExternalApplication
{
    private const string TabName = "Revit MCP";
    private const string PanelName = "MCP Control";

    internal static PipeService? Service { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            application.CreateRibbonTab(TabName);
        }
        catch
        {
            // Revit throws if another add-in or previous load already created the tab.
        }

        var panel = application.CreateRibbonPanel(TabName, PanelName);

        var settingsData = new PushButtonData(
            "RevitMcpSettings",
            "MCP",
            typeof(App).Assembly.Location,
            typeof(SettingsCommand).FullName);
        settingsData.ToolTip = "Open Revit MCP settings and connection controls.";
        settingsData.LongDescription = "Configure command sets, start or stop the MCP pipe service, and view connection logs.";

        if (panel.AddItem(settingsData) is PushButton settingsButton)
        {
            var icon = CreateIcon(Color.FromRgb(0, 120, 215));
            settingsButton.Image = icon;
            settingsButton.LargeImage = icon;
        }

        // Register the AI Chat dockable pane
        application.RegisterDockablePane(AiPanelProvider.PanelId, "AI Chat", new AiPanelProvider());

        // Add AI Chat button to the ribbon panel
        var aiChatData = new PushButtonData(
            "RevitMcpAiChat", "AI Chat",
            typeof(App).Assembly.Location,
            typeof(AiPanelCommand).FullName);
        aiChatData.ToolTip = "Open AI Chat panel for interacting with Revit via LLM.";

        if (panel.AddItem(aiChatData) is PushButton aiChatButton)
        {
            var aiIcon = CreateIcon(Color.FromRgb(106, 90, 205));
            aiChatButton.Image = aiIcon;
            aiChatButton.LargeImage = aiIcon;
        }

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        StopService();
        return Result.Succeeded;
    }

    internal static void InitService(UIApplication uiApp, Action<string>? log = null)
    {
        if (Service != null)
            return;

        log?.Invoke("Initializing Revit MCP service.");
        var registry = new CommandRegistry();
        var executor = new CommandExecutor(registry);
        Service = new PipeService(registry, executor, log);

        ExternalEventManager.Instance.Initialize(uiApp);

        var configManager = new ConfigurationManager();
        configManager.LoadConfiguration();
        log?.Invoke("Command configuration loaded.");

        var commandManager = new CommandManager(registry, configManager, uiApp, uiApp.Application.VersionNumber, log);
        commandManager.LoadCommands();
    }

    internal static void StopService()
    {
        Service?.Stop();
        Service = null;
    }

    private static ImageSource CreateIcon(Color accent)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var background = new SolidColorBrush(accent);
            var foreground = Brushes.White;
            var pen = new Pen(foreground, 2.4);

            context.DrawRoundedRectangle(background, null, new Rect(0, 0, 32, 32), 5, 5);
            context.DrawEllipse(null, pen, new Point(16, 16), 8, 8);
            context.DrawLine(pen, new Point(16, 5), new Point(16, 10));
            context.DrawLine(pen, new Point(16, 22), new Point(16, 27));
            context.DrawLine(pen, new Point(5, 16), new Point(10, 16));
            context.DrawLine(pen, new Point(22, 16), new Point(27, 16));
            context.DrawEllipse(foreground, null, new Point(16, 16), 2.5, 2.5);
        }

        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}

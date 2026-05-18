using Autodesk.Revit.UI;
using RevitMcpSdk;

namespace RevitMcpPlugin.Core;

public class ExternalEventManager
{
    private static ExternalEventManager? _instance;
    private readonly Dictionary<string, ExternalEvent> _events = new();
    private bool _isInitialized;

    public static ExternalEventManager Instance => _instance ??= new ExternalEventManager();

    private ExternalEventManager() { }

    public void Initialize(UIApplication uiApp)
    {
        _isInitialized = true;
    }

    public ExternalEvent GetOrCreateEvent(IWaitableExternalEventHandler handler, string key)
    {
        if (!_isInitialized)
            throw new InvalidOperationException($"{nameof(ExternalEventManager)} has not been initialized.");

        if (_events.TryGetValue(key, out var externalEvent))
            return externalEvent;

        externalEvent = ExternalEvent.Create(handler);
        _events[key] = externalEvent;
        return externalEvent;
    }

    public void ClearEvents() => _events.Clear();
}

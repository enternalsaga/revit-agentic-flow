using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace RevitMcpSdk;

public abstract class ExternalEventCommandBase : IRevitCommand
{
    private readonly IExternalEventHandler _handler;
    private readonly UIApplication _uiApp;
    private ExternalEvent? _externalEvent;

    protected ExternalEventCommandBase(IExternalEventHandler handler, UIApplication uiApp)
    {
        _handler = handler;
        _uiApp = uiApp;
        _externalEvent = ExternalEvent.Create(_handler);
    }

    protected IExternalEventHandler Handler => _handler;

    public abstract string CommandName { get; }
    public abstract object Execute(JObject parameters, string requestId);

    protected bool RaiseAndWaitForCompletion(int timeoutMs)
    {
        _externalEvent!.Raise();

        if (_handler is IWaitableExternalEventHandler waitable)
            return waitable.WaitForCompletion(timeoutMs);

        return true;
    }
}

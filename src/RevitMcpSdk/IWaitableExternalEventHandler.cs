using Autodesk.Revit.UI;

namespace RevitMcpSdk;

public interface IWaitableExternalEventHandler : IExternalEventHandler
{
    bool WaitForCompletion(int timeoutMilliseconds = 10000);
}

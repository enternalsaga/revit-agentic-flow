using Autodesk.Revit.UI;

namespace RevitMcpSdk;

public interface IRevitCommandInitializable
{
    void Initialize(UIApplication uiApp);
}

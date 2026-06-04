using Newtonsoft.Json.Linq;
using Autodesk.Revit.UI;
using RevitMcpSdk;
using RevitMcpSdk.Rag;

namespace RevitMcpCommandSet.Commands;

public class SearchRevitApiCommand : IRevitCommand
{
    private readonly string _revitVersion;

    public SearchRevitApiCommand(UIApplication uiApp)
    {
        _revitVersion = uiApp.Application.VersionNumber;
    }

    public string CommandName => "search_revit_api";

    public object Execute(JObject parameters, string requestId)
    {
        var query = parameters["query"]?.ToString();
        if (string.IsNullOrWhiteSpace(query))
            return new { error = "Parameter 'query' is required." };

        var revitVersion = parameters["revitVersion"]?.ToString()
            ?? _revitVersion;
        var result = LocalRevitRagService.FetchAsync(query, revitVersion).GetAwaiter().GetResult();

        return new
        {
            status = result.Status,
            context = result.ContextText,
            elapsedMs = result.ElapsedMs,
            error = result.ErrorSummary
        };
    }
}

namespace RevitMcpSdk.Rag;

/// <summary>
/// Result of a RAG fetch — used by LocalRevitRagService and the search_revit_api tool.
/// </summary>
public class RagFetchResult
{
    public string Status { get; set; } = "";  // "hit" | "no_match" | "no_index" | "error"
    public string? ContextText { get; set; }
    public long ElapsedMs { get; set; }
    public string? ErrorSummary { get; set; }

    public bool HasContext => !string.IsNullOrWhiteSpace(ContextText);
}

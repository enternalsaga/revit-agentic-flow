namespace RevitMcpSdk.Rag;

/// <summary>
/// A single searchable unit in the Revit API index.
/// One chunk = one class (or a domain-grouped subset for giant classes).
/// </summary>
internal class RagChunk
{
    /// <summary>Class name, e.g. "Document"</summary>
    public string ClassName { get; set; } = "";

    /// <summary>Full namespace, e.g. "Autodesk.Revit.DB"</summary>
    public string Namespace { get; set; } = "";

    /// <summary>
    /// The human-readable text that is sent to the LLM as context.
    /// Contains class summary + member signatures + descriptions.
    /// </summary>
    public string DisplayText { get; set; } = "";

    /// <summary>
    /// Text used for BM25 indexing (DisplayText + extra tags for better matching).
    /// </summary>
    public string IndexText { get; set; } = "";
}

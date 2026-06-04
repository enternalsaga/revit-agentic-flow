using System.Reflection;

namespace RevitMcpPlugin.AI.Roslyn;

public class CompilationResult
{
    public bool Success { get; set; }
    public Assembly? Assembly { get; set; }
    public List<CompilationDiagnostic> Diagnostics { get; set; } = new();
    public string? ErrorSummary { get; set; }
    public string? OriginalSource { get; set; }
    public string? NormalizedSource { get; set; }
    public string? WrappedSource { get; set; }
}

public class CompilationDiagnostic
{
    public string Id { get; set; } = "";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }

    public override string ToString() => $"[{Id}] L{Line}:C{Column} ({Severity}) {Message}";
}

internal class PreparedSource
{
    public string OriginalSource { get; set; } = "";
    public string NormalizedSource { get; set; } = "";
    public string WrappedSource { get; set; } = "";
}

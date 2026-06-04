namespace RevitMcpPlugin.AI.Roslyn;

public enum AnalyzerSeverity { Info, Warning, Error }

public class AnalyzerDiagnostic
{
    public string Id { get; set; } = "";
    public string Message { get; set; } = "";
    public AnalyzerSeverity Severity { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string? SuggestedFix { get; set; }

    public override string ToString() => $"[{Id}] L{Line}:C{Column} ({Severity}) {Message}";
}

public class AnalyzerReport
{
    public List<AnalyzerDiagnostic> Diagnostics { get; set; } = new();

    public bool HasErrors => Diagnostics.Any(d => d.Severity == AnalyzerSeverity.Error);
    public bool HasWarnings => Diagnostics.Any(d => d.Severity == AnalyzerSeverity.Warning);
    public int ErrorCount => Diagnostics.Count(d => d.Severity == AnalyzerSeverity.Error);
    public int WarningCount => Diagnostics.Count(d => d.Severity == AnalyzerSeverity.Warning);

    public string FormatSummary()
    {
        if (Diagnostics.Count == 0) return "No issues found.";
        return string.Join("\n", Diagnostics.Select(d => d.ToString()));
    }
}

public class CodeFixResult
{
    public string OriginalCode { get; set; } = "";
    public string FixedCode { get; set; } = "";
    public bool HasChanges { get; set; }
    public List<string> AppliedFixes { get; set; } = new();
    public List<string> SuggestedFixes { get; set; } = new();
}

using System.Text;

namespace RevitMcpPlugin.AI;

public static class HistorySummariser
{
    private const int MaxSummaryChars = 600;
    private const int MaxUserSnippetChars = 80;

    public static string Summarise(IReadOnlyList<(string role, string text)> dropped)
    {
        if (dropped == null || dropped.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("[Earlier session context — ");
        sb.Append(dropped.Count);
        sb.AppendLine(" earlier turns; latest turns continue below]");

        var firstUser = dropped.FirstOrDefault(m => m.role == "user");
        if (!string.IsNullOrWhiteSpace(firstUser.text))
            sb.AppendLine($"- session opened with: \"{Clip(firstUser.text, MaxUserSnippetChars)}\"");

        var lastUser = dropped.LastOrDefault(m => m.role == "user");
        if (lastUser != firstUser && !string.IsNullOrWhiteSpace(lastUser.text))
            sb.AppendLine($"- last user message before window: \"{Clip(lastUser.text, MaxUserSnippetChars)}\"");

        string result = sb.ToString();
        return result.Length <= MaxSummaryChars
            ? result
            : result.Substring(0, MaxSummaryChars) + "...]";
    }

    private static string Clip(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
    }
}

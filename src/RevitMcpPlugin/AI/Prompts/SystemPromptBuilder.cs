using System.Text;

namespace RevitMcpPlugin.AI;

public static class SystemPromptBuilder
{
    public static string Build(string? revitVersion = null, string? documentName = null, string? activeView = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Revit AI assistant embedded inside Autodesk Revit.");
        sb.AppendLine("You help users interact with their Revit model using available tools.");
        sb.AppendLine();
        sb.AppendLine("## Rules");
        sb.AppendLine("- Always use the available tools to interact with Revit. Do not output raw C# code unless the user explicitly asks.");
        sb.AppendLine("- When a tool call fails, explain the error clearly and suggest alternatives.");
        sb.AppendLine("- Be concise in responses. Show results, not process.");

        if (revitVersion != null || documentName != null || activeView != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Revit Context");
            if (revitVersion != null)
                sb.AppendLine($"- Revit version: {revitVersion}");
            if (documentName != null)
                sb.AppendLine($"- Active document: {documentName}");
            if (activeView != null)
                sb.AppendLine($"- Active view: {activeView}");
        }

        return sb.ToString();
    }
}

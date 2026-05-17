namespace RevitMcpServer.Utils;

public static class ErrorFormatter
{
    public static string FormatForLlm(string command, Exception ex)
    {
        var errorType = ex.GetType().Name;
        var message = ex.Message;

        var hint = errorType switch
        {
            "TimeoutException" => "The Revit command took too long. Try simplifying the operation or breaking it into smaller batches.",
            "InvalidOperationException" => "Revit may not have an active document open, or the requested operation is not valid in the current state.",
            "ArgumentException" => "One or more parameters were invalid. Check parameter names, types, and ranges.",
            _ => "Check that Revit is running and the MCP plugin is enabled."
        };

        return $"Command '{command}' failed.\nError: {errorType} - {message}\nHint: {hint}";
    }
}

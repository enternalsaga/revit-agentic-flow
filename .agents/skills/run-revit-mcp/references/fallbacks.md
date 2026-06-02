# Revit MCP Fallbacks

Use this reference only when direct MCP tools are unavailable, stale, or fail with evidence.

## Runtime Discovery Order

1. Run `src/RevitHarness/bootstrap.ps1`.
2. Prefer direct MCP tools exposed to the agent.
3. If direct MCP discovery is stale but Revit runtime has the command, call `src/RevitHarness/invoke-command.ps1`.
4. Use compiled helper DLL flow only for large C# payloads or custom geometry.
5. Use inline `send_code_to_revit` only when the command wrapper and compiled helper flow are inappropriate.

## JSON-RPC Invocation

Use params files or canonical JSON through `invoke-command.ps1` to avoid PowerShell quoting errors.

Example:

```powershell
$params = @{ data = @(@{ name = "L1"; elevation = 0 }) } | ConvertTo-Json -Depth 10
$paramsPath = New-TemporaryFile
Set-Content -Path $paramsPath.FullName -Value $params -Encoding UTF8
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName create_level -ParamsPath $paramsPath.FullName
```

## Compiled Helper Flow

Use `.agents/skills/run-revit-mcp/scripts/Invoke-RevitMcpJsonRpc.ps1` with `-SourcePath`, `-TypeName`, and `-MethodName` for long C# snippets.

Do not create a nested Revit `Transaction` inside helper code when the bridge already wraps execution in a transaction.

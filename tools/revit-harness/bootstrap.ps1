param(
    [string]$WorkspaceRoot = (Resolve-Path ".").Path,
    [switch]$WriteCache
)

$ErrorActionPreference = "Stop"
$registryScript = Join-Path $WorkspaceRoot "tools/revit-harness/registry-report.ps1"
$invokeScript = Join-Path $WorkspaceRoot "tools/revit-harness/invoke-command.ps1"

$registry = powershell -NoProfile -ExecutionPolicy Bypass -File $registryScript -WorkspaceRoot $WorkspaceRoot | ConvertFrom-Json

$jsonRpcStatus = "unavailable"
$projectInfo = $null
try {
    $probe = powershell -NoProfile -ExecutionPolicy Bypass -File $invokeScript -CommandName get_project_info -ParamsJson "{}" -TimeoutSeconds 10 | ConvertFrom-Json
    if ($probe.success) {
        $jsonRpcStatus = "available"
        $projectInfo = $probe.result
    }
} catch {
    $jsonRpcStatus = "unavailable"
}

$phase1Status = "unknown"

$bootstrap = [ordered]@{
    success = $true
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    workspaceRoot = $WorkspaceRoot
    transports = [ordered]@{
        phase1Pipe = $phase1Status
        legacyJsonRpc = $jsonRpcStatus
    }
    projectInfo = $projectInfo
    registry = $registry
    guidance = @()
}

if ($jsonRpcStatus -eq "available") {
    $bootstrap.guidance += "Use invoke-command.ps1 for JSON-RPC fallback calls."
} else {
    $bootstrap.guidance += "Open Revit and ensure the MCP plugin is loaded before live command execution."
}

if ($WriteCache) {
    $cacheDir = Join-Path $WorkspaceRoot ".revit-harness/cache"
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $bootstrap | ConvertTo-Json -Depth 50 | Set-Content -Path (Join-Path $cacheDir "last-bootstrap.json") -Encoding UTF8
}

$bootstrap | ConvertTo-Json -Depth 50

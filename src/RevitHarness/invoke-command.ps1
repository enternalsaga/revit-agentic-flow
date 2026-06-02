param(
    [Parameter(Mandatory=$true)][string]$CommandName,
    [string]$ParamsJson = "{}",
    [string]$ParamsPath = "",
    [ValidateSet("auto", "jsonrpc")][string]$Transport = "auto",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$workspaceRoot = (Resolve-Path ".").Path
$bridgeScript = Join-Path $workspaceRoot ".agents/skills/run-revit-mcp/scripts/Invoke-RevitMcpJsonRpc.ps1"
$requestId = "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), ([guid]::NewGuid().ToString("N").Substring(0, 8))
$started = Get-Date

if ($ParamsPath) {
    $rawParams = Get-Content -Path $ParamsPath -Raw
} else {
    $rawParams = $ParamsJson
}

try {
    $parsedParams = $rawParams | ConvertFrom-Json
    $canonicalParams = $parsedParams | ConvertTo-Json -Depth 50 -Compress
} catch {
    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    [ordered]@{
        success = $false
        command = $CommandName
        transport = "none"
        durationMs = $duration
        requestId = $requestId
        paramsHash = $null
        result = $null
        error = [ordered]@{
            categoryHint = "json_quoting"
            message = $_.Exception.Message
            raw = $rawParams
        }
    } | ConvertTo-Json -Depth 20
    exit 1
}

$sha = [System.Security.Cryptography.SHA256]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($canonicalParams)
$hash = "sha256:" + (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "")

try {
    if (-not (Test-Path $bridgeScript)) {
        throw "Missing JSON-RPC bridge script: $bridgeScript"
    }

    $tempParams = New-TemporaryFile
    Set-Content -Path $tempParams.FullName -Value $canonicalParams -Encoding UTF8

    $job = Start-Job -ScriptBlock {
        param($scriptPath, $method, $paramsFile, $timeoutSeconds)
        $json = Get-Content -Path $paramsFile -Raw
        powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -Method $method -ParamsJson $json -TimeoutSeconds $timeoutSeconds
    } -ArgumentList $bridgeScript, $CommandName, $tempParams.FullName, $TimeoutSeconds

    if (-not (Wait-Job $job -Timeout $TimeoutSeconds)) {
        Stop-Job $job
        throw "Command timed out after $TimeoutSeconds seconds"
    }

    $output = Receive-Job $job -ErrorAction Stop

    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    [ordered]@{
        success = $true
        command = $CommandName
        transport = "jsonrpc"
        durationMs = $duration
        requestId = $requestId
        paramsHash = $hash
        result = ($output -join "`n" | ConvertFrom-Json)
        error = $null
    } | ConvertTo-Json -Depth 50
} catch {
    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    [ordered]@{
        success = $false
        command = $CommandName
        transport = "jsonrpc"
        durationMs = $duration
        requestId = $requestId
        paramsHash = $hash
        result = $null
        error = [ordered]@{
            categoryHint = "command_error"
            message = $_.Exception.Message
            raw = $_.ToString()
        }
    } | ConvertTo-Json -Depth 20
    exit 1
} finally {
    if ($job) {
        Remove-Job $job -Force -ErrorAction SilentlyContinue
    }
    if ($tempParams -and (Test-Path $tempParams.FullName)) {
        Remove-Item -LiteralPath $tempParams.FullName -Force -ErrorAction SilentlyContinue
    }
}

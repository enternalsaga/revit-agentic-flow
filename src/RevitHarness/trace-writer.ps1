param(
    [ValidateSet("new", "append-command", "append-classification", "finalize")][string]$Mode = "new",
    [string]$RunId = "",
    [string]$TaskLabel = "revit-task",
    [string]$UserIntent = "",
    [string]$CommandResultPath = "",
    [string]$ClassificationPath = "",
    [string]$FinalStatus = "in_progress"
)

$ErrorActionPreference = "Stop"
$workspaceRoot = (Resolve-Path ".").Path

function New-RunId {
    param([string]$Label)
    $slug = ($Label.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if (-not $slug) { $slug = "revit-task" }
    "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $slug
}

if (-not $RunId) {
    $RunId = New-RunId -Label $TaskLabel
}

$runDir = Join-Path $workspaceRoot ".revit-harness/runs/$RunId"
$tracePath = Join-Path $runDir "trace.json"

if ($Mode -eq "new") {
    New-Item -ItemType Directory -Force -Path $runDir | Out-Null
    $trace = [ordered]@{
        traceVersion = "1.0"
        runId = $RunId
        timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        workspaceRoot = $workspaceRoot
        taskLabel = $TaskLabel
        userIntent = $UserIntent
        agentPlan = @()
        bootstrap = $null
        commands = @()
        snapshots = @()
        modelStatistics = $null
        warnings = $null
        classifiedFailures = @()
        lessonCandidates = @()
        finalStatus = "in_progress"
    }
    $trace | ConvertTo-Json -Depth 50 | Set-Content -Path $tracePath -Encoding UTF8
    [ordered]@{ success = $true; runId = $RunId; tracePath = $tracePath } | ConvertTo-Json
    exit 0
}

if (-not (Test-Path $tracePath)) {
    throw "Trace not found: $tracePath"
}

$trace = Get-Content -Path $tracePath -Raw | ConvertFrom-Json

if ($Mode -eq "append-command") {
    if (-not (Test-Path $CommandResultPath)) {
        throw "Command result file not found: $CommandResultPath"
    }
    $commandResult = Get-Content -Path $CommandResultPath -Raw | ConvertFrom-Json
    $commands = @($trace.commands)
    $commands += $commandResult
    $trace.commands = $commands
}

if ($Mode -eq "append-classification") {
    if (-not (Test-Path $ClassificationPath)) {
        throw "Classification file not found: $ClassificationPath"
    }
    $classification = Get-Content -Path $ClassificationPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($classification.category)) {
        throw "Classification category is required."
    }
    $classifiedFailures = @($trace.classifiedFailures)
    $classifiedFailures += $classification
    $trace.classifiedFailures = $classifiedFailures
}

if ($Mode -eq "finalize") {
    $trace.finalStatus = $FinalStatus
}

$trace | ConvertTo-Json -Depth 50 | Set-Content -Path $tracePath -Encoding UTF8
[ordered]@{ success = $true; runId = $RunId; tracePath = $tracePath } | ConvertTo-Json

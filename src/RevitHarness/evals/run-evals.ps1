param(
    [switch]$IncludeLive,
    [string]$ResultsDirectory = "src/RevitHarness/evals/results"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null

$evalRunId = Get-Date -Format "yyyyMMdd_HHmmss"
$started = (Get-Date).ToUniversalTime().ToString("o")
$evalFiles = @(
    "eval-bootstrap.ps1",
    "eval-registry-report.ps1",
    "eval-invoke-command.ps1",
    "eval-failure-classifier.ps1",
    "eval-trace-writer.ps1"
)

if ($IncludeLive) {
    $evalFiles += @(
        "live/eval-create-levels-grids.ps1",
        "live/eval-create-basic-building.ps1",
        "live/eval-create-or-switch-3d-view.ps1",
        "live/eval-snapshot-statistics.ps1"
    )
}

$results = @()
foreach ($evalFile in $evalFiles) {
    $path = Join-Path "src/RevitHarness/evals" $evalFile
    $before = Get-Date
    try {
        $result = powershell -NoProfile -ExecutionPolicy Bypass -File $path | ConvertFrom-Json
        $results += $result
    } catch {
        $results += [ordered]@{
            name = [System.IO.Path]::GetFileNameWithoutExtension($evalFile)
            status = "failed"
            durationMs = [int]((Get-Date) - $before).TotalMilliseconds
            assertions = @(@{ name = "scriptRuns"; passed = $false; message = $_.Exception.Message })
        }
    }
}

$summary = [ordered]@{
    passed = @($results | Where-Object { $_.status -eq "passed" }).Count
    failed = @($results | Where-Object { $_.status -eq "failed" }).Count
    skipped = @($results | Where-Object { $_.status -eq "skipped" }).Count
}

$run = [ordered]@{
    evalRunId = $evalRunId
    startedAtUtc = $started
    environment = [ordered]@{
        revitConnected = $null
        transport = $null
    }
    results = $results
    summary = $summary
}

$path = Join-Path $ResultsDirectory "$evalRunId.json"
$run | ConvertTo-Json -Depth 50 | Set-Content -Path $path -Encoding UTF8
$run | ConvertTo-Json -Depth 50

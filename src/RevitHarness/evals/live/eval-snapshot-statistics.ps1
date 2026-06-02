$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-snapshot-statistics"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}
$stats = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName analyze_model_statistics | ConvertFrom-Json
$snapshotParams = @{ includeImage = $false; includeVisibleElements = $true; pixelSize = 800 } | ConvertTo-Json -Depth 10 -Compress
$snapshotParamsPath = New-TemporaryFile
try {
  Set-Content -Path $snapshotParamsPath.FullName -Value $snapshotParams -Encoding UTF8
  $snapshot = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName snapshot_workspace -ParamsPath $snapshotParamsPath.FullName | ConvertFrom-Json
} finally {
  Remove-Item -LiteralPath $snapshotParamsPath.FullName -Force -ErrorAction SilentlyContinue
}
$assertions = @(
  @{ name = "statisticsSucceeded"; passed = [bool]$stats.success; message = $stats.error.message },
  @{ name = "snapshotSucceeded"; passed = [bool]$snapshot.success; message = $snapshot.error.message }
)
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-snapshot-statistics"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20

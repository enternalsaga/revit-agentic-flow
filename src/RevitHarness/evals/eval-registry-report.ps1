$started = Get-Date
$assertions = @()
$report = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\registry-report.ps1' | ConvertFrom-Json
$assertions += @{ name = "registryReportsSuccess"; passed = [bool]$report.success; message = "" }
$assertions += @{ name = "manifestHasCommands"; passed = ($report.counts.manifest -gt 0); message = "manifest count is $($report.counts.manifest)" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-registry-report"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10

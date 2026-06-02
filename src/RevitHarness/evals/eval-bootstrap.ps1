$started = Get-Date
$assertions = @()
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' | ConvertFrom-Json
$assertions += @{ name = "bootstrapReportsSuccess"; passed = [bool]$bootstrap.success; message = "" }
$assertions += @{ name = "bootstrapHasTransports"; passed = ($null -ne $bootstrap.transports); message = "" }
$assertions += @{ name = "bootstrapHasRegistry"; passed = ($null -ne $bootstrap.registry); message = "" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-bootstrap"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10

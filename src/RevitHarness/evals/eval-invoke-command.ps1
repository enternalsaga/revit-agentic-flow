$started = Get-Date
$assertions = @()
$result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName get_project_info -ParamsJson '{bad json}' | ConvertFrom-Json
$assertions += @{ name = "invalidJsonFails"; passed = (-not $result.success); message = "" }
$assertions += @{ name = "invalidJsonClassified"; passed = ($result.error.categoryHint -eq "json_quoting"); message = "category was $($result.error.categoryHint)" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-invoke-command"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10

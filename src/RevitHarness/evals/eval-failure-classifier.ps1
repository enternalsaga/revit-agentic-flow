$started = Get-Date
$assertions = @()
$expected = @{
  "json_quoting" = "json_quoting"
  "command_not_registered" = "command_not_registered"
  "invalid_geometry" = "invalid_geometry"
  "view_missing" = "view_missing"
  "verification_gap" = "verification_gap"
}
foreach ($name in $expected.Keys) {
  $result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\classify-failure.ps1' -InputPath "src/RevitHarness/fixtures/errors/$name.json" | ConvertFrom-Json
  $assertions += @{ name = "classifies_$name"; passed = ($result.category -eq $expected[$name]); message = "category was $($result.category)" }
}
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-failure-classifier"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20

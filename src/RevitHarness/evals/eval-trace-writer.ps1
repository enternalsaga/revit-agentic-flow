$started = Get-Date
$assertions = @()
$run = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\trace-writer.ps1' -Mode new -TaskLabel eval-trace-writer | ConvertFrom-Json
$traceExists = Test-Path $run.tracePath
$trace = if ($traceExists) { Get-Content $run.tracePath -Raw | ConvertFrom-Json } else { $null }
$assertions += @{ name = "traceCreated"; passed = $traceExists; message = $run.tracePath }
$assertions += @{ name = "traceHasRunId"; passed = ($null -ne $trace.runId); message = "" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-trace-writer"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10

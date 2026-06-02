$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-create-or-switch-3d-view"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}
$views = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName get_views | ConvertFrom-Json
$viewResult = $views.result
$threeDView = @($viewResult.views | Where-Object { $_.viewType -eq "ThreeD" -or $_.viewType -eq "ThreeDimensional" } | Select-Object -First 1)
$assertions = @(@{ name = "getViewsSucceeded"; passed = [bool]$views.success; message = "" })
if ($threeDView.Count -gt 0) {
  $params = @{ viewId = [int]$threeDView[0].id } | ConvertTo-Json -Compress
  $switch = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName switch_view -ParamsJson $params | ConvertFrom-Json
  $assertions += @{ name = "switch3dViewSucceeded"; passed = [bool]$switch.success; message = "" }
} else {
  $assertions += @{ name = "threeDViewAvailable"; passed = $false; message = "No 3D view exists; this exposes the view_missing workflow." }
}
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-create-or-switch-3d-view"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20

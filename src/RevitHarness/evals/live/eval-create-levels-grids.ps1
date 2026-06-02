$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-create-levels-grids"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}
$levelParams = @{ data = @(@{ name = "EVAL_L1"; elevation = 0; createFloorPlan = $false; createCeilingPlan = $false }) } | ConvertTo-Json -Depth 10 -Compress
$level = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName create_level -ParamsJson $levelParams | ConvertFrom-Json
$gridParams = @{ xGrids = @(@{ label = "EX1"; position = 0 }); yGrids = @(@{ label = "EY1"; position = 0 }); xExtentMin = 0; xExtentMax = 1000; yExtentMin = 0; yExtentMax = 1000; elevation = 0 } | ConvertTo-Json -Depth 10 -Compress
$grid = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName create_custom_grid -ParamsJson $gridParams | ConvertFrom-Json
$assertions = @(
  @{ name = "levelCommandSucceeded"; passed = [bool]$level.success; message = "" },
  @{ name = "gridCommandSucceeded"; passed = [bool]$grid.success; message = "" }
)
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-create-levels-grids"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20

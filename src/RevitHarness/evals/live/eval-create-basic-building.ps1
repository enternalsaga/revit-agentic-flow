$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-create-basic-building"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}

$loop = @(
  @{ p0 = @{ x = 0; y = 0; z = 0 }; p1 = @{ x = 3000; y = 0; z = 0 } },
  @{ p0 = @{ x = 3000; y = 0; z = 0 }; p1 = @{ x = 3000; y = 3000; z = 0 } },
  @{ p0 = @{ x = 3000; y = 3000; z = 0 }; p1 = @{ x = 0; y = 3000; z = 0 } },
  @{ p0 = @{ x = 0; y = 3000; z = 0 }; p1 = @{ x = 0; y = 0; z = 0 } }
)

$floorParams = @{ data = @(@{ name = "EVAL_Floor"; category = "OST_Floors"; boundary = @{ outerLoop = $loop }; thickness = 150; baseLevel = 0; baseOffset = 0 }) } | ConvertTo-Json -Depth 20 -Compress
$floorParamsPath = New-TemporaryFile
try {
  Set-Content -Path $floorParamsPath.FullName -Value $floorParams -Encoding UTF8
  $floor = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName create_surface_based_element -ParamsPath $floorParamsPath.FullName | ConvertFrom-Json
} finally {
  Remove-Item -LiteralPath $floorParamsPath.FullName -Force -ErrorAction SilentlyContinue
}

$wallParams = @{ data = @(@{ category = "OST_Walls"; locationLine = @{ p0 = @{ x = 0; y = 0; z = 0 }; p1 = @{ x = 3000; y = 0; z = 0 } }; thickness = 150; height = 3000; baseLevel = 0; baseOffset = 0 }) } | ConvertTo-Json -Depth 20 -Compress
$wallParamsPath = New-TemporaryFile
try {
  Set-Content -Path $wallParamsPath.FullName -Value $wallParams -Encoding UTF8
  $wall = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName create_line_based_element -ParamsPath $wallParamsPath.FullName | ConvertFrom-Json
} finally {
  Remove-Item -LiteralPath $wallParamsPath.FullName -Force -ErrorAction SilentlyContinue
}

$assertions = @(
  @{ name = "floorCommandSucceeded"; passed = [bool]$floor.success; message = $floor.error.message },
  @{ name = "wallCommandSucceeded"; passed = [bool]$wall.success; message = $wall.error.message }
)
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-create-basic-building"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20

# Revit Harness Phase 4 Evals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add technical and live Revit evals so future self-improvement patches can be measured before approval.

**Architecture:** Build a lightweight PowerShell eval runner. Technical evals run without Revit; live evals skip cleanly when Revit is unavailable and produce JSON result summaries.

**Tech Stack:** PowerShell 5+, JSON, Phase 1-2 harness scripts, optional live Revit JSON-RPC bridge.

---

## File Structure

- Create: `tools/revit-harness/evals/run-evals.ps1`
- Create: `tools/revit-harness/evals/eval-bootstrap.ps1`
- Create: `tools/revit-harness/evals/eval-registry-report.ps1`
- Create: `tools/revit-harness/evals/eval-invoke-command.ps1`
- Create: `tools/revit-harness/evals/eval-failure-classifier.ps1`
- Create: `tools/revit-harness/evals/eval-trace-writer.ps1`
- Create: `tools/revit-harness/evals/live/eval-create-levels-grids.ps1`
- Create: `tools/revit-harness/evals/live/eval-create-basic-building.ps1`
- Create: `tools/revit-harness/evals/live/eval-create-or-switch-3d-view.ps1`
- Create: `tools/revit-harness/evals/live/eval-snapshot-statistics.ps1`
- Runtime output: `tools/revit-harness/evals/results/`

### Task 1: Add Eval Result Helpers

**Files:**
- Create: `tools/revit-harness/evals/run-evals.ps1`

- [ ] **Step 1: Create eval directory**

```powershell
New-Item -ItemType Directory -Force 'tools/revit-harness/evals/live' | Out-Null
New-Item -ItemType Directory -Force 'tools/revit-harness/evals/results' | Out-Null
```

- [ ] **Step 2: Create `run-evals.ps1`**

```powershell
param(
    [switch]$IncludeLive,
    [string]$ResultsDirectory = "tools/revit-harness/evals/results"
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
    $path = Join-Path "tools/revit-harness/evals" $evalFile
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
```

- [ ] **Step 3: Commit runner**

```powershell
git add tools/revit-harness/evals/run-evals.ps1
git commit -m "feat: add revit harness eval runner"
```

### Task 2: Add Technical Evals

**Files:**
- Create: `tools/revit-harness/evals/eval-bootstrap.ps1`
- Create: `tools/revit-harness/evals/eval-registry-report.ps1`
- Create: `tools/revit-harness/evals/eval-invoke-command.ps1`
- Create: `tools/revit-harness/evals/eval-failure-classifier.ps1`
- Create: `tools/revit-harness/evals/eval-trace-writer.ps1`

- [ ] **Step 1: Create common result pattern**

Each eval file should return this structure:

```json
{
  "name": "eval-bootstrap",
  "status": "passed",
  "durationMs": 100,
  "assertions": [
    { "name": "assertionName", "passed": true, "message": "" }
  ]
}
```

- [ ] **Step 2: Create `eval-registry-report.ps1`**

```powershell
$started = Get-Date
$assertions = @()
$report = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\registry-report.ps1' | ConvertFrom-Json
$assertions += @{ name = "registryReportsSuccess"; passed = [bool]$report.success; message = "" }
$assertions += @{ name = "manifestHasCommands"; passed = ($report.counts.manifest -gt 0); message = "manifest count is $($report.counts.manifest)" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-registry-report"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10
```

- [ ] **Step 3: Create `eval-bootstrap.ps1`**

```powershell
$started = Get-Date
$assertions = @()
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\bootstrap.ps1' | ConvertFrom-Json
$assertions += @{ name = "bootstrapReportsSuccess"; passed = [bool]$bootstrap.success; message = "" }
$assertions += @{ name = "bootstrapHasTransports"; passed = ($null -ne $bootstrap.transports); message = "" }
$assertions += @{ name = "bootstrapHasRegistry"; passed = ($null -ne $bootstrap.registry); message = "" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-bootstrap"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10
```

- [ ] **Step 4: Create `eval-invoke-command.ps1`**

```powershell
$started = Get-Date
$assertions = @()
$result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName get_project_info -ParamsJson '{bad json}' | ConvertFrom-Json
$assertions += @{ name = "invalidJsonFails"; passed = (-not $result.success); message = "" }
$assertions += @{ name = "invalidJsonClassified"; passed = ($result.error.categoryHint -eq "json_quoting"); message = "category was $($result.error.categoryHint)" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-invoke-command"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10
```

- [ ] **Step 5: Create `eval-failure-classifier.ps1`**

```powershell
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
  $result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\classify-failure.ps1' -InputPath "tools/revit-harness/fixtures/errors/$name.json" | ConvertFrom-Json
  $assertions += @{ name = "classifies_$name"; passed = ($result.category -eq $expected[$name]); message = "category was $($result.category)" }
}
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-failure-classifier"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20
```

- [ ] **Step 6: Create `eval-trace-writer.ps1`**

```powershell
$started = Get-Date
$assertions = @()
$run = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\trace-writer.ps1' -Mode new -TaskLabel eval-trace-writer | ConvertFrom-Json
$traceExists = Test-Path $run.tracePath
$trace = if ($traceExists) { Get-Content $run.tracePath -Raw | ConvertFrom-Json } else { $null }
$assertions += @{ name = "traceCreated"; passed = $traceExists; message = $run.tracePath }
$assertions += @{ name = "traceHasRunId"; passed = ($null -ne $trace.runId); message = "" }
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-trace-writer"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 10
```

- [ ] **Step 7: Commit technical evals**

```powershell
git add tools/revit-harness/evals/eval-*.ps1
git commit -m "feat: add revit harness technical evals"
```

### Task 3: Add Live Revit Eval Skip Harness

**Files:**
- Create: `tools/revit-harness/evals/live/eval-create-levels-grids.ps1`
- Create: `tools/revit-harness/evals/live/eval-create-basic-building.ps1`
- Create: `tools/revit-harness/evals/live/eval-create-or-switch-3d-view.ps1`
- Create: `tools/revit-harness/evals/live/eval-snapshot-statistics.ps1`

- [ ] **Step 1: Create shared live preflight block**

Use this block at the top of each live eval:

```powershell
$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{
    name = "LIVE_EVAL_NAME"
    status = "skipped"
    durationMs = [int]((Get-Date)-$started).TotalMilliseconds
    assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" })
  } | ConvertTo-Json -Depth 10
  exit 0
}
```

- [ ] **Step 2: Create `eval-create-levels-grids.ps1`**

```powershell
$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-create-levels-grids"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}
$levelParams = @{ data = @(@{ name = "EVAL_L1"; elevation = 0; createFloorPlan = $false; createCeilingPlan = $false }) } | ConvertTo-Json -Depth 10 -Compress
$level = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName create_level -ParamsJson $levelParams | ConvertFrom-Json
$gridParams = @{ xGrids = @(@{ label = "EX1"; position = 0 }); yGrids = @(@{ label = "EY1"; position = 0 }); xExtentMin = 0; xExtentMax = 1000; yExtentMin = 0; yExtentMax = 1000; elevation = 0 } | ConvertTo-Json -Depth 10 -Compress
$grid = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName create_custom_grid -ParamsJson $gridParams | ConvertFrom-Json
$assertions = @(
  @{ name = "levelCommandSucceeded"; passed = [bool]$level.success; message = "" },
  @{ name = "gridCommandSucceeded"; passed = [bool]$grid.success; message = "" }
)
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-create-levels-grids"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20
```

- [ ] **Step 3: Create `eval-snapshot-statistics.ps1`**

```powershell
$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-snapshot-statistics"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}
$stats = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName analyze_model_statistics | ConvertFrom-Json
$snapshotParams = @{ includeImage = $false; includeVisibleElements = $true; pixelSize = 800 } | ConvertTo-Json -Depth 10 -Compress
$snapshot = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName snapshot_workspace -ParamsJson $snapshotParams | ConvertFrom-Json
$assertions = @(
  @{ name = "statisticsSucceeded"; passed = [bool]$stats.success; message = "" },
  @{ name = "snapshotSucceeded"; passed = [bool]$snapshot.success; message = "" }
)
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-snapshot-statistics"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20
```

- [ ] **Step 4: Create building and 3D view live evals**

Create `eval-create-basic-building.ps1`:

```powershell
$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\bootstrap.ps1' | ConvertFrom-Json
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
$floor = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName create_surface_based_element -ParamsJson $floorParams | ConvertFrom-Json
$wallParams = @{ data = @(@{ category = "OST_Walls"; locationLine = @{ p0 = @{ x = 0; y = 0; z = 0 }; p1 = @{ x = 3000; y = 0; z = 0 } }; thickness = 150; height = 3000; baseLevel = 0; baseOffset = 0 }) } | ConvertTo-Json -Depth 20 -Compress
$wall = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName create_line_based_element -ParamsJson $wallParams | ConvertFrom-Json
$assertions = @(
  @{ name = "floorCommandSucceeded"; passed = [bool]$floor.success; message = "" },
  @{ name = "wallCommandSucceeded"; passed = [bool]$wall.success; message = "" }
)
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-create-basic-building"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20
```

Create `eval-create-or-switch-3d-view.ps1`:

```powershell
$started = Get-Date
$bootstrap = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\bootstrap.ps1' | ConvertFrom-Json
if ($bootstrap.transports.legacyJsonRpc -ne "available") {
  [ordered]@{ name = "eval-create-or-switch-3d-view"; status = "skipped"; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = @(@{ name = "revitConnected"; passed = $false; message = "Revit JSON-RPC unavailable" }) } | ConvertTo-Json -Depth 10
  exit 0
}
$views = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName get_views | ConvertFrom-Json
$viewResult = $views.result
$threeDView = @($viewResult.views | Where-Object { $_.viewType -eq "ThreeD" -or $_.viewType -eq "ThreeDimensional" } | Select-Object -First 1)
$assertions = @(@{ name = "getViewsSucceeded"; passed = [bool]$views.success; message = "" })
if ($threeDView.Count -gt 0) {
  $params = @{ viewId = [int]$threeDView[0].id } | ConvertTo-Json -Compress
  $switch = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\invoke-command.ps1' -CommandName switch_view -ParamsJson $params | ConvertFrom-Json
  $assertions += @{ name = "switch3dViewSucceeded"; passed = [bool]$switch.success; message = "" }
} else {
  $assertions += @{ name = "threeDViewAvailable"; passed = $false; message = "No 3D view exists; this exposes the view_missing workflow." }
}
$status = if (@($assertions | Where-Object { -not $_.passed }).Count -eq 0) { "passed" } else { "failed" }
[ordered]@{ name = "eval-create-or-switch-3d-view"; status = $status; durationMs = [int]((Get-Date)-$started).TotalMilliseconds; assertions = $assertions } | ConvertTo-Json -Depth 20
```

- [ ] **Step 5: Commit live evals**

```powershell
git add tools/revit-harness/evals/live
git commit -m "feat: add live revit harness evals"
```

### Task 4: Run Phase 4 Verification

**Files:**
- Modify only if previous tasks fail.

- [ ] **Step 1: Run technical evals**

```powershell
$run = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\evals\run-evals.ps1' | ConvertFrom-Json
$run.summary
```

Expected: `failed = 0`.

- [ ] **Step 2: Run live evals with skip allowed**

```powershell
$run = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\evals\run-evals.ps1' -IncludeLive | ConvertFrom-Json
$run.summary
```

Expected: technical evals pass. Live evals pass when Revit is available or skip when unavailable.

- [ ] **Step 3: Verify results were written**

```powershell
Get-ChildItem 'tools/revit-harness/evals/results' -Filter '*.json' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
```

Expected: one recent JSON result file.

- [ ] **Step 4: Commit verification fixes if needed**

```powershell
git add tools/revit-harness/evals
git commit -m "fix: stabilize revit harness evals"
```

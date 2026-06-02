# Revit Harness Phase 2 Classification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add failure classification and lesson candidate generation on top of Phase 1 traces.

**Architecture:** Keep classification deterministic and fixture-driven. Scripts read trace/error JSON, return structured categories with evidence, and write reviewable lesson candidate JSON without editing project source.

**Tech Stack:** PowerShell 5+, JSON fixtures, Phase 1 trace format, Git.

---

## File Structure

- Create: `tools/revit-harness/classify-failure.ps1` - classify one error or trace.
- Create: `tools/revit-harness/generate-lesson-candidates.ps1` - aggregate traces into candidate lessons.
- Create: `tools/revit-harness/schemas/failure.schema.json` - classifier output contract.
- Create: `tools/revit-harness/schemas/lesson-candidate.schema.json` - lesson candidate contract.
- Create: `tools/revit-harness/fixtures/errors/*.json` - category fixtures.
- Create: `tools/revit-harness/fixtures/traces/*.json` - trace fixtures for candidate generation.

### Task 1: Add Failure and Lesson Schemas

**Files:**
- Create: `tools/revit-harness/schemas/failure.schema.json`
- Create: `tools/revit-harness/schemas/lesson-candidate.schema.json`

- [ ] **Step 1: Create failure schema**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["category", "confidence", "evidence", "suggestedNextAction", "patchTargets"],
  "properties": {
    "category": {
      "type": "string",
      "enum": [
        "stale_session",
        "schema_mismatch",
        "json_quoting",
        "missing_family",
        "invalid_geometry",
        "view_missing",
        "command_not_registered",
        "transport_unavailable",
        "command_bug",
        "skill_gap",
        "verification_gap",
        "unknown"
      ]
    },
    "confidence": { "type": "number" },
    "evidence": { "type": "array", "items": { "type": "string" } },
    "suggestedNextAction": { "type": "string" },
    "patchTargets": { "type": "array", "items": { "type": "string" } }
  }
}
```

- [ ] **Step 2: Create lesson candidate schema**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["candidateId", "createdAtUtc", "category", "frequency", "supportingTracePaths", "summary", "generalizedLesson", "recommendedPatchTargets", "risk", "requiresHumanApproval"],
  "properties": {
    "candidateId": { "type": "string" },
    "createdAtUtc": { "type": "string" },
    "category": { "type": "string" },
    "frequency": { "type": "integer" },
    "supportingTracePaths": { "type": "array", "items": { "type": "string" } },
    "summary": { "type": "string" },
    "generalizedLesson": { "type": "string" },
    "recommendedPatchTargets": { "type": "array", "items": { "type": "string" } },
    "risk": { "type": "string" },
    "requiresHumanApproval": { "type": "boolean" }
  }
}
```

- [ ] **Step 3: Commit**

```powershell
git add tools/revit-harness/schemas/failure.schema.json tools/revit-harness/schemas/lesson-candidate.schema.json
git commit -m "feat: add revit harness classification schemas"
```

### Task 2: Add Error Fixtures

**Files:**
- Create: `tools/revit-harness/fixtures/errors/json_quoting.json`
- Create: `tools/revit-harness/fixtures/errors/command_not_registered.json`
- Create: `tools/revit-harness/fixtures/errors/invalid_geometry.json`
- Create: `tools/revit-harness/fixtures/errors/view_missing.json`
- Create: `tools/revit-harness/fixtures/errors/verification_gap.json`

- [ ] **Step 1: Create fixture directory**

```powershell
New-Item -ItemType Directory -Force 'tools/revit-harness/fixtures/errors' | Out-Null
```

- [ ] **Step 2: Add fixtures**

Create `json_quoting.json`:

```json
{
  "command": "get_available_family_types",
  "transport": "none",
  "error": {
    "message": "ConvertFrom-Json : Invalid JSON primitive: OST_Walls.",
    "raw": "Invalid JSON primitive: OST_Walls."
  }
}
```

Create `command_not_registered.json`:

```json
{
  "command": "list_available_commands",
  "transport": "jsonrpc",
  "error": {
    "message": "Revit JSON-RPC error: {\"code\":-32601,\"message\":\"Method 'list_available_commands' not found\"}",
    "raw": "Method 'list_available_commands' not found"
  }
}
```

Create `invalid_geometry.json`:

```json
{
  "command": "create_sloped_roof",
  "transport": "jsonrpc",
  "result": {
    "Success": true,
    "Message": "Successfully created 0 sloped roof(s).\n\nWarnings:\n  - Failed to create roof 'Main': The roof is not created by pick walls.",
    "Response": []
  }
}
```

Create `view_missing.json`:

```json
{
  "command": "switch_view",
  "transport": "jsonrpc",
  "result": {
    "Success": false,
    "Message": "View not found. Name='{3D}', Id="
  }
}
```

Create `verification_gap.json`:

```json
{
  "trace": {
    "commands": [
      { "success": true, "command": "create_level" },
      { "success": true, "command": "create_line_based_element" }
    ],
    "snapshots": [],
    "modelStatistics": null,
    "warnings": null,
    "finalStatus": "completed"
  }
}
```

- [ ] **Step 3: Commit**

```powershell
git add tools/revit-harness/fixtures/errors
git commit -m "test: add revit harness failure fixtures"
```

### Task 3: Implement Failure Classifier

**Files:**
- Create: `tools/revit-harness/classify-failure.ps1`

- [ ] **Step 1: Run failing classifier command**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\classify-failure.ps1' -InputPath '.\tools\revit-harness\fixtures\errors\json_quoting.json'
```

Expected: FAIL because script does not exist.

- [ ] **Step 2: Create classifier script**

```powershell
param(
    [Parameter(Mandatory=$true)][string]$InputPath
)

$ErrorActionPreference = "Stop"
$inputObject = Get-Content -Path $InputPath -Raw | ConvertFrom-Json
$text = ($inputObject | ConvertTo-Json -Depth 50)

function New-Classification {
    param(
        [string]$Category,
        [double]$Confidence,
        [string[]]$Evidence,
        [string]$SuggestedNextAction,
        [string[]]$PatchTargets
    )
    [ordered]@{
        category = $Category
        confidence = $Confidence
        evidence = $Evidence
        suggestedNextAction = $SuggestedNextAction
        patchTargets = $PatchTargets
    }
}

if ($text -match 'Invalid JSON primitive|ConvertFrom-Json|Invalid object passed in') {
    New-Classification "json_quoting" 0.95 @("Input contains PowerShell JSON parsing failure.") "Pass params through invoke-command.ps1 using JSON file or canonical JSON." @("tools/revit-harness/invoke-command.ps1", ".agents/skills/run-revit-mcp/SKILL.md") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match 'Method .* not found|-32601') {
    New-Classification "command_not_registered" 0.92 @("Runtime bridge reported method not found.") "Run bootstrap and registry report to detect stale runtime or missing command registration." @("tools/revit-harness/bootstrap.ps1", "mcp-servers-for-revit/command.json") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match 'created 0 .*roof|not created by pick walls|invalid geometry') {
    New-Classification "invalid_geometry" 0.88 @("Command output indicates geometry was rejected or created zero elements.") "Retry with bounded valid geometry; if repeated, inspect command handler geometry assumptions." @("mcp-servers-for-revit/commandset/Services/CreateSlopedRoofEventHandler.cs", ".agents/skills/run-revit-mcp/SKILL.md") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match 'View not found') {
    New-Classification "view_missing" 0.9 @("Requested view does not exist in the active model.") "Create or discover a valid 3D view before switching, or snapshot the current view and report the limitation." @("mcp-servers-for-revit/commandset/Services/SwitchViewEventHandler.cs", "tools/revit-harness/evals/live/eval-create-or-switch-3d-view.ps1") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match '"finalStatus"\s*:\s*"completed"' -and $text -match '"snapshots"\s*:\s*\[\]' -and $text -match '"modelStatistics"\s*:\s*null') {
    New-Classification "verification_gap" 0.86 @("Trace completed without snapshot or model statistics.") "Run snapshot, statistics, and warnings check before final reporting." @(".agents/skills/run-revit-mcp/SKILL.md", "tools/revit-harness/trace-writer.ps1") | ConvertTo-Json -Depth 10
    exit 0
}

New-Classification "unknown" 0.2 @("No deterministic classifier rule matched.") "Record trace and review manually." @() | ConvertTo-Json -Depth 10
```

- [ ] **Step 3: Test known fixtures**

```powershell
$fixtures = Get-ChildItem 'tools/revit-harness/fixtures/errors/*.json'
foreach ($fixture in $fixtures) {
  $result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\classify-failure.ps1' -InputPath $fixture.FullName | ConvertFrom-Json
  "$($fixture.BaseName) => $($result.category)"
}
```

Expected:

```text
command_not_registered => command_not_registered
invalid_geometry => invalid_geometry
json_quoting => json_quoting
verification_gap => verification_gap
view_missing => view_missing
```

- [ ] **Step 4: Commit**

```powershell
git add tools/revit-harness/classify-failure.ps1
git commit -m "feat: add revit harness failure classifier"
```

### Task 4: Implement Lesson Candidate Generator

**Files:**
- Create: `tools/revit-harness/generate-lesson-candidates.ps1`
- Create: `tools/revit-harness/fixtures/traces/repeated_json_quoting_1.json`
- Create: `tools/revit-harness/fixtures/traces/repeated_json_quoting_2.json`

- [ ] **Step 1: Add repeated trace fixtures**

Create both fixture files with this structure, changing only `runId`:

```json
{
  "traceVersion": "1.0",
  "runId": "repeated_json_quoting_1",
  "timestampUtc": "2026-06-02T00:00:00Z",
  "workspaceRoot": "fixture",
  "commands": [],
  "classifiedFailures": [
    {
      "category": "json_quoting",
      "confidence": 0.95,
      "evidence": ["Input contains PowerShell JSON parsing failure."],
      "suggestedNextAction": "Pass params through invoke-command.ps1 using JSON file or canonical JSON.",
      "patchTargets": ["tools/revit-harness/invoke-command.ps1"]
    }
  ],
  "finalStatus": "failed"
}
```

- [ ] **Step 2: Create generator script**

```powershell
param(
    [string]$TraceDirectory = "tools/revit-harness/fixtures/traces",
    [string]$OutputDirectory = ".revit-harness/lesson-candidates",
    [int]$MinimumFrequency = 2
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$groups = @{}
Get-ChildItem -Path $TraceDirectory -Filter "*.json" -File | ForEach-Object {
    $trace = Get-Content -Path $_.FullName -Raw | ConvertFrom-Json
    foreach ($failure in @($trace.classifiedFailures)) {
        if (-not $groups.ContainsKey($failure.category)) {
            $groups[$failure.category] = @()
        }
        $groups[$failure.category] += [ordered]@{
            tracePath = $_.FullName
            failure = $failure
        }
    }
}

$created = @()
foreach ($category in $groups.Keys) {
    $items = @($groups[$category])
    if ($items.Count -lt $MinimumFrequency) {
        continue
    }

    $firstFailure = $items[0].failure
    $candidate = [ordered]@{
        candidateId = "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $category
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        category = $category
        frequency = $items.Count
        supportingTracePaths = @($items | ForEach-Object { $_.tracePath })
        summary = "Repeated $category failure across $($items.Count) traces."
        generalizedLesson = $firstFailure.suggestedNextAction
        recommendedPatchTargets = @($firstFailure.patchTargets)
        risk = "Review before applying because generated candidates may overfit fixture or session-specific failures."
        requiresHumanApproval = $true
    }

    $path = Join-Path $OutputDirectory ($candidate.candidateId + ".json")
    $candidate | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding UTF8
    $created += $path
}

[ordered]@{
    success = $true
    createdCount = $created.Count
    createdPaths = $created
} | ConvertTo-Json -Depth 20
```

- [ ] **Step 3: Test candidate generation**

```powershell
$result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\generate-lesson-candidates.ps1' -TraceDirectory 'tools/revit-harness/fixtures/traces' -OutputDirectory '.revit-harness/lesson-candidates-test' | ConvertFrom-Json
$result.createdCount
```

Expected: `1`.

- [ ] **Step 4: Commit**

```powershell
git add tools/revit-harness/generate-lesson-candidates.ps1 tools/revit-harness/fixtures/traces
git commit -m "feat: add revit harness lesson candidate generator"
```

### Task 5: Phase 2 Verification

**Files:**
- Modify only if previous tasks fail.

- [ ] **Step 1: Run classifier fixture verification**

```powershell
$expected = @{
  "json_quoting" = "json_quoting"
  "command_not_registered" = "command_not_registered"
  "invalid_geometry" = "invalid_geometry"
  "view_missing" = "view_missing"
  "verification_gap" = "verification_gap"
}
foreach ($name in $expected.Keys) {
  $result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\classify-failure.ps1' -InputPath "tools/revit-harness/fixtures/errors/$name.json" | ConvertFrom-Json
  if ($result.category -ne $expected[$name]) { throw "$name classified as $($result.category)" }
}
"PASS"
```

Expected: `PASS`.

- [ ] **Step 2: Run generator verification**

```powershell
Remove-Item -Recurse -Force '.revit-harness/lesson-candidates-test' -ErrorAction SilentlyContinue
$result = powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\revit-harness\generate-lesson-candidates.ps1' -TraceDirectory 'tools/revit-harness/fixtures/traces' -OutputDirectory '.revit-harness/lesson-candidates-test' | ConvertFrom-Json
if ($result.createdCount -ne 1) { throw "Expected 1 candidate" }
"PASS"
```

Expected: `PASS`.

- [ ] **Step 3: Commit verification adjustments**

If fixes were needed:

```powershell
git add tools/revit-harness
git commit -m "fix: stabilize revit harness classification"
```


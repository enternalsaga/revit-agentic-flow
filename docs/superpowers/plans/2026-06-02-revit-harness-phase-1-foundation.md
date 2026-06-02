# Revit Harness Phase 1 Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Revit harness layer: runtime bootstrap, command registry report, safe command invocation, and trace writing.

**Architecture:** Implement a small PowerShell harness under `src/RevitHarness/` because the project already uses PowerShell for Revit bridge workflows. Keep runtime output under `.revit-harness/` and source fixtures under `src/RevitHarness/fixtures/`.

**Tech Stack:** PowerShell 5+, JSON, existing Revit JSON-RPC bridge script, Git, optional live Revit smoke tests.

---

## File Structure

- Create: `src/RevitHarness/bootstrap.ps1` - source/runtime discovery entrypoint.
- Create: `src/RevitHarness/registry-report.ps1` - source-layer command coverage report.
- Create: `src/RevitHarness/invoke-command.ps1` - safe command invocation wrapper.
- Create: `src/RevitHarness/trace-writer.ps1` - trace creation and append helpers.
- Create: `src/RevitHarness/schemas/bootstrap.schema.json` - documented bootstrap output schema.
- Create: `src/RevitHarness/schemas/command-result.schema.json` - documented invocation envelope schema.
- Create: `src/RevitHarness/schemas/trace.schema.json` - documented trace schema.
- Create: `src/RevitHarness/fixtures/sample-bootstrap.json` - stable fixture for report consumers.
- Modify: `.gitignore` - ignore `.revit-harness/runs/`, `.revit-harness/cache/`, and `.revit-harness/lesson-candidates/`.

### Task 1: Add Runtime Output Ignore Rules

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Inspect current ignore rules**

Run:

```powershell
Select-String -Path '.gitignore' -Pattern '\.revit-harness|src/RevitHarness' -CaseSensitive:$false
```

Expected: either no output or existing `.revit-harness` entries to preserve.

- [ ] **Step 2: Add ignore rules**

Append these lines if they do not already exist:

```gitignore
# Revit harness runtime output
.revit-harness/runs/
.revit-harness/cache/
.revit-harness/lesson-candidates/
```

- [ ] **Step 3: Verify ignore behavior**

Run:

```powershell
New-Item -ItemType Directory -Force '.revit-harness/runs/test' | Out-Null
Set-Content -Path '.revit-harness/runs/test/trace.json' -Value '{}'
git status --short -- '.revit-harness/runs/test/trace.json'
```

Expected: no output.

- [ ] **Step 4: Commit**

```powershell
git add .gitignore
git commit -m "chore: ignore revit harness runtime output"
```

### Task 2: Create Registry Report Script

**Files:**
- Create: `src/RevitHarness/registry-report.ps1`

- [ ] **Step 1: Write the failing smoke check**

Run before the file exists:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\registry-report.ps1' | ConvertFrom-Json
```

Expected: FAIL because `registry-report.ps1` does not exist.

- [ ] **Step 2: Create `registry-report.ps1`**

Add this implementation:

```powershell
param(
    [string]$WorkspaceRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

function Get-RelativeNameSet {
    param(
        [string]$Path,
        [string]$Pattern,
        [scriptblock]$Transform
    )

    if (-not (Test-Path $Path)) {
        return @()
    }

    Get-ChildItem -Path $Path -Filter $Pattern -File |
        ForEach-Object { & $Transform $_ } |
        Where-Object { $_ } |
        Sort-Object -Unique
}

$commandJsonPath = Join-Path $WorkspaceRoot "mcp-servers-for-revit/command.json"
$tsToolsPath = Join-Path $WorkspaceRoot "mcp-servers-for-revit/server/src/tools"
$csharpToolsPath = Join-Path $WorkspaceRoot "src/RevitMcpServer/Tools"
$commandsetPath = Join-Path $WorkspaceRoot "mcp-servers-for-revit/commandset/Commands"

if (-not (Test-Path $commandJsonPath)) {
    throw "Missing command manifest: $commandJsonPath"
}

$manifest = Get-Content -Path $commandJsonPath -Raw | ConvertFrom-Json
$manifestCommands = @($manifest.commands | ForEach-Object { $_.commandName } | Sort-Object -Unique)

$typescriptTools = Get-RelativeNameSet -Path $tsToolsPath -Pattern "*.ts" -Transform {
    param($file)
    if ($file.BaseName -in @("register", "index")) { return $null }
    return $file.BaseName
}

$csharpMcpTools = @()
if (Test-Path $csharpToolsPath) {
    $csharpMcpTools = Get-ChildItem -Path $csharpToolsPath -Filter "*.cs" -File |
        ForEach-Object {
            Select-String -Path $_.FullName -Pattern 'McpServerTool\(Name\s*=\s*"([^"]+)"' -AllMatches |
                ForEach-Object { $_.Matches.Groups[1].Value }
        } |
        Sort-Object -Unique
}

$commandsetImplementations = @()
if (Test-Path $commandsetPath) {
    $commandsetImplementations = Get-ChildItem -Path $commandsetPath -Filter "*.cs" -Recurse -File |
        ForEach-Object {
            Select-String -Path $_.FullName -Pattern 'CommandName\s*=>\s*"([^"]+)"' -AllMatches |
                ForEach-Object { $_.Matches.Groups[1].Value }
        } |
        Sort-Object -Unique
}

function Compare-Layers {
    param([string[]]$Left, [string[]]$Right)
    $rightSet = @{}
    foreach ($item in $Right) { $rightSet[$item] = $true }
    @($Left | Where-Object { -not $rightSet.ContainsKey($_) })
}

$report = [ordered]@{
    success = $true
    workspaceRoot = $WorkspaceRoot
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    manifestCommands = $manifestCommands
    typescriptTools = $typescriptTools
    csharpMcpTools = $csharpMcpTools
    commandsetImplementations = $commandsetImplementations
    runtimeRegisteredCommands = $null
    counts = [ordered]@{
        manifest = $manifestCommands.Count
        typescriptTools = $typescriptTools.Count
        csharpMcpTools = $csharpMcpTools.Count
        commandsetImplementations = $commandsetImplementations.Count
        runtimeRegisteredCommands = $null
    }
    coverageGaps = [ordered]@{
        manifest_without_typescript_tool = Compare-Layers $manifestCommands $typescriptTools
        manifest_without_csharp_wrapper = Compare-Layers $manifestCommands $csharpMcpTools
        manifest_without_commandset_implementation = Compare-Layers $manifestCommands $commandsetImplementations
        typescript_tool_without_manifest = Compare-Layers $typescriptTools $manifestCommands
        csharp_wrapper_without_manifest = Compare-Layers $csharpMcpTools $manifestCommands
        runtime_missing_manifest_command = $null
        runtime_extra_command = $null
    }
}

$report | ConvertTo-Json -Depth 20
```

- [ ] **Step 3: Run registry report**

```powershell
$report = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\registry-report.ps1' | ConvertFrom-Json
$report.success
$report.counts
```

Expected: `True`, then counts for manifest/tools/wrappers/implementations.

- [ ] **Step 4: Commit**

```powershell
git add src/RevitHarness/registry-report.ps1
git commit -m "feat: add revit harness registry report"
```

### Task 3: Create Safe Command Invocation Wrapper

**Files:**
- Create: `src/RevitHarness/invoke-command.ps1`

- [ ] **Step 1: Write the failing smoke check**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName get_project_info
```

Expected: FAIL because the file does not exist.

- [ ] **Step 2: Create `invoke-command.ps1`**

```powershell
param(
    [Parameter(Mandatory=$true)][string]$CommandName,
    [string]$ParamsJson = "{}",
    [string]$ParamsPath = "",
    [ValidateSet("auto", "jsonrpc")][string]$Transport = "auto",
    [int]$TimeoutSeconds = 120,
    [string]$TraceRunId = ""
)

$ErrorActionPreference = "Stop"
$workspaceRoot = (Resolve-Path ".").Path
$bridgeScript = Join-Path $workspaceRoot ".agents/skills/run-revit-mcp/scripts/Invoke-RevitMcpJsonRpc.ps1"
$requestId = "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), ([guid]::NewGuid().ToString("N").Substring(0, 8))
$started = Get-Date

if ($ParamsPath) {
    $rawParams = Get-Content -Path $ParamsPath -Raw
} else {
    $rawParams = $ParamsJson
}

try {
    $parsedParams = $rawParams | ConvertFrom-Json
    $canonicalParams = $parsedParams | ConvertTo-Json -Depth 50 -Compress
} catch {
    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    [ordered]@{
        success = $false
        command = $CommandName
        transport = "none"
        durationMs = $duration
        requestId = $requestId
        paramsHash = $null
        result = $null
        error = [ordered]@{
            categoryHint = "json_quoting"
            message = $_.Exception.Message
            raw = $rawParams
        }
    } | ConvertTo-Json -Depth 20
    exit 1
}

$sha = [System.Security.Cryptography.SHA256]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($canonicalParams)
$hash = "sha256:" + (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "")

try {
    if (-not (Test-Path $bridgeScript)) {
        throw "Missing JSON-RPC bridge script: $bridgeScript"
    }

    $tempParams = New-TemporaryFile
    Set-Content -Path $tempParams.FullName -Value $canonicalParams -Encoding UTF8

    $job = Start-Job -ScriptBlock {
        param($scriptPath, $method, $paramsFile)
        $json = Get-Content -Path $paramsFile -Raw
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -Method $method -ParamsJson $json
    } -ArgumentList $bridgeScript, $CommandName, $tempParams.FullName

    if (-not (Wait-Job $job -Timeout $TimeoutSeconds)) {
        Stop-Job $job
        throw "Command timed out after $TimeoutSeconds seconds"
    }

    $output = Receive-Job $job -ErrorAction Stop
    Remove-Job $job
    Remove-Item -LiteralPath $tempParams.FullName -Force

    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    [ordered]@{
        success = $true
        command = $CommandName
        transport = "jsonrpc"
        durationMs = $duration
        requestId = $requestId
        paramsHash = $hash
        result = ($output -join "`n" | ConvertFrom-Json)
        error = $null
    } | ConvertTo-Json -Depth 50
} catch {
    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    [ordered]@{
        success = $false
        command = $CommandName
        transport = "jsonrpc"
        durationMs = $duration
        requestId = $requestId
        paramsHash = $hash
        result = $null
        error = [ordered]@{
            categoryHint = "command_error"
            message = $_.Exception.Message
            raw = $_.ToString()
        }
    } | ConvertTo-Json -Depth 20
    exit 1
}
```

- [ ] **Step 3: Test invalid JSON classification**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName get_project_info -ParamsJson '{bad json}' | ConvertFrom-Json
```

Expected: `success = false`, `error.categoryHint = json_quoting`.

- [ ] **Step 4: Live smoke test**

Run when Revit and the plugin are open:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\invoke-command.ps1' -CommandName get_project_info | ConvertFrom-Json
```

Expected: `success = true` and `transport = jsonrpc`.

- [ ] **Step 5: Commit**

```powershell
git add src/RevitHarness/invoke-command.ps1
git commit -m "feat: add safe revit command invocation wrapper"
```

### Task 4: Add Bootstrap Script

**Files:**
- Create: `src/RevitHarness/bootstrap.ps1`
- Create: `src/RevitHarness/fixtures/sample-bootstrap.json`

- [ ] **Step 1: Write failing bootstrap check**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1'
```

Expected: FAIL because file does not exist.

- [ ] **Step 2: Create `bootstrap.ps1`**

```powershell
param(
    [string]$WorkspaceRoot = (Resolve-Path ".").Path,
    [switch]$WriteCache
)

$ErrorActionPreference = "Stop"
$registryScript = Join-Path $WorkspaceRoot "src/RevitHarness/registry-report.ps1"
$invokeScript = Join-Path $WorkspaceRoot "src/RevitHarness/invoke-command.ps1"

$registry = powershell -NoProfile -ExecutionPolicy Bypass -File $registryScript -WorkspaceRoot $WorkspaceRoot | ConvertFrom-Json

$jsonRpcStatus = "unavailable"
$projectInfo = $null
try {
    $probe = powershell -NoProfile -ExecutionPolicy Bypass -File $invokeScript -CommandName get_project_info -ParamsJson "{}" -TimeoutSeconds 10 | ConvertFrom-Json
    if ($probe.success) {
        $jsonRpcStatus = "available"
        $projectInfo = $probe.result
    }
} catch {
    $jsonRpcStatus = "unavailable"
}

$phase1Status = "unknown"

$bootstrap = [ordered]@{
    success = $true
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    workspaceRoot = $WorkspaceRoot
    transports = [ordered]@{
        phase1Pipe = $phase1Status
        legacyJsonRpc = $jsonRpcStatus
    }
    projectInfo = $projectInfo
    registry = $registry
    guidance = @()
}

if ($jsonRpcStatus -eq "available") {
    $bootstrap.guidance += "Use invoke-command.ps1 for JSON-RPC fallback calls."
} else {
    $bootstrap.guidance += "Open Revit and ensure the MCP plugin is loaded before live command execution."
}

if ($WriteCache) {
    $cacheDir = Join-Path $WorkspaceRoot ".revit-harness/cache"
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $bootstrap | ConvertTo-Json -Depth 50 | Set-Content -Path (Join-Path $cacheDir "last-bootstrap.json") -Encoding UTF8
}

$bootstrap | ConvertTo-Json -Depth 50
```

- [ ] **Step 3: Add sample fixture**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' | Set-Content -Path '.\src\RevitHarness\fixtures\sample-bootstrap.json'
```

Expected: `sample-bootstrap.json` exists and contains `transports`, `registry`, and `guidance`.

- [ ] **Step 4: Commit**

```powershell
git add src/RevitHarness/bootstrap.ps1 src/RevitHarness/fixtures/sample-bootstrap.json
git commit -m "feat: add revit harness bootstrap"
```

### Task 5: Add Trace Writer and Schemas

**Files:**
- Create: `src/RevitHarness/trace-writer.ps1`
- Create: `src/RevitHarness/schemas/bootstrap.schema.json`
- Create: `src/RevitHarness/schemas/command-result.schema.json`
- Create: `src/RevitHarness/schemas/trace.schema.json`

- [ ] **Step 1: Create schema files**

Create `trace.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["traceVersion", "runId", "timestampUtc", "workspaceRoot", "commands", "finalStatus"],
  "properties": {
    "traceVersion": { "type": "string" },
    "runId": { "type": "string" },
    "timestampUtc": { "type": "string" },
    "workspaceRoot": { "type": "string" },
    "taskLabel": { "type": "string" },
    "userIntent": { "type": "string" },
    "agentPlan": { "type": "array" },
    "bootstrap": { "type": ["object", "null"] },
    "commands": { "type": "array" },
    "snapshots": { "type": "array" },
    "modelStatistics": { "type": ["object", "null"] },
    "warnings": { "type": ["object", "null"] },
    "classifiedFailures": { "type": "array" },
    "lessonCandidates": { "type": "array" },
    "finalStatus": { "type": "string" }
  }
}
```

Create `bootstrap.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["success", "generatedAtUtc", "workspaceRoot", "transports", "registry", "guidance"],
  "properties": {
    "success": { "type": "boolean" },
    "generatedAtUtc": { "type": "string" },
    "workspaceRoot": { "type": "string" },
    "transports": {
      "type": "object",
      "required": ["phase1Pipe", "legacyJsonRpc"],
      "properties": {
        "phase1Pipe": { "type": "string" },
        "legacyJsonRpc": { "type": "string" }
      }
    },
    "projectInfo": { "type": ["object", "null"] },
    "registry": { "type": "object" },
    "guidance": { "type": "array", "items": { "type": "string" } }
  }
}
```

Create `command-result.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["success", "command", "transport", "durationMs", "requestId", "paramsHash", "result", "error"],
  "properties": {
    "success": { "type": "boolean" },
    "command": { "type": "string" },
    "transport": { "type": "string" },
    "durationMs": { "type": "integer" },
    "requestId": { "type": "string" },
    "paramsHash": { "type": ["string", "null"] },
    "result": {},
    "error": {
      "type": ["object", "null"],
      "properties": {
        "categoryHint": { "type": "string" },
        "message": { "type": "string" },
        "raw": {}
      }
    }
  }
}
```

- [ ] **Step 2: Create `trace-writer.ps1`**

```powershell
param(
    [ValidateSet("new", "append-command", "finalize")][string]$Mode = "new",
    [string]$RunId = "",
    [string]$TaskLabel = "revit-task",
    [string]$UserIntent = "",
    [string]$CommandResultPath = "",
    [string]$FinalStatus = "in_progress"
)

$ErrorActionPreference = "Stop"
$workspaceRoot = (Resolve-Path ".").Path

function New-RunId {
    param([string]$Label)
    $slug = ($Label.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if (-not $slug) { $slug = "revit-task" }
    "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $slug
}

if (-not $RunId) {
    $RunId = New-RunId -Label $TaskLabel
}

$runDir = Join-Path $workspaceRoot ".revit-harness/runs/$RunId"
$tracePath = Join-Path $runDir "trace.json"

if ($Mode -eq "new") {
    New-Item -ItemType Directory -Force -Path $runDir | Out-Null
    $trace = [ordered]@{
        traceVersion = "1.0"
        runId = $RunId
        timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        workspaceRoot = $workspaceRoot
        taskLabel = $TaskLabel
        userIntent = $UserIntent
        agentPlan = @()
        bootstrap = $null
        commands = @()
        snapshots = @()
        modelStatistics = $null
        warnings = $null
        classifiedFailures = @()
        lessonCandidates = @()
        finalStatus = "in_progress"
    }
    $trace | ConvertTo-Json -Depth 50 | Set-Content -Path $tracePath -Encoding UTF8
    [ordered]@{ success = $true; runId = $RunId; tracePath = $tracePath } | ConvertTo-Json
    exit 0
}

if (-not (Test-Path $tracePath)) {
    throw "Trace not found: $tracePath"
}

$trace = Get-Content -Path $tracePath -Raw | ConvertFrom-Json

if ($Mode -eq "append-command") {
    if (-not (Test-Path $CommandResultPath)) {
        throw "Command result file not found: $CommandResultPath"
    }
    $commandResult = Get-Content -Path $CommandResultPath -Raw | ConvertFrom-Json
    $commands = @($trace.commands)
    $commands += $commandResult
    $trace.commands = $commands
}

if ($Mode -eq "finalize") {
    $trace.finalStatus = $FinalStatus
}

$trace | ConvertTo-Json -Depth 50 | Set-Content -Path $tracePath -Encoding UTF8
[ordered]@{ success = $true; runId = $RunId; tracePath = $tracePath } | ConvertTo-Json
```

- [ ] **Step 3: Test trace creation**

```powershell
$created = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\trace-writer.ps1' -Mode new -TaskLabel phase1-smoke -UserIntent "smoke" | ConvertFrom-Json
Test-Path $created.tracePath
```

Expected: `True`.

- [ ] **Step 4: Commit**

```powershell
git add src/RevitHarness/trace-writer.ps1 src/RevitHarness/schemas
git commit -m "feat: add revit harness trace writer"
```

### Task 6: Phase 1 End-to-End Verification

**Files:**
- Modify only if a previous task failed.

- [ ] **Step 1: Run local registry report**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\registry-report.ps1' | ConvertFrom-Json | Select-Object success, counts
```

Expected: `success = True`.

- [ ] **Step 2: Run bootstrap**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\bootstrap.ps1' -WriteCache | ConvertFrom-Json | Select-Object success, transports
```

Expected: `success = True`. Revit transport may be `available` or `unavailable` depending on environment.

- [ ] **Step 3: Run trace smoke**

```powershell
$run = powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\trace-writer.ps1' -Mode new -TaskLabel phase1-final-smoke | ConvertFrom-Json
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\RevitHarness\trace-writer.ps1' -Mode finalize -RunId $run.runId -FinalStatus passed | ConvertFrom-Json
Get-Content $run.tracePath -Raw | ConvertFrom-Json | Select-Object runId, finalStatus
```

Expected: `finalStatus = passed`.

- [ ] **Step 4: Commit verification adjustments**

If no files changed, skip this commit. If fixes were needed:

```powershell
git add src/RevitHarness .gitignore
git commit -m "fix: stabilize revit harness foundation"
```

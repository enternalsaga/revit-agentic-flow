# Revit Harness Phase 3 Skill Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the Revit harness into `run-revit-mcp` and refactor long operational guidance into scoped references.

**Architecture:** Keep the main skill as an orchestration workflow. Move tool tables, fallback payloads, failure taxonomy, and verification details into reference files loaded only when relevant.

**Tech Stack:** Markdown skills, local PowerShell harness scripts from Phases 1-2, Revit MCP project conventions.

---

## File Structure

- Modify: `.agents/skills/run-revit-mcp/SKILL.md`
- Create: `.agents/skills/run-revit-mcp/references/tool-reference.md`
- Create: `.agents/skills/run-revit-mcp/references/fallbacks.md`
- Create: `.agents/skills/run-revit-mcp/references/failure-taxonomy.md`
- Create: `.agents/skills/run-revit-mcp/references/verification-checklist.md`
- Modify optional: `.agents/workflows/start.md`

### Task 1: Extract Tool Reference

**Files:**
- Create: `.agents/skills/run-revit-mcp/references/tool-reference.md`

- [ ] **Step 1: Create reference directory**

```powershell
New-Item -ItemType Directory -Force '.agents/skills/run-revit-mcp/references' | Out-Null
```

- [ ] **Step 2: Create `tool-reference.md`**

```markdown
# Revit MCP Tool Reference

Use this reference when mapping a modeling task to dedicated Revit MCP commands.

## Creation

| Task | Preferred Tool |
|------|----------------|
| Levels | `create_level` |
| Uniform grids | `create_grid` |
| Custom grids | `create_custom_grid` |
| Walls | `create_line_based_element` with `OST_Walls` |
| Structural framing | `create_line_based_element` with `OST_StructuralFraming` |
| Floors | `create_surface_based_element` with `OST_Floors` |
| Flat roofs | `create_surface_based_element` with `OST_Roofs` |
| Sloped roofs | `create_sloped_roof` |
| Structural columns | `create_structural_column` |
| Braces | `create_brace` |
| Curtain walls | `create_curtain_wall` |
| Parametric doors | `create_parametric_door` |
| Point-based families | `create_point_based_element` |
| Rooms | `create_room` |
| Dimensions | `create_dimensions` |

## Modification

| Task | Preferred Tool |
|------|----------------|
| Delete | `delete_element` |
| Select, hide, isolate, move, color | `operate_element` |
| Edit wall profile | `edit_wall_profile` |
| Set one parameter | `set_element_parameter` |
| Set many parameters | `set_parameter_bulk` |
| Filter and set parameters | `filter_and_set_parameter` |
| Copy parameters | `copy_parameters` |
| Change materials | `batch_change_materials` |

## Query and Verification

| Task | Preferred Tool |
|------|----------------|
| Family types | `get_available_family_types` |
| Views | `get_views` |
| Current view | `get_current_view_info` |
| Switch view | `switch_view` |
| Snapshot | `snapshot_workspace` |
| Statistics | `analyze_model_statistics` |
| Warnings | `get_warnings` |
| Verify elements | `verify_elements` |
| Filter elements | `ai_element_filter` |

`send_code_to_revit` is last resort for operations without dedicated tool coverage, such as custom DirectShape geometry, geometry join, attach wall top/base, or complex family type manipulation.
```

- [ ] **Step 3: Commit**

```powershell
git add .agents/skills/run-revit-mcp/references/tool-reference.md
git commit -m "docs: add revit mcp tool reference"
```

### Task 2: Extract Fallback Guidance

**Files:**
- Create: `.agents/skills/run-revit-mcp/references/fallbacks.md`

- [ ] **Step 1: Create `fallbacks.md`**

```markdown
# Revit MCP Fallbacks

Use this reference only when direct MCP tools are unavailable, stale, or fail with evidence.

## Runtime Discovery Order

1. Run `src/RevitHarness/bootstrap.ps1`.
2. Prefer direct MCP tools exposed to the agent.
3. If direct MCP discovery is stale but Revit runtime has the command, call `src/RevitHarness/invoke-command.ps1`.
4. Use compiled helper DLL flow only for large C# payloads or custom geometry.
5. Use inline `send_code_to_revit` only when the command wrapper and compiled helper flow are inappropriate.

## JSON-RPC Invocation

Use params files or canonical JSON through `invoke-command.ps1` to avoid PowerShell quoting errors.

Example:

```powershell
$params = @{ data = @(@{ name = "L1"; elevation = 0 }) } | ConvertTo-Json -Depth 10
$paramsPath = New-TemporaryFile
Set-Content -Path $paramsPath.FullName -Value $params -Encoding UTF8
powershell -NoProfile -ExecutionPolicy Bypass -File '.\src\\RevitHarness\invoke-command.ps1' -CommandName create_level -ParamsPath $paramsPath.FullName
```

## Compiled Helper Flow

Use `.agents/skills/run-revit-mcp/scripts/Invoke-RevitMcpJsonRpc.ps1` with `-SourcePath`, `-TypeName`, and `-MethodName` for long C# snippets.

Do not create a nested Revit `Transaction` inside helper code when the bridge already wraps execution in a transaction.
```

- [ ] **Step 2: Commit**

```powershell
git add .agents/skills/run-revit-mcp/references/fallbacks.md
git commit -m "docs: add revit mcp fallback reference"
```

### Task 3: Extract Failure Taxonomy and Verification Checklist

**Files:**
- Create: `.agents/skills/run-revit-mcp/references/failure-taxonomy.md`
- Create: `.agents/skills/run-revit-mcp/references/verification-checklist.md`

- [ ] **Step 1: Create `failure-taxonomy.md`**

```markdown
# Revit MCP Failure Taxonomy

Use this reference when a command fails, retries, falls back, or verification is incomplete.

| Category | Evidence | First Action |
|----------|----------|--------------|
| `stale_session` | Source has tool/command but client or runtime cannot see it | Run bootstrap and use runtime fallback if available |
| `schema_mismatch` | Command exists but params shape fails | Compare tool schema, C# model, and command handler |
| `json_quoting` | `ConvertFrom-Json` or invalid JSON primitive | Use params file through `invoke-command.ps1` |
| `missing_family` | Revit cannot find family/type | Call `get_available_family_types` |
| `invalid_geometry` | Zero created elements or Revit geometry rejection | Retry with simpler valid geometry |
| `view_missing` | `View not found` | Discover views or create a 3D view helper |
| `command_not_registered` | JSON-RPC method not found | Check command manifest and deployed runtime |
| `transport_unavailable` | Named pipe and JSON-RPC unreachable | Ask user to open Revit/load plugin |
| `command_bug` | Valid command and params return internal exception | Create trace and propose command patch |
| `skill_gap` | Agent selected wrong tool or skipped required step | Propose skill update |
| `verification_gap` | No snapshot/statistics/warnings after modeling | Run verification before final report |

Classify through `src/RevitHarness/classify-failure.ps1` when Phase 2 harness is available.
```

- [ ] **Step 2: Create `verification-checklist.md`**

```markdown
# Revit MCP Verification Checklist

Run this checklist before reporting a modeling task as complete.

## Required Checks

1. Verify expected element IDs still exist with `verify_elements` when IDs were captured.
2. Run `snapshot_workspace` with image and visible elements when a view is available.
3. Run `analyze_model_statistics`.
4. Run `get_warnings`.
5. Compare created categories and counts to the plan.
6. If snapshot or statistics cannot run, write a trace and report the exact blocker.

## Final Report Fields

- Completed tasks and tools used.
- Failed or skipped tasks with classified reason.
- Snapshot path or reason snapshot was unavailable.
- Element counts from statistics.
- Warnings count.
- Trace path when trace exists.
- Tool usage summary.
```

- [ ] **Step 3: Commit**

```powershell
git add .agents/skills/run-revit-mcp/references/failure-taxonomy.md .agents/skills/run-revit-mcp/references/verification-checklist.md
git commit -m "docs: add revit mcp failure and verification references"
```

### Task 4: Refactor Main Skill to Use Harness

**Files:**
- Modify: `.agents/skills/run-revit-mcp/SKILL.md`

- [ ] **Step 1: Save pre-change line count**

```powershell
(Get-Content '.agents/skills/run-revit-mcp/SKILL.md').Count
```

Expected: note the number for comparison.

- [ ] **Step 2: Add harness bootstrap requirement near Phase 2**

Insert this section after "Map to MCP Tools":

```markdown
### Harness Bootstrap

Before executing or planning fallback calls, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\\RevitHarness\bootstrap.ps1 -WriteCache
```

Use the bootstrap report as runtime truth:

- If direct MCP tools are visible, prefer them.
- If direct MCP discovery is stale but JSON-RPC is available, use `src/RevitHarness/invoke-command.ps1`.
- If no transport is available, stop and report that Revit/plugin is not connected.
- If a command fails, save or update a trace and classify the failure when the classifier exists.
```

- [ ] **Step 3: Add reference loading rules**

Insert:

```markdown
### Reference Loading

Load references only when needed:

- `references/tool-reference.md` when mapping tasks to tools.
- `references/fallbacks.md` when a dedicated tool is unavailable or fails.
- `references/failure-taxonomy.md` when classifying an error.
- `references/verification-checklist.md` before final reporting.
```

- [ ] **Step 4: Replace inline fallback examples with reference pointer**

Find long JSON-RPC fallback examples and replace the block with:

```markdown
For JSON-RPC fallback and compiled helper flow, use `references/fallbacks.md`. Do not hand-write shell-quoted JSON when `src/RevitHarness/invoke-command.ps1` is available.
```

- [ ] **Step 5: Verify skill still mentions dedicated tools first**

Run:

```powershell
Select-String -Path '.agents/skills/run-revit-mcp/SKILL.md' -Pattern 'send_code_to_revit.*LAST RESORT|Harness Bootstrap|Reference Loading|invoke-command.ps1'
```

Expected: all patterns appear.

- [ ] **Step 6: Commit**

```powershell
git add .agents/skills/run-revit-mcp/SKILL.md
git commit -m "docs: integrate revit harness into modeling skill"
```

### Task 5: Update Start Workflow

**Files:**
- Modify: `.agents/workflows/start.md`

- [ ] **Step 1: Add bootstrap check to start workflow**

Add this after the Revit connection check:

```markdown
## BÆ°á»›c 2b: Kiá»ƒm tra Revit Harness

Cháº¡y:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\\RevitHarness\bootstrap.ps1 -WriteCache
```

BÃ¡o cÃ¡o ngáº¯n:

- Transport kháº£ dá»¥ng.
- Sá»‘ command manifest/tool wrapper.
- Drift hoáº·c stale session náº¿u cÃ³.
```

- [ ] **Step 2: Verify workflow references harness**

```powershell
Select-String -Path '.agents/workflows/start.md' -Pattern 'revit-harness|bootstrap.ps1|Transport'
```

Expected: the new section appears.

- [ ] **Step 3: Commit**

```powershell
git add .agents/workflows/start.md
git commit -m "docs: add revit harness bootstrap to start workflow"
```

### Task 6: Phase 3 Verification

**Files:**
- Modify only if previous tasks fail.

- [ ] **Step 1: Check references exist**

```powershell
Test-Path '.agents/skills/run-revit-mcp/references/tool-reference.md'
Test-Path '.agents/skills/run-revit-mcp/references/fallbacks.md'
Test-Path '.agents/skills/run-revit-mcp/references/failure-taxonomy.md'
Test-Path '.agents/skills/run-revit-mcp/references/verification-checklist.md'
```

Expected: four `True` lines.

- [ ] **Step 2: Check skill has harness hooks**

```powershell
$skill = Get-Content '.agents/skills/run-revit-mcp/SKILL.md' -Raw
if ($skill -notmatch 'Harness Bootstrap') { throw 'Missing Harness Bootstrap' }
if ($skill -notmatch 'invoke-command.ps1') { throw 'Missing invoke-command reference' }
if ($skill -notmatch 'send_code_to_revit.*LAST RESORT') { throw 'Missing last resort policy' }
"PASS"
```

Expected: `PASS`.

- [ ] **Step 3: Commit fixes if needed**

```powershell
git add .agents/skills/run-revit-mcp .agents/workflows/start.md
git commit -m "fix: stabilize revit harness skill integration"
```



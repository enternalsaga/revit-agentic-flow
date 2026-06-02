---
description: Run Revit Harness operations — check, registry, evals, classify, gap detection. Use when diagnosing MCP issues or running harness tools.
---
// turbo-all

# Revit Harness Workflow

This workflow dispatches harness commands through the `.\harness` CLI.

## Determine the operation

Based on the user's request or arguments after `/harness`, determine which operation to run:

| User says | Action |
|---|---|
| `/harness` or `/harness check` | Go to Step 1: Check |
| `/harness registry` | Go to Step 2: Registry |
| `/harness evals` | Go to Step 3: Evals (offline) |
| `/harness evals --live` | Go to Step 3: Evals (with live) |
| `/harness classify <file>` | Go to Step 4: Classify |
| `/harness gap <trace>` | Go to Step 5: Gap pipeline |
| `/harness full` | Go to Step 6: Full diagnostic |

## Step 1: Check

Run the bootstrap check:

```powershell
.\harness check
```

Parse the JSON output and report:
- Transport status (Named Pipe / JSON-RPC)
- Command counts per layer
- Any drift or stale session issues
- Guidance for the current session

## Step 2: Registry

Run the command coverage audit:

```powershell
.\harness registry
```

Summarize:
- Total commands per layer
- Coverage gaps (which commands are missing wrappers)
- Recommendations for fixing gaps

## Step 3: Evals

Run the appropriate eval suite:

```powershell
# For offline only:
.\harness evals

# For full suite including live:
.\harness evals --live
```

Report pass/fail counts and any failing assertions.

## Step 4: Classify

Run the failure classifier:

```powershell
.\harness classify <error-file>
```

Explain:
- The error category
- Confidence level
- Suggested fix or next action

## Step 5: Gap Pipeline

Run detect then propose:

```powershell
.\harness gap detect <trace-file>
```

If gaps are found, ask the user if they want to generate a proposal:

```powershell
.\harness gap propose <gap-file>
```

Present the proposal summary for review. Do NOT scaffold without explicit user approval.

## Step 6: Full Diagnostic

Run check → registry → evals in sequence:

```powershell
.\harness check
.\harness registry
.\harness evals
```

Compile a comprehensive report covering:
- Connection status
- Command coverage
- Eval pass/fail
- Any issues found and recommended actions

**Format the final report as a clear summary with ✅/⚠️/❌ indicators.**

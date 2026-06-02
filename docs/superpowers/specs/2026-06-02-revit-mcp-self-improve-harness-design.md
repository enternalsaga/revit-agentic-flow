# Revit MCP Self-Improve Harness Design

## Goal

Build a technical self-improvement harness for the MCP_Revit project so agents can diagnose tool/runtime drift, record Revit command failures, classify repeated issues, and propose targeted improvements to tooling and skills.

This first phase optimizes project/tooling reliability, not architectural model quality.

## Current Problems

Repeated Revit MCP sessions show the same failure classes:

- Agents cannot reliably tell which commands are available in the live Revit runtime.
- `list_available_commands` can exist in source but be unavailable in a stale running session.
- Agents work around discovery gaps by creating ad hoc scripts.
- JSON-RPC calls through PowerShell are error-prone because quoting mistakes corrupt JSON.
- Runtime failures are captured as conversation memory or manual notes, not structured traces.
- `run-revit-mcp/SKILL.md` carries too much operational detail and grows as every failure is patched into prompt text.

## Design Principles

- Prefer deterministic harness tools over longer prompt instructions.
- Capture evidence first, then improve. No self-modification without a trace.
- Treat command availability as runtime state, not source-code assumption.
- Keep skill files lean; move reusable mechanics into scripts and references.
- Propose lessons automatically, but require approval before changing skills or command code.
- Make every improvement measurable with a small eval or replay.

## Components

### 1. `revit_harness_bootstrap`

Purpose: establish the actual runtime state before any modeling or command development task.

Responsibilities:

- Check Phase 1 named pipe connectivity.
- Check legacy JSON-RPC bridge connectivity.
- Read local `command.json`.
- Inspect TypeScript tool files.
- Inspect C# MCP tool wrappers.
- Ask live Revit runtime which commands are registered when possible.
- Detect stale sessions and wrapper/command drift.

Output shape:

```json
{
  "success": true,
  "transports": {
    "phase1Pipe": "available",
    "legacyJsonRpc": "available"
  },
  "commands": {
    "localManifestCount": 68,
    "typescriptToolCount": 68,
    "csharpWrapperCount": 68,
    "runtimeRegisteredCount": 67
  },
  "drift": {
    "manifestWithoutWrapper": [],
    "wrapperWithoutManifest": [],
    "runtimeMissing": ["list_available_commands"]
  },
  "guidance": [
    "Use JSON-RPC fallback for commands registered in runtime.",
    "Restart MCP client when wrappers are missing from client tool discovery."
  ]
}
```

### 2. `command_registry_report`

Purpose: provide a deeper command/tool audit for development work.

Responsibilities:

- Compare manifest, TypeScript tools, C# MCP wrappers, and commandset implementations.
- Report schema availability.
- Identify source-level drift separately from runtime drift.
- Produce machine-readable output for evals and agent decisions.

This should replace agent-authored one-off "list command" scripts.

### 3. `invoke_revit_command`

Purpose: be the single safe command invocation path for scripts and agents.

Responsibilities:

- Accept command name and params as a JSON file or object.
- Avoid shell quoting issues by writing params to a temp file when needed.
- Normalize timeout handling.
- Capture request, response, transport, duration, and errors.
- Return a consistent envelope for both named pipe and JSON-RPC transports.

Output envelope:

```json
{
  "success": true,
  "command": "create_level",
  "transport": "legacyJsonRpc",
  "durationMs": 812,
  "requestId": "20260602-abc123",
  "result": {}
}
```

### 4. `trace_writer`

Purpose: persist evidence from every meaningful harness run.

Trace path:

```text
.revit-harness/runs/{YYYYMMDD_HHMMSS}_{slug}/trace.json
```

Trace includes:

- User intent or task label.
- Agent plan summary.
- Bootstrap result.
- Every command call with params hash, result, duration, and error.
- Retry/fallback decisions.
- Snapshot paths and model statistics.
- Final status.
- Lesson candidates.

### 5. `failure_classifier`

Purpose: convert raw failures into stable categories.

Initial taxonomy:

- `stale_session`
- `schema_mismatch`
- `json_quoting`
- `missing_family`
- `invalid_geometry`
- `view_missing`
- `command_not_registered`
- `transport_unavailable`
- `command_bug`
- `skill_gap`
- `verification_gap`

Each classification should include confidence, evidence, and suggested next action.

### 6. `lesson_candidate_generator`

Purpose: propose improvements from repeated trace patterns.

Rules:

- One isolated failure creates a trace, not a lesson.
- Two repeated failures in the same category create a lesson candidate.
- Three repeated failures can propose a concrete patch target.
- Generated candidates are written to a review queue, not applied automatically.

Review queue path:

```text
.revit-harness/lesson-candidates/{YYYYMMDD_HHMMSS}_{category}.json
```

## Skill Integration

`run-revit-mcp/SKILL.md` should be refactored after the harness exists.

First change only:

- At the start of every Revit modeling task, run `revit_harness_bootstrap`.
- Use `invoke_revit_command` for fallback bridge calls.
- Save a trace when a task has any command failure, retry, fallback, or snapshot verification issue.

Later change:

- Move long tool tables and fallback payload examples into `references/`.
- Keep the main skill focused on Diagnose -> Plan -> Execute -> Verify.
- Add links to harness scripts instead of embedding long PowerShell examples.

## Eval Strategy

Start with technical evals that validate the harness itself:

1. Bootstrap detects live Revit and reports command drift.
2. Registry report catches an intentionally missing wrapper.
3. `invoke_revit_command` handles params without shell quoting errors.
4. Failure classifier maps known sample errors to expected categories.
5. Trace writer produces valid JSON matching schema.

After those pass, add live Revit evals:

1. Create levels and grids.
2. Create a small wall/floor model.
3. Create or switch to a 3D view.
4. Run snapshot and model statistics.
5. Verify no warnings or expected warning classification.

## Phased Implementation

### Phase 1: Foundation

- Create `.revit-harness/` runtime output structure.
- Add bootstrap script.
- Add registry report script.
- Add invocation wrapper.
- Add trace schema and writer.

### Phase 2: Classification

- Add failure taxonomy.
- Add sample error fixtures.
- Add classifier tests.
- Add lesson candidate generator.

### Phase 3: Skill Hook-In

- Update `run-revit-mcp/SKILL.md` to call the harness.
- Move large operational reference content into `references/`.
- Add a small final report format that includes trace path and classified failures.

### Phase 4: Evals

- Add technical harness evals.
- Add live Revit smoke evals.
- Use eval results as the approval gate for future self-improvement changes.

## Non-Goals

- Do not build full autonomous self-patching in the first version.
- Do not optimize building design quality yet.
- Do not replace MCP server architecture.
- Do not add model/provider-specific behavior.
- Do not auto-commit generated lessons or patches.

## Open Decisions

- Implementation language for harness scripts: PowerShell, Node, or C# CLI.
- Whether trace files should be gitignored by default.
- Whether live Revit evals should run manually only or become CI-like smoke tests.

## Recommended First Milestone

Build Phase 1 with PowerShell or Node scripts first, because the immediate failures are transport, quoting, discovery, and trace capture. Keep the first milestone independent from Revit command implementation changes so it can improve debugging before deeper refactors.

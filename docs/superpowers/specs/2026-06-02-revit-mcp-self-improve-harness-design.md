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
- `command_gap`
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

## Full Phase Specification

### Phase 1: Foundation - Runtime Discovery, Invocation, and Trace Capture

Purpose: create a deterministic tooling layer that agents can call before and during any Revit MCP task.

#### Deliverables

- `src/RevitHarness/`
  - `bootstrap.ps1` or `bootstrap.mjs`
  - `registry-report.ps1` or `registry-report.mjs`
  - `invoke-command.ps1` or `invoke-command.mjs`
  - `trace-writer.ps1` or `trace-writer.mjs`
  - `schemas/trace.schema.json`
  - `schemas/bootstrap.schema.json`
  - `fixtures/sample-bootstrap.json`
- Runtime output directory:
  - `.revit-harness/runs/`
  - `.revit-harness/cache/`
  - `.revit-harness/lesson-candidates/`
- `.gitignore` update for runtime trace output if not already ignored.

#### Bootstrap Contract

`revit_harness_bootstrap` must report both source-level and live-runtime state.

Required checks:

- Phase 1 named pipe availability.
- Legacy JSON-RPC bridge availability.
- Current Revit project info if a transport is available.
- Local `mcp-servers-for-revit/command.json` command count.
- TypeScript tool files under `mcp-servers-for-revit/server/src/tools`.
- C# MCP wrapper methods under `src/RevitMcpServer/Tools`.
- Runtime command list when a working command exists.
- Drift between manifest, wrappers, tools, and runtime.

Exit behavior:

- Exit code `0`: at least one transport available and report generated.
- Exit code `1`: no transport available, but local source audit still generated.
- Exit code `2`: local source audit failed due to missing expected project files.

#### Registry Report Contract

`command_registry_report` must return command coverage by layer:

- `manifestCommands`
- `typescriptTools`
- `csharpMcpTools`
- `commandsetImplementations`
- `runtimeRegisteredCommands`
- `coverageGaps`

Coverage gaps must distinguish:

- `manifest_without_typescript_tool`
- `manifest_without_csharp_wrapper`
- `manifest_without_commandset_implementation`
- `typescript_tool_without_manifest`
- `csharp_wrapper_without_manifest`
- `runtime_missing_manifest_command`
- `runtime_extra_command`

#### Invocation Contract

`invoke_revit_command` must support:

- Command name.
- Params as inline JSON.
- Params from a JSON file.
- Transport preference: `auto`, `phase1`, `jsonrpc`.
- Timeout in seconds.
- Optional trace run ID.

It must normalize output into:

```json
{
  "success": true,
  "command": "get_project_info",
  "transport": "jsonrpc",
  "durationMs": 123,
  "requestId": "20260602_120000_abc123",
  "paramsHash": "sha256:...",
  "result": {},
  "error": null
}
```

Failure output must include:

```json
{
  "success": false,
  "command": "create_sloped_roof",
  "transport": "jsonrpc",
  "durationMs": 123,
  "requestId": "20260602_120000_abc123",
  "paramsHash": "sha256:...",
  "result": null,
  "error": {
    "categoryHint": "command_error",
    "message": "The roof is not created by pick walls.",
    "raw": {}
  }
}
```

#### Trace Contract

Every trace must include:

- `traceVersion`
- `runId`
- `timestampUtc`
- `workspaceRoot`
- `taskLabel`
- `userIntent`
- `agentPlan`
- `bootstrap`
- `commands`
- `snapshots`
- `modelStatistics`
- `warnings`
- `classifiedFailures`
- `lessonCandidates`
- `finalStatus`

The trace writer must never store secrets, API keys, or full environment dumps.

#### Acceptance Criteria

- Bootstrap works when Revit is connected.
- Bootstrap still produces local source audit when Revit is disconnected.
- Registry report catches a known missing or stale command path.
- Invocation wrapper can call `get_project_info` without shell quoting issues.
- Invocation wrapper can pass a nested JSON payload without corrupting it.
- Trace writer produces schema-valid JSON.
- No generated runtime traces are accidentally staged by default.

#### Tests

- Unit test registry parsing against local fixtures.
- Unit test params hashing and redaction.
- Unit test JSON-file params invocation formatting.
- Smoke test `get_project_info` when live Revit is available.

#### Risks

- Live runtime command discovery may not exist in stale sessions. Mitigation: report `runtimeRegisteredCommands` as unknown rather than guessing.
- PowerShell quoting can remain fragile. Mitigation: prefer params file input as the default path.

### Phase 2: Failure Classification and Lesson Candidate Generation

Purpose: convert raw traces into actionable, repeatable improvement signals.

#### Deliverables

- `src/RevitHarness/classify-failure.*`
- `src/RevitHarness/generate-lesson-candidates.*`
- `src/RevitHarness/schemas/failure.schema.json`
- `src/RevitHarness/schemas/lesson-candidate.schema.json`
- `src/RevitHarness/fixtures/errors/`
- `src/RevitHarness/fixtures/traces/`

#### Failure Taxonomy

Initial categories:

- `stale_session`: source contains a wrapper or command, but live client/runtime does not expose it.
- `schema_mismatch`: command exists, but params shape differs between tool schema and command handler.
- `json_quoting`: params fail before Revit due to shell or JSON parsing.
- `missing_family`: Revit cannot find a required family or type.
- `invalid_geometry`: Revit rejects geometry or creates zero elements because geometry is invalid.
- `view_missing`: requested view is missing and no fallback creates it.
- `command_not_registered`: runtime bridge reports method not found.
- `command_gap`: a reusable operation is missing and the task needed repeated dynamic C# or helper fallback. This category is introduced by Phase 5; Phase 2 implementations must treat it as a future enum extension until Phase 5 updates the classifier and schema.
- `transport_unavailable`: named pipe and JSON-RPC are unreachable.
- `command_bug`: command exists and params are valid, but implementation returns an internal bug.
- `skill_gap`: agent plan or skill instruction caused an avoidable wrong tool choice.
- `verification_gap`: task completed without required snapshot/statistics/element verification.

#### Classifier Contract

Input:

```json
{
  "trace": {},
  "error": {},
  "context": {
    "command": "create_sloped_roof",
    "transport": "jsonrpc"
  }
}
```

Output:

```json
{
  "category": "invalid_geometry",
  "confidence": 0.86,
  "evidence": [
    "Command returned success true but created 0 sloped roof(s).",
    "Warning contains: The roof is not created by pick walls."
  ],
  "suggestedNextAction": "Retry with footprint bounded by existing walls and no overhang before falling back.",
  "patchTargets": [
    ".agents/skills/run-revit-mcp/SKILL.md",
    "mcp-servers-for-revit/commandset/Services/CreateSlopedRoofEventHandler.cs"
  ]
}
```

#### Lesson Candidate Rules

Lesson candidates are generated only when a pattern repeats or when one failure is severe enough to block a core workflow.

Required candidate fields:

- `candidateId`
- `createdAtUtc`
- `category`
- `frequency`
- `supportingTracePaths`
- `summary`
- `generalizedLesson`
- `recommendedPatchTargets`
- `risk`
- `requiresHumanApproval`

Candidate example:

```json
{
  "category": "json_quoting",
  "frequency": 3,
  "generalizedLesson": "When invoking Revit JSON-RPC from PowerShell, pass params through a JSON file instead of inline shell-quoted JSON.",
  "recommendedPatchTargets": [
    "src/RevitHarness/invoke-command.ps1",
    ".agents/skills/run-revit-mcp/SKILL.md"
  ],
  "requiresHumanApproval": true
}
```

#### Acceptance Criteria

- Known fixture errors classify into expected categories.
- Classifier includes evidence, not just a label.
- Lesson generator does not create candidates for one-off low-confidence failures.
- Lesson generator writes reviewable JSON, not direct source edits.
- Candidate output can be consumed by a later planning or implementation workflow.

#### Tests

- Fixture tests for every taxonomy category.
- Regression test for stale session: source has command, runtime says method not found.
- Regression test for quoting: invalid JSON primitive from PowerShell.
- Regression test for command gap: repeated dynamic C# fallback for the same operation produces a command proposal, not an immediate patch.
- Regression test for verification gap: no snapshot after modeling command sequence.

#### Risks

- Over-classification can create noisy lessons. Mitigation: require confidence and repeated support.
- Agent may treat candidates as instructions. Mitigation: mark every candidate as requiring human approval.

### Phase 3: Skill Integration and Workflow Refactor

Purpose: connect the harness to agent behavior while keeping `run-revit-mcp` smaller and less brittle.

#### Deliverables

- Updated `.agents/skills/run-revit-mcp/SKILL.md`.
- New references under `.agents/skills/run-revit-mcp/references/`:
  - `tool-reference.md`
  - `fallbacks.md`
  - `failure-taxonomy.md`
  - `verification-checklist.md`
- New scripts pointer section in the skill.
- Optional `.agents/workflows/start.md` update to call harness bootstrap.

#### Main Skill Changes

`run-revit-mcp/SKILL.md` should keep only high-signal workflow rules:

- Diagnose.
- Plan.
- Bootstrap.
- Execute through dedicated tools or `invoke_revit_command`.
- Verify.
- Write trace and report.

Long tables and examples should move into references. The main skill should load only relevant references:

- Load `tool-reference.md` when mapping tasks to tools.
- Load `fallbacks.md` only when a dedicated tool fails or runtime is stale.
- Load `failure-taxonomy.md` only when classifying an error.
- Load `verification-checklist.md` before final report.

#### Required Agent Behavior

At the start of every modeling task:

1. Run bootstrap.
2. If bootstrap reports stale MCP client discovery, use runtime/JSON-RPC fallback for available Revit commands.
3. If bootstrap reports no transport, stop and report the connection issue.
4. Use command wrapper for all JSON-RPC fallback calls.
5. Save trace when any command fails, retries, or uses fallback.

At the end of every modeling task:

1. Run snapshot when possible.
2. Run model statistics when possible.
3. Run warnings check when possible.
4. Include trace path in report if a trace exists.
5. Include classified failures and lesson candidates if generated.

#### Acceptance Criteria

- The skill no longer contains large duplicated fallback payload tables in the main body.
- The skill explicitly requires bootstrap before Revit execution.
- The skill sends fallback calls through `invoke_revit_command`.
- The final report includes trace path and failure classification when applicable.
- Existing modeling behavior remains compatible with direct MCP tools.

#### Tests

- Trigger test: Vietnamese modeling request should still activate `run-revit-mcp`.
- Dry-run test: skill plan includes bootstrap and verification.
- Failure-flow test: command failure produces trace and classification.
- Reference loading review: main skill stays concise and references are scoped.

#### Risks

- Moving content into references may hide useful instructions. Mitigation: keep clear "when to load" pointers in the main skill.
- Too much harness ceremony may slow simple tasks. Mitigation: bootstrap should be fast and cache local source audit when possible.

### Phase 4: Eval Harness and Improvement Gate

Purpose: make future MCP_Revit self-improvement measurable before changing skills or commands.

#### Deliverables

- `src/RevitHarness/evals/`
  - `eval-bootstrap.*`
  - `eval-registry-report.*`
  - `eval-invoke-command.*`
  - `eval-failure-classifier.*`
  - `eval-trace-writer.*`
  - `live/eval-create-levels-grids.*`
  - `live/eval-create-basic-building.*`
  - `live/eval-create-or-switch-3d-view.*`
  - `live/eval-snapshot-statistics.*`
- `src/RevitHarness/evals/fixtures/`
- `src/RevitHarness/evals/results/`
- `src/RevitHarness/evals/run-evals.*`

#### Eval Types

Technical evals run without Revit:

- Registry parsing and drift detection.
- JSON params handling.
- Trace schema validation.
- Failure fixture classification.
- Lesson candidate threshold behavior.

Live Revit evals run only when Revit is open:

- Create levels and grids.
- Create basic walls/floors.
- Create or switch to a 3D view.
- Snapshot workspace.
- Analyze model statistics.
- Check warnings.

#### Eval Result Contract

```json
{
  "evalRunId": "20260602_120000",
  "startedAtUtc": "2026-06-02T12:00:00Z",
  "environment": {
    "revitConnected": true,
    "transport": "jsonrpc"
  },
  "results": [
    {
      "name": "eval-bootstrap",
      "status": "passed",
      "durationMs": 511,
      "assertions": [
        {
          "name": "reportsAtLeastOneTransport",
          "passed": true
        }
      ]
    }
  ],
  "summary": {
    "passed": 1,
    "failed": 0,
    "skipped": 0
  }
}
```

#### Improvement Gate

Before approving any future self-improvement patch:

1. Run technical evals.
2. Run relevant live Revit evals if the patch touches command execution.
3. Compare results to previous eval run.
4. Reject patches that improve prompt behavior but regress runtime correctness.
5. Require trace evidence for any proposed lesson-based change.

#### Acceptance Criteria

- Technical evals can run without Revit.
- Live evals skip cleanly when Revit is unavailable.
- Eval output is JSON and human-readable summary.
- Eval failure points to trace and command output.
- Improvement patches cite eval results.

#### Tests

- Run all technical evals locally.
- Verify live eval skip behavior with Revit unavailable.
- Verify live eval pass behavior with Revit available and plugin loaded.
- Verify failed assertion creates a trace link.

#### Risks

- Live Revit evals may mutate user models. Mitigation: require a disposable test model or create a temporary project where possible.
- Eval runtime may be slow. Mitigation: separate technical evals from live smoke evals.

### Phase 5: Command Gap Resolver and Controlled Command Creation

Purpose: let agents move from "missing command detected" to a reviewable command proposal, and optionally to a scaffolded command implementation, without silently patching the project during a modeling task.

This phase adds the missing capability that Phase 1-4 intentionally defer: creating new Revit MCP commands when repeated traces prove that a reusable operation is missing.

#### Operating Modes

The command gap resolver must support three explicit modes:

- `observe`: record command gaps in traces only.
- `propose`: generate a command proposal and required eval plan. This is the default.
- `implement`: scaffold and edit command code only after explicit user approval of a reviewed proposal.

Agents must not enter `implement` mode from a normal modeling request. They can only implement a command when the user asks to create the command or when a reviewed proposal is approved.

#### Deliverables

- Modify `src/RevitHarness/classify-failure.ps1` and `src/RevitHarness/schemas/failure.schema.json` to include `command_gap` only when Phase 5 classification is implemented.
- `src/RevitHarness/detect-command-gap.ps1`
- `src/RevitHarness/generate-command-proposal.*`
- `src/RevitHarness/scaffold-command.*`
- `src/RevitHarness/validate-command-contract.*`
- `src/RevitHarness/evals/eval-command-gap-detection.*`
- `src/RevitHarness/evals/eval-command-proposal.*`
- `src/RevitHarness/evals/eval-command-scaffold.*`
- `src/RevitHarness/schemas/command-gap.schema.json`
- `src/RevitHarness/schemas/command-proposal.schema.json`
- `src/RevitHarness/schemas/command-contract.schema.json`
- Runtime review queues:
  - `.revit-harness/command-gaps/`
  - `.revit-harness/command-proposals/`
  - `.revit-harness/command-scaffolds/`

#### Command Gap Detection

`detect-command-gap` reads traces and registry reports, then identifies operations where a dedicated command is missing.

Detection signals:

- A task uses `send_code_to_revit` or compiled helper DLLs for the same operation more than once.
- A runtime command returns `Method not found`, and source audit confirms no command implementation exists.
- A skill or trace marks a fallback reason as "no dedicated tool".
- A verification trace shows successful dynamic C# behavior that could become a reusable command.
- A command exists in one layer but not all required layers, such as C# commandset without MCP wrapper.

Output shape:

```json
{
  "gapId": "20260602_switch_or_create_3d_view",
  "operation": "switch_or_create_3d_view",
  "status": "missing_command",
  "confidence": 0.91,
  "evidence": [
    "switch_view failed because the requested 3D view did not exist.",
    "A helper C# file created NP_3D_Townhouse successfully.",
    "No command named create_3d_view or switch_or_create_3d_view exists in command.json."
  ],
  "fallbackUsed": "compiled_helper_csharp",
  "recommendedMode": "propose"
}
```

#### Command Proposal Contract

`generate-command-proposal` converts one gap into a concrete implementation brief.

Required proposal fields:

- `proposalId`
- `createdAtUtc`
- `sourceGapId`
- `commandName`
- `purpose`
- `whyDedicatedCommandIsNeeded`
- `inputSchema`
- `outputSchema`
- `implementationTargets`
- `manifestEntry`
- `typescriptWrapperPlan`
- `csharpMcpWrapperPlan`
- `commandsetImplementationPlan`
- `evalPlan`
- `deploymentImpact`
- `risks`
- `approvalRequired`

Example command proposal:

```json
{
  "commandName": "switch_or_create_3d_view",
  "purpose": "Activate an existing 3D view by name or create an isometric 3D view when missing.",
  "inputSchema": {
    "viewName": "NP_3D_Townhouse",
    "createIfMissing": true,
    "detailLevel": "Fine",
    "activate": true
  },
  "outputSchema": {
    "success": true,
    "viewId": 12345,
    "viewName": "NP_3D_Townhouse",
    "created": true,
    "activated": true
  },
  "implementationTargets": [
    "mcp-servers-for-revit/commandset/Commands/SwitchOrCreate3DViewCommand.cs",
    "mcp-servers-for-revit/commandset/Services/SwitchOrCreate3DViewEventHandler.cs",
    "mcp-servers-for-revit/server/src/tools/switch_or_create_3d_view.ts",
    "src/RevitMcpServer/Tools/AccessTools.cs",
    "mcp-servers-for-revit/command.json"
  ],
  "approvalRequired": true
}
```

#### Command Scaffold Contract

`scaffold-command` creates only the files and manifest entries needed for an approved proposal.

It must support dry run and apply modes:

```powershell
.\src\RevitHarness\scaffold-command.ps1 `
  -ProposalPath .\.revit-harness\command-proposals\20260602_switch_or_create_3d_view.json `
  -Mode DryRun
```

Dry run output must list exact files to create or modify without writing source changes.

Apply mode may create or modify:

- TypeScript MCP wrapper under `mcp-servers-for-revit/server/src/tools/`.
- C# Phase 1 wrapper under `src/RevitMcpServer/Tools/`.
- C# commandset command under `mcp-servers-for-revit/commandset/Commands/`.
- C# commandset service handler under `mcp-servers-for-revit/commandset/Services/`.
- Command manifest entry in `mcp-servers-for-revit/command.json`.
- Eval fixture or smoke eval under `src/RevitHarness/evals/`.

The scaffold must not deploy add-ins, restart Revit, or commit changes automatically.
Scaffold apply must write only inside the allowlisted target paths above, and proposal mode must never write source files.

#### Command Contract Validation

`validate-command-contract` checks source consistency before build/deploy.

Validation checks:

- `commandName` matches across proposal, manifest, wrapper, and C# command implementation.
- The TypeScript schema and C# wrapper parameter names match the proposal input schema.
- The commandset class inherits the correct command base class.
- The commandset command constructs the expected service handler and both files follow local namespace and subfolder conventions.
- `command.json` contains exactly one entry for the new command.
- The new command appears in registry report after source changes.
- A targeted eval exists for the command.

Failure output must include file paths and the mismatched names.

#### Eval Requirements

Every new command must ship with at least one targeted eval:

- Source-level eval: registry and schema contract.
- Build eval: `dotnet build src/RevitMcpServer.sln -c Release` and the matching commandset build config must pass after scaffold apply.
- Runtime smoke eval: optional when Revit is unavailable, required before declaring the command production-ready.

For `switch_or_create_3d_view`, the live eval should:

1. Call the command with a unique view name.
2. Assert the command returns `created: true`.
3. Call the command again with the same name.
4. Assert the command returns `created: false`.
5. Run `snapshot_workspace` or `get_views` to verify the view exists.

#### Agent Workflow

When a missing command is detected during execution:

1. Continue the active modeling task using the safest approved fallback if possible.
2. Record the fallback in the trace.
3. Classify the issue as `command_gap` when evidence supports it.
4. In `observe` mode, stop there.
5. In `propose` mode, write a command proposal to `.revit-harness/command-proposals/`.
6. In `implement` mode, require explicit approval, run scaffold dry run, then apply source changes.
7. Run command contract validation.
8. Run technical evals.
9. If Revit is available, run the targeted live smoke eval.
10. Report changed files, eval results, and remaining deployment/restart steps.

#### Acceptance Criteria

- Repeated helper C# usage can produce a `command_gap` classification.
- A command proposal includes schema, target files, eval plan, and approval requirement.
- Scaffold dry run reports exact file changes without touching source.
- Scaffold apply creates a command skeleton matching local project patterns.
- Contract validation catches mismatched command names across layers.
- Build validation catches compile errors in MCP server and commandset source after scaffold apply.
- New command work is blocked unless mode is `implement` and approval is explicit.
- The harness never deploys, restarts Revit, or commits without explicit user instruction.

#### Tests

- Fixture test: two traces using the same helper C# operation produce one command gap.
- Fixture test: one isolated helper C# operation does not produce a command gap proposal by default.
- Proposal test: `switch_or_create_3d_view` proposal includes all required fields.
- Scaffold dry-run test: reports expected create/modify paths and writes no files.
- Source safety test: proposal mode writes only review queue files, and scaffold apply writes only allowlisted source/eval paths.
- Contract validation test: intentionally mismatched command names fail validation.
- Eval presence test: scaffolded command requires at least one targeted eval file.
- Build test: scaffolded command compiles in the MCP server and the relevant commandset build config.
- Runtime output test: `.revit-harness/command-gaps/`, `.revit-harness/command-proposals/`, and `.revit-harness/command-scaffolds/` are ignored by git.
- Secret redaction test: command gap and proposal files do not contain API keys, passwords, tokens, full environment dumps, or full generated helper source.

#### Risks

- Agents may overfit one-off modeling needs into too many commands. Mitigation: require repeated evidence or explicit user request.
- Scaffolded command code may compile but fail in live Revit. Mitigation: targeted live smoke eval before production-ready status.
- Command creation can interrupt a modeling session. Mitigation: default mode is `propose`; implementation is a separate workflow.
- Source-level registry can pass while runtime remains stale. Mitigation: report source-ready and runtime-ready as separate statuses.

### Phase 6: CLI Wrapper and Workflow Orchestrator

Purpose: eliminate long PowerShell invocation paths by providing a single entry-point CLI and an agent-invokable workflow, so both humans and agents interact with the harness through short, memorable subcommands.

#### Problem Statement

Before the Phase 6 CLI wrapper, harness scripts existed but were hard to invoke directly:

- Users must type `powershell -NoProfile -ExecutionPolicy Bypass -File .\src\RevitHarness\bootstrap.ps1 -WriteCache` to run a simple health check.
- Agents must construct these paths from memory or documentation, increasing error rate.
- No workflow exists for agents to orchestrate multi-step harness operations (e.g., "check everything", "run evals then classify failures").
- The `/start` workflow calls bootstrap but ignores registry, evals, and gap detection.

Phase 6 resolves this by routing users and agents through `.\harness <subcommand>` while keeping source scripts under `src/RevitHarness/`.

#### Deliverables

- `harness.bat` at project root — batch wrapper dispatching to PowerShell scripts via subcommands.
- `.agents/workflows/harness.md` — agent workflow for `/harness` invocation.
- Updated `README.md` — Section 4 uses short `.\harness <command>` syntax.
- Updated `.agents/workflows/start.md` — harness bootstrap uses `.\harness check`.

#### CLI Contract (`harness.bat`)

The CLI must support these subcommands:

| Subcommand | Maps to | Description |
|---|---|---|
| `check` | `bootstrap.ps1 -WriteCache` | Check transport & command state |
| `registry` | `registry-report.ps1` | Full command coverage audit |
| `invoke <name> [params]` | `invoke-command.ps1` | Call a Revit command safely |
| `classify <error-file>` | `classify-failure.ps1` | Classify an error |
| `trace new <label>` | `trace-writer.ps1 -Mode new` | Start a new trace |
| `trace append <runId> <file>` | `trace-writer.ps1 -Mode append-command` | Append command result |
| `trace finalize <runId> <status>` | `trace-writer.ps1 -Mode finalize` | Finalize trace |
| `evals` | `evals/run-evals.ps1` | Run offline evals |
| `evals --live` | `evals/run-evals.ps1 -IncludeLive` | Run all evals including live |
| `gap detect <trace>` | `detect-command-gap.ps1` | Detect command gaps |
| `gap propose <gap-file>` | `generate-command-proposal.ps1` | Generate proposal |
| `gap validate <proposal>` | `validate-command-proposal.ps1` | Validate proposal |
| `gap scaffold <proposal> [--dry-run]` | `scaffold-command.ps1` | Scaffold command |
| `help` | — | Show available subcommands |

Invocation examples:

```batch
.\harness check
.\harness registry
.\harness invoke get_project_info
.\harness invoke create_level --params-file .\params.json --timeout 60
.\harness classify .\error.json
.\harness evals
.\harness evals --live
.\harness gap detect .revit-harness\runs\<run_id>\trace.json
.\harness gap propose .revit-harness\command-gaps\<gap_id>.json
.\harness gap scaffold .revit-harness\command-proposals\<id>.json --dry-run
.\harness help
```

Exit codes:

- `0`: success.
- `1`: script-level failure (transport unavailable, classification error).
- `2`: invalid subcommand or missing arguments.

The wrapper must:

- Set `-NoProfile -ExecutionPolicy Bypass` automatically.
- Forward all stdout/stderr from the underlying PowerShell script.
- Not require any PATH configuration beyond having PowerShell available.
- Work from the project root directory.

#### Workflow Contract (`.agents/workflows/harness.md`)

The workflow enables agents to invoke harness operations via `/harness` or when they detect a harness-related need.

Supported modes:

| Agent says | Workflow does |
|---|---|
| `/harness` or `/harness check` | Run `.\harness check`, report results |
| `/harness registry` | Run `.\harness registry`, summarize gaps |
| `/harness evals` | Run `.\harness evals`, report pass/fail |
| `/harness evals --live` | Run `.\harness evals --live`, report results |
| `/harness classify <file>` | Run classifier, explain category and suggested fix |
| `/harness gap <trace>` | Run detect → propose pipeline, present proposal for review |
| `/harness full` | Run check → registry → evals sequence, comprehensive report |

The workflow must:

- Use `// turbo-all` annotation so all commands auto-run.
- Parse JSON output and present human-readable summaries.
- Not require the user to type PowerShell paths.
- Include error handling: if `.\harness` is not found, guide user to check they are in the project root.

#### README Update Contract

README Section 4 must use only short CLI commands:

Before:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\RevitHarness\bootstrap.ps1 -WriteCache
```

After:
```batch
.\harness check
```

Every code block in Section 4 must use `.\harness <subcommand>` syntax. No raw PowerShell invocation paths should remain in README Section 4.

#### `/start` Workflow Update

The `/start` workflow should invoke the harness through:
```batch
.\harness check
```

This replaces the old direct script form:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\RevitHarness\bootstrap.ps1 -WriteCache
```

#### Acceptance Criteria

- `.\harness help` lists all subcommands with descriptions.
- `.\harness check` produces the same output as direct `bootstrap.ps1 -WriteCache` invocation.
- `.\harness invoke get_project_info` returns valid JSON envelope.
- `.\harness evals` runs technical evals and returns summary.
- `.\harness gap detect <trace>` correctly detects gaps.
- All existing plan eval assertions pass when invoked through `.\harness` instead of direct script paths.
- `/harness` workflow in agent context runs successfully.
- README contains zero raw `powershell -NoProfile -ExecutionPolicy Bypass -File` paths.
- `/start` workflow uses `.\harness check` instead of direct path.

#### Tests

- Subcommand routing test: each subcommand dispatches to correct script with correct arguments.
- Unknown subcommand test: `.\harness foobar` returns exit code 2 and help text.
- No-args test: `.\harness` without subcommand shows help.
- Params forwarding test: `.\harness invoke create_level --params-file .\params.json --timeout 60` correctly maps to `invoke-command.ps1 -CommandName create_level -ParamsPath .\params.json -TimeoutSeconds 60`.
- Exit code forwarding test: `.\harness check` returns same exit code as underlying `bootstrap.ps1`.
- Workflow integration test: `/harness check` in agent context produces expected output format.

#### Risks

- Windows batch file quoting can be fragile with complex paths. Mitigation: test with paths containing spaces (OneDrive paths).
- Adding another layer may hide errors. Mitigation: forward all stdout/stderr unchanged; only add the routing logic.

## Cross-Phase Architecture

### Recommended File Layout

```text
src/RevitHarness/
├── README.md
├── bootstrap.ps1
├── registry-report.ps1
├── invoke-command.ps1
├── trace-writer.ps1
├── classify-failure.ps1
├── generate-lesson-candidates.ps1
├── detect-command-gap.ps1
├── generate-command-proposal.ps1
├── scaffold-command.ps1
├── validate-command-contract.ps1
├── schemas/
│   ├── bootstrap.schema.json
│   ├── command-result.schema.json
│   ├── trace.schema.json
│   ├── failure.schema.json
│   ├── lesson-candidate.schema.json
│   ├── command-gap.schema.json
│   ├── command-proposal.schema.json
│   └── command-contract.schema.json
├── fixtures/
│   ├── errors/
│   └── traces/
└── evals/
    ├── run-evals.ps1
    ├── eval-bootstrap.ps1
    ├── eval-registry-report.ps1
    ├── eval-invoke-command.ps1
    ├── eval-failure-classifier.ps1
    ├── eval-trace-writer.ps1
    ├── eval-command-gap-detection.ps1
    ├── eval-command-proposal.ps1
    ├── eval-command-scaffold.ps1
    └── live/
        ├── eval-create-levels-grids.ps1
        ├── eval-create-basic-building.ps1
        ├── eval-create-or-switch-3d-view.ps1
        └── eval-snapshot-statistics.ps1
```

Use PowerShell first because this project already uses PowerShell for Revit bridge workflows and the target environment is Windows. Node can be introduced later if schema validation or reporting becomes cleaner there.

### Runtime Output Layout

```text
.revit-harness/
├── runs/
│   └── 20260602_120000_create-townhouse/
│       ├── trace.json
│       ├── bootstrap.json
│       ├── commands/
│       │   ├── 001_get_project_info.json
│       │   └── 002_create_level.json
│       └── snapshots/
├── cache/
│   └── last-bootstrap.json
├── lesson-candidates/
│   └── 20260602_120500_json_quoting.json
├── command-gaps/
│   └── 20260602_121000_switch_or_create_3d_view.json
├── command-proposals/
│   └── 20260602_121100_switch_or_create_3d_view.json
└── command-scaffolds/
    └── switch_or_create_3d_view/
```

Runtime output should be ignored by git unless a specific fixture is intentionally copied into `src/RevitHarness/fixtures/`.

### Security and Data Hygiene

- Redact environment variables.
- Store params hash by default; store full params only when safe.
- Never store API keys, passwords, tokens, or local user secrets.
- Snapshot paths are allowed, but not binary snapshot files unless explicitly requested.
- Generated C# helper source can be referenced by path, not embedded wholesale in trace files.

### Definition of Done

The full harness is complete when:

- Agents can discover Revit MCP runtime state without ad hoc scripts.
- JSON-RPC fallback calls no longer require hand-written shell quoting.
- Every failed Revit command can be traced to a structured run record.
- Repeated failures generate reviewable lesson candidates.
- Missing reusable operations generate reviewable command gap reports.
- Approved command gaps can generate command proposals with schemas, target files, eval plans, and explicit approval gates.
- Approved command proposals can scaffold and validate source changes without deploy, restart, or commit side effects.
- `run-revit-mcp` uses harness bootstrap and wrappers.
- Technical evals run without Revit.
- Live evals run or skip cleanly depending on Revit availability.
- Future skill/tool changes can be evaluated against previous harness results.
- Users interact with the harness through `.\harness <subcommand>` — no raw PowerShell paths required.
- Agents can invoke `/harness` workflow to run any harness operation without constructing script paths.
- README documentation uses only CLI subcommands, not direct script invocations.

## Non-Goals

- Do not build full autonomous self-patching in the first version.
- Do not optimize building design quality yet.
- Do not replace MCP server architecture.
- Do not add model/provider-specific behavior.
- Do not auto-commit generated lessons, command proposals, or patches.
- Do not auto-implement new commands during ordinary modeling tasks; command implementation requires explicit `implement` mode and approval.

## Open Decisions

- ~~Implementation language for harness scripts: PowerShell, Node, or C# CLI.~~ **Resolved:** PowerShell for scripts, `harness.bat` wrapper for CLI entry point.
- Whether trace files should be gitignored by default.
- Whether live Revit evals should run manually only or become CI-like smoke tests.

## Recommended First Milestone

Build Phase 1 with PowerShell or Node scripts first, because the immediate failures are transport, quoting, discovery, and trace capture. Keep the first milestone independent from Revit command implementation changes so it can improve debugging before deeper refactors.


# Command Gap Workflow

Use this reference when a Revit MCP task reveals that a dedicated command is missing, not registered, or repeatedly replaced by custom `send_code_to_revit` helpers.

## Modes

- `observe`: record the gap in the trace only.
- `propose`: create a command gap and proposal for review. This is the default.
- `implement`: scaffold source files only after the user approves a proposal.

## Required Flow

1. Run `src/RevitHarness/detect-command-gap.ps1` against the trace that contains the failure.
2. Run `src/RevitHarness/generate-command-proposal.ps1` for each confirmed gap.
3. Report the proposal path to the user.
4. Do not scaffold or edit source unless the user approves the proposal.
5. After approval, run `src/RevitHarness/validate-command-proposal.ps1 -RequireApproved`.
6. Run `src/RevitHarness/scaffold-command.ps1 -DryRun`.
7. Review the dry-run scaffold.
8. Run `src/RevitHarness/scaffold-command.ps1 -Apply` only when source edits are explicitly requested.
9. Run registry report and command-specific evals after source changes.

## Default User-Facing Report

When a command gap is found, report:

- missing command name
- evidence from trace
- fallback used
- proposal path
- whether implementation is blocked on approval

## Hard Limits

- Do not build or deploy command code during an unrelated modeling task.
- Do not restart Revit or MCP clients without explicit user approval.
- Do not auto-commit generated commands.
- Do not mark a generated command complete until registry report and relevant evals pass.

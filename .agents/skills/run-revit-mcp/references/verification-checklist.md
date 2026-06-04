# Revit MCP Verification Checklist

Run this checklist before reporting a modeling task as complete.

## Required Checks

1. Verify expected element IDs still exist with `verify_elements` when IDs were captured.
2. Run `snapshot_workspace` with image and visible elements when a view is available. Always pass `outputDirectory: "<PROJECT_ROOT>/.tmp/snapshots"` — never save to `.claude/` or source folders.
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

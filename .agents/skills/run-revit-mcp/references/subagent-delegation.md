# Subagent Delegation for Revit Modeling

Use subagents only when the current AI client supports them and the user allows subagent/parallel work. They help most on large models with 10+ planned tasks, repeated coordinate sets, complex native-helper C# preparation, or expensive verification. They do **not** make Revit model writes parallel: the main agent remains the coordinator and drives every Revit MCP call sequentially.

Before execution, classify the plan into two lists:

```
## Parallel Prep Plan
- [ ] Coordinate table for columns/walls/openings — subagent, no Revit calls
- [ ] Native-helper C# draft for missing wrapper operation — subagent, no Revit calls
- [ ] Verification checklist and expected counts — subagent, no Revit calls

## Serialized Revit Queue
1. create_level ...
2. create_grid ...
3. create_structural_column ...
4. snapshot_workspace ...
```

## Delegate only sidecar work

- Coordinate calculation: compute XYZ/location/height arrays from already approved grid and level rules; return JSON ready for the coordinator to inspect.
- Native-helper C# drafting: write `send_code_to_revit` snippets for native Revit API operations where command wrappers are missing, such as wall profile edits, roof creation, hosted family placement, type duplication, or parameter updates. The coordinator reviews and executes.
- Snapshot analysis: after the coordinator exports an image, inspect it for missing elements, misalignment, intersections, or wrong elevations.
- Element count verification: compare `analyze_model_statistics` output against the plan checklist.
- Final report drafting: prepare the report from completed execution notes, without inventing success for unchecked items.

## Keep on the coordinator/main agent

- All Revit MCP calls, harness invocations that affect the live model, and any `send_code_to_revit` execution.
- Decisions that depend on previous tool call results, especially element IDs, created type names, host relationships, and error handling.
- Family/type discovery (`get_available_family_types`) and command availability checks; these results define valid downstream work.
- Final acceptance of subagent output before any Revit write.

## Dispatch rules (from superpowers:subagent-driven-development)

- Give each subagent a fresh, narrow prompt with only the relevant plan excerpt, units, constraints, and expected output schema. Do not make the subagent read the whole plan file.
- Prefer one subagent per independent sidecar task. Multiple sidecar subagents may run in parallel, but never dispatch multiple agents that write to the same files or call Revit.
- While subagents run, the coordinator should continue non-overlapping work on the critical path. Wait only when a Revit call depends on the subagent result.
- Review each returned artifact for spec compliance first, then quality/safety. If it has gaps, ask the same subagent or a focused fix subagent to correct it before use.
- If a subagent reports `NEEDS_CONTEXT` or `BLOCKED`, change the prompt/context/model or split the task; do not retry the same vague request.

## Example subagent prompts

```
Coordinate prep:
Given approved grids X=[0,15000,40000] and Y=[0,6000,...,60000],
plus end-frame intermediates at Y=0 and Y=60000 with X=[7500,20000,25000,30000,35000],
return ONLY JSON: [{ "name": "...", "x": 0, "y": 0, "baseLevel": "...", "topLevel": "..." }].
Do not call Revit tools.

Native helper draft:
Write C# for send_code_to_revit that edits selected native Wall elements so their top profiles follow a gable roof line.
Keep elements as Wall instances, use mm/304.8 for ft conversion, do not create DirectShape, and do not start a Transaction.
Return code plus assumptions only. Do not call Revit tools.
```

## When NOT to delegate

Simple models (< 10 elements), tightly coupled steps where each result changes the next instruction, or any task whose next action is a live Revit write.

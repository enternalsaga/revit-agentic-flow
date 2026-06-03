---
name: run-revit-mcp
description: "Execute complex Revit modeling tasks through MCP tools with a structured diagnose-plan-execute-verify workflow. Use this skill whenever the user asks to create, build, or model anything in Revit involving 2+ elements (walls, floors, columns, grids, levels, roofs, doors, windows, structural framing, etc.), or when they say /revit-model. Also trigger when the user provides a drawing, specification, or description of a building/structure to be modeled in Revit. Covers phrases like: 'tạo mô hình', 'dựng hình', 'build a house', 'create structure', 'model this building', 'vẽ nhà', 'tạo kết cấu', 'dựng khung', or any multi-element Revit creation request."
---

# Revit Modeling Workflow

You are executing a structured Revit modeling workflow. This process ensures accuracy, uses the right MCP tools, and catches errors before reporting to the user.

The workflow has 4 phases: **Diagnose → Plan → Execute → Verify & Report**. Follow them in order.

---

## CRITICAL: Tool Usage Policy

**Native Revit elements are the default output.** See `references/tool-reference.md` § "Native-First Output Contract" for the full priority chain. `DirectShape` is only allowed for an explicitly approved placeholder, temporary visualization, or geometry that Revit cannot represent with native elements after the native options have been tried and documented.

**`send_code_to_revit` is the LAST RESORT, not the default.** A past failure mode is the AI assuming dedicated tools don't exist and falling back to `send_code_to_revit` for everything. This defeats the purpose of the MCP server. When `send_code_to_revit` is necessary, use it to create or modify native Revit elements before considering `DirectShape`.

Before using `send_code_to_revit` for ANY operation, you MUST:
1. Load `.agents/skills/run-revit-mcp/references/tool-reference.md` and map the task to a dedicated tool first
2. If you think a tool doesn't exist, **call it anyway** — tool availability is determined by the MCP server at runtime, not by your assumptions
3. Classify each requested element as `native`, `native-with-helper`, or `DirectShape-placeholder`; final model output must be `native` or `native-with-helper` unless the user accepts the editability tradeoff
4. Only use `send_code_to_revit` after a dedicated tool genuinely does not exist or has failed with an error that proves it cannot handle the specific operation

If your execution plan has more than 1 `send_code_to_revit` call, stop and re-examine — you are almost certainly missing available tools.

### Why this matters
- Dedicated tools validate parameters and return structured errors
- `send_code_to_revit` requires correct C# syntax, manual unit conversion (mm→ft), manual transaction handling, and produces opaque errors
- The AI frequently writes buggy C# (compilation errors, wrong API calls) when tools would work on the first try
- DirectShape creates mostly static geometry; users cannot edit it like normal Revit walls, roofs, doors, framing, or family instances

---

## Phase 1: Diagnose & Decompose

Read the user's request carefully and break it into atomic modeling tasks.

1. **List every element** the user wants created. Be specific — include dimensions, positions, types, and relationships. If the user provided an image or drawing, extract all measurable details (grid spacing, level heights, member sizes, slope angles, door/window sizes).

2. **Identify missing information.** If critical dimensions, family types, or positions are ambiguous, ask the user before proceeding. Do not guess structural member sizes or door/window dimensions. Query available family/types before creating placeholders; missing families are a reason to load/create a native family/type, not a reason to default to DirectShape.

3. **Determine creation order.** Revit elements have dependencies:
   - Grids and Levels come first (they define the coordinate system)
   - Structural columns need levels and grid intersections
   - Walls need levels (base/top constraints)
   - Floors and roofs need walls or boundary lines
   - Doors and windows need host walls
   - Structural framing needs columns or support points
   - Dimensions and tags come last

## Phase 2: Map to MCP Tools

For each task from Phase 1, assign the MCP tool to use.

### Harness Bootstrap

Before executing or planning fallback calls, run:

```batch
.\harness check
```

Use the bootstrap report as runtime truth:

- If direct MCP tools are visible, prefer them.
- If direct MCP discovery is stale but JSON-RPC is available, use `.\harness invoke <command>`.
- If no transport is available, stop and report that Revit/plugin is not connected.
- If a command fails, save or update a trace, classify the failure through `.\harness classify <error-file>`, and append trace data through `.\harness trace`.

**If `harness` CLI is not installed or not found:** Skip the bootstrap step and proceed directly to Step 1 (tool discovery via `list_available_commands`). The harness improves diagnostics but is not required for the core workflow. Log: `"(Harness: unavailable — skipping bootstrap, using direct MCP discovery)"`.

### Reference Loading

Load references only when needed:

- `.agents/skills/run-revit-mcp/references/tool-reference.md` when mapping tasks to tools.
- `.agents/skills/run-revit-mcp/references/fallbacks.md` when a dedicated tool is unavailable or fails.
- `.agents/skills/run-revit-mcp/references/failure-taxonomy.md` when classifying an error.
- `.agents/skills/run-revit-mcp/references/verification-checklist.md` before final reporting.
- `.agents/skills/run-revit-mcp/references/subagent-delegation.md` when delegating sidecar work to subagents.

### MANDATORY: Load tool schemas first

**Step 0 (before anything else):** MCP tools are *deferred* — their schemas are not loaded until you fetch them. Before calling any `mcp__mcp-server-for-revit__*` tool, run `ToolSearch` to load its schema:

```
ToolSearch({ query: "select:mcp__mcp-server-for-revit__create_level,mcp__mcp-server-for-revit__create_grid,..." })
```

If `ToolSearch` returns "No matching deferred tools found" for a tool name, that tool does **not exist** in the current session. Load `.agents/skills/run-revit-mcp/references/tool-reference.md` and `.agents/skills/run-revit-mcp/references/fallbacks.md` before choosing a workaround. Do NOT plan around tools that failed `ToolSearch`.

### MANDATORY: Discover available tools first

**Step 1:** Call `list_available_commands` (no parameters needed). This returns:
- All registered MCP tools in the current server session
- All Revit-side commands from `command.json`
- Coverage gaps (tools without commands, commands without tools)

Use this list as the ground truth for which tools you can call. Do NOT rely on your memory or assumptions about tool availability.

**Step 2:** Call `get_available_family_types` for each category you plan to create (e.g., `OST_Walls`, `OST_StructuralColumns`, `OST_Roofs`, `OST_Doors`) to confirm which family types are loaded.

**If `list_available_commands` is not available:** The MCP server may need a restart to pick up this new tool. Inform the user: "Tool `list_available_commands` chưa có trong session này. Bạn cần restart MCP client để nhận tool mới." Then fall back to attempting dedicated tools directly — try calling them and handle errors gracefully rather than preemptively using `send_code_to_revit`.

**If Revit MCP tools are not exposed in the current agent session but Revit is running:** use the Phase 1 C# harness and Named Pipe diagnostics first:
1. Run `.\harness check`.
2. Run `.\src\RevitHarness\registry-report.ps1`.
3. Confirm the Phase 1 add-in is deployed under `%APPDATA%\Autodesk\Revit\Addins\<version>\revit-mcp-v2`.

Still preserve the same modeling policy: discover commands, prefer dedicated commands, and document any use of `send_code_to_revit`.

### Tool Reference

Use `.agents/skills/run-revit-mcp/references/tool-reference.md` when mapping tasks to dedicated tools. Keep the detailed creation, modification, query, and verification tool tables there so this skill remains an orchestration workflow.


#### Tools Missing from ToolSearch (Stale Session Issue)

**All 68+ tools have C# wrappers and commands.** If `ToolSearch` returns fewer tools than expected, the MCP server was started from an older build. This is the #1 cause of "tool not found" errors (see LL-010).

**Diagnosis:** Compare `ToolSearch` count vs expected 68+. If missing, the session is stale.

**Resolution priority:**

1. **JSON-RPC bridge (same session):** For JSON-RPC fallback and compiled helper flow, use `references/fallbacks.md`. Do not hand-write shell-quoted JSON when `.\harness invoke` is available.
2. **Restart MCP client:** Tell the user to restart Claude Code to pick up the latest build. All tools will then appear in ToolSearch.
3. **`send_code_to_revit` workaround (last resort):** Only when both MCP tool and JSON-RPC bridge fail.

### Known Pitfalls

1. **Stale session = missing tools (LL-010).** The MCP server starts once per Claude Code session. If tools were added/rebuilt after session start, `ToolSearch` won't find them. **Always try the JSON-RPC bridge first** before concluding a tool doesn't exist. If many tools are missing, recommend the user restart Claude Code.

2. **`create_surface_based_element` with `OST_Roofs` creates FLAT roofs.** Revit footprint roofs ignore Z coordinates in boundary loops. Use `create_sloped_roof` first. If it cannot model the roof, use native roof API/edit profiles or a compiled native-helper command. Only use DirectShape for a roof after documenting why native roofs cannot meet the requirement and confirming the user accepts non-native editability.

3. **Cannot switch active view inside `send_code_to_revit`.** The code runs within a transaction; `RequestViewChange` and setting `ActiveView` fail. Use the dedicated `switch_view` tool instead — it runs outside a transaction. For exporting images without switching views, use `ImageExportOptions.SetViewsAndSheets()` inside `send_code_to_revit`.

4. **`send_code_to_revit` already wraps code in a transaction.** Do NOT create a new `Transaction` — it will throw `InvalidOperationException`. Write code as if you're already inside `tx.Start()`/`tx.Commit()`.

### When `send_code_to_revit` IS appropriate

Only for operations that genuinely have no tool equivalent, such as:
- Joining/unjoing geometry between elements
- Complex family manipulation (duplicating types, setting type parameters)
- Attaching walls to roofs (Attach Top/Base)
- Creating or modifying native Revit elements where the MCP command wrapper is missing
- Operations combining multiple Revit API calls in a single transaction

When you must use it, document the reason: `"(Tool: send_code_to_revit — Reason: no MCP tool for joining wall geometry)"`

If the code creates DirectShape, document it separately as an approved placeholder: `"(DirectShape placeholder — Reason: native roof/family path failed because ...; Editability: static geometry, not editable as native BIM element)"`

### Large native helper operations through `send_code_to_revit`

For compiled helper DLL flow and JSON-RPC invocation patterns, use `references/fallbacks.md`. The reference covers the helper script with `-SourcePath`, `-TypeName`, and `-MethodName` parameters, and how to avoid nested transactions.

### Present the plan

Output a numbered checklist to the user. Each item MUST show the specific MCP tool name:

```
## Kế hoạch thực thi

1. [ ] Tạo levels (Tool: `create_level`) — 6 levels: L1_Base 0mm ... L6 11500mm
2. [ ] Tạo lưới trục Y (Tool: `create_grid`) — Y1-Y11, spacing 6000mm
3. [ ] Tạo lưới trục X (Tool: `create_custom_grid`) — X1-X8 @ 0, 7500, 15000, 20000...
4. [ ] Tạo cột (Tool: `create_structural_column`) — 43 cột tại giao lưới
5. [ ] Tạo tường gạch (Tool: `create_line_based_element`) — 4 tường, 0-2000mm
6. [ ] Tạo tường tôn (Tool: `create_line_based_element`) — 4 tường, 2000-8000mm
7. [ ] Tạo mái chính (Tool: `create_sloped_roof`) — pitched roof 10%
8. [ ] Tạo cửa (Tool: `create_parametric_door`) — 4 cửa cuốn 3000×3500mm
9. [ ] Join tường (Tool: `send_code_to_revit` — Reason: no MCP tool for JoinGeometry)
```

If your plan has `send_code_to_revit` for items like grids, columns, walls, roofs, or doors — **your plan is wrong.** Go back and use the dedicated tools.

Wait for user confirmation before executing. If the user says to proceed or the task is straightforward, move to Phase 3.

## Phase 3: Execute

Execute each task using the mapped MCP tools.

### Subagent Delegation (use only for large, independent work)

For models with 10+ planned tasks, use subagents for sidecar work (coordinate calculation, C# drafting, snapshot analysis). The main agent remains the coordinator for all Revit MCP calls.

Load `references/subagent-delegation.md` for the full delegation protocol, dispatch rules, and example prompts.

### Execution rules

- **Use the tool from the plan.** Do not silently switch to `send_code_to_revit` during execution because "it seems easier." If the planned tool fails, fix the parameters and retry with the same tool first.
- **One tool call at a time.** Do not batch unrelated operations. This makes errors easier to isolate.
- **All coordinates in millimeters (mm).** This is the unit convention for all MCP tools. The tools handle mm→ft conversion internally — you do NOT need to convert.
- **Batch limit: ~24 items per call** for bulk operations (levels, grids). The MCP server has a 2-minute timeout.
- **Track results.** After each tool call, note whether it succeeded or failed, and capture any element IDs returned.

### Error handling

When a tool call fails:
1. Read the error message carefully.
2. Check if it's a known issue:
   - **Family type not found** → call `get_available_family_types` to find alternatives
   - **Level not found** → verify level names/IDs from previous creation steps
   - **Invalid geometry** → check coordinates, ensure start ≠ end point, verify winding order for surfaces
   - **Native family/type missing** → query existing types, duplicate the closest native type, load/create a family, or ask the user for the correct family path before using placeholders
   - **DirectShape/TessellatedShapeBuilder failed** → this should only occur for approved placeholders; look for degenerate faces, repeated points, non-planar face loops, wrong winding, or invalid polygon offsets; split triangles from quads when needed
   - **Timeout** → reduce batch size and retry
   - **Tool not found** → the MCP server may need restart. Inform the user.
   - **`Invalid JSON` from socket bridge** → payload is likely too large for the Revit plugin socket buffer; switch to a compiled helper DLL plus short bootstrap snippet
   - **`Exception has been thrown by the target of an invocation`** → wrap reflection calls and return `InnerException.Message`, `InnerException.GetType().FullName`, and stack trace before changing geometry logic
3. Fix the parameters and retry the failed operation with the SAME tool.
4. Only fall back to `send_code_to_revit` if the dedicated tool has a confirmed bug that prevents the specific operation; keep the fallback native-first and do not convert the element to DirectShape unless explicitly approved.
5. If the error persists after 2 retries, log it and continue with remaining tasks.

### Command gaps

If a missing command, repeated dynamic C# workaround, or `Method not found` blocks the task, load `references/command-gap-workflow.md`. Default to `propose`: detect the gap from the trace and generate a reviewable command proposal. Do not scaffold, build, deploy, restart Revit, or commit command code unless the user explicitly approves implementation.

## Phase 4: Verify & Report

After all execution steps complete:

### 4a. Visual verification (MANDATORY)

Call `snapshot_workspace` to capture the current state of the model:

```
snapshot_workspace({
  includeImage: true,
  includeVisibleElements: true,
  pixelSize: 1920
})
```

Review the returned image. Check for:
- Missing elements (compare against the plan)
- Misaligned geometry (grids not orthogonal, columns not at intersections)
- Incorrect dimensions or elevations
- Elements that look wrong (walls not connected, floors floating)

If the snapshot reveals issues:
1. Identify what's wrong
2. Fix using the appropriate MCP tool
3. Take another snapshot to confirm the fix
4. Repeat until the model matches the plan

### 4b. Element count verification

Call `analyze_model_statistics` to verify element counts match expectations. Compare against the plan checklist.

### 4c. Report to user

Always end with a structured report:

```markdown
## Báo cáo Thực thi Revit

### Đã hoàn thành
- [x] Task (Tool: `tool_name`) — ID: xxx
- [x] Task (Tool: `tool_name`) — ID: xxx

### Lỗi / Chưa làm được
- [ ] Task — Nguyên nhân: ...

### Kết quả kiểm tra
- Snapshot: [đã chụp và review]
- Số lượng phần tử: X elements created (expected: Y)
- Kết luận: [Thành công / Cần chỉnh sửa thêm]

### Đề xuất tiếp theo
- "Bạn có muốn chỉnh sửa phần này không?"
- "Cần load thêm Family XYZ để tạo..."
```

### Tool usage summary

At the end of the report, include a tool usage breakdown so the user can verify dedicated tools were used and DirectShape was not silently substituted for native BIM:

```
### Thống kê Tool sử dụng
- create_level: 1 call (6 levels)
- create_grid: 1 call (11 grids)
- create_custom_grid: 1 call (8 grids)
- create_structural_column: 2 calls (43 columns)
- create_line_based_element: 4 calls (8 walls)
- create_sloped_roof: 1 call (1 roof)
- create_parametric_door: 1 call (4 doors)
- send_code_to_revit: 1 call (join geometry — no dedicated tool)
- DirectShape placeholders: 0
- snapshot_workspace: 1 call (verification)
```

---

## Key reminders

- Units are always **millimeters (mm)** — elevations, distances, coordinates.
- Grid and level creation should happen first — everything else depends on them.
- Always verify family types exist before creating elements.
- Take a snapshot at the end — this is MANDATORY, not optional.
- If the user provides an image/drawing, extract ALL dimensions before starting. Ask if anything is unclear.
- Respond to the user in Vietnamese (per project rules).
- **Do not assume tools don't exist. Try them first.**
- If direct MCP tool namespaces are unavailable but Revit is running, use the Phase 1 C# harness and Named Pipe diagnostics first: run `.\harness check`, then `.\src\RevitHarness\registry-report.ps1`, and confirm the Phase 1 add-in is deployed.
- Use `.agents/skills/run-revit-mcp/scripts/Invoke-RevitMcpJsonRpc.ps1` for JSON-RPC calls and compile+run helper DLL workflows.
- Keep socket payloads small; use compiled helper DLLs for long C# modeling logic.
- When dynamically loading helper DLLs, rebuild to a versioned filename after each change because Revit locks loaded assemblies. The bundled helper script does this automatically.
- Do not generate DirectShape models by default. If DirectShape is approved, verify both category counts and a visual snapshot, then clearly report which elements are non-native and what editable native replacement is still needed.

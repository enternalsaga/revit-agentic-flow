# Revit MCP Failure Taxonomy

Use this reference when a command fails, retries, falls back, or verification is incomplete.

| Category | Evidence | First Action |
|----------|----------|--------------|
| `stale_session` | Source has tool/command but client or runtime cannot see it | Run bootstrap and use runtime fallback if available |
| `schema_mismatch` | Command exists but params shape fails | Compare tool schema, C# model, and command handler |
| `json_quoting` | `ConvertFrom-Json` or invalid JSON primitive | Use params file through `.\harness invoke` |
| `missing_family` | Revit cannot find family/type | Call `get_available_family_types` |
| `invalid_geometry` | Zero created elements or Revit geometry rejection | Retry with simpler valid geometry |
| `view_missing` | `View not found` | Discover views or create a 3D view helper |
| `command_not_registered` | JSON-RPC method not found | Check command manifest and deployed runtime |
| `transport_unavailable` | Named pipe and JSON-RPC unreachable | Ask user to open Revit/load plugin |
| `command_bug` | Valid command and params return internal exception | Create trace and propose command patch |
| `skill_gap` | Agent selected wrong tool or skipped required step | Propose skill update |
| `verification_gap` | No snapshot/statistics/warnings after modeling | Run verification before final report |

Classify through `.\harness classify <error-file>` when Phase 2 harness is available.

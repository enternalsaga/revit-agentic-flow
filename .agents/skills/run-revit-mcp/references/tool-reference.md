# Revit MCP Tool Reference

Use this reference when mapping a modeling task to dedicated Revit MCP commands.

## Native-First Output Contract

Default to editable native Revit elements. A model made mostly from DirectShape is only acceptable when the user explicitly asks for massing/concept geometry or approves static placeholders.

Before planning DirectShape for any building element, try this order:

1. Dedicated MCP command that creates a native Revit element.
2. Native Revit API helper through `send_code_to_revit` for operations missing from the command wrapper, such as type duplication, wall profile edits, openings, hosted family placement, or attach top/base.
3. Propose a new dedicated command if the workflow will repeat.
4. DirectShape placeholder only with documented reason and user-visible tradeoff.

## Creation

| Task | Preferred Tool |
|------|----------------|
| Levels | `create_level` |
| Uniform grids | `create_grid` |
| Custom grids | `create_custom_grid` |
| Single-zone walls and gable infill | `create_line_based_element` with `OST_Walls`; use `edit_wall_profile` or native-helper API for non-rectangular profiles |
| Basic wall type setup with layers, thickness, and materials | `create_or_update_basic_wall_type`; creates native editable Basic Wall types with `CompoundStructure` layers; do not use `SplitRegion` for this workflow |
| Vertical facade assemblies with height bands, such as brick + louver + metal cladding | Create/verify the member Basic Wall types with `create_or_update_basic_wall_type`, inspect available Stacked Wall templates with `inspect_stacked_wall_type`, then place with `create_stacked_wall`; do not model each band as separate wall strips unless no stacked wall type is available |
| Structural framing | `create_line_based_element` with `OST_StructuralFraming` |
| Floors | `create_surface_based_element` with `OST_Floors` |
| Flat roofs | `create_surface_based_element` with `OST_Roofs` |
| Sloped roofs and canopies | `create_sloped_roof`; use native-helper API for roof profiles/openings when needed |
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
| Stacked wall composition/member types | `inspect_stacked_wall_type` |
| Warnings | `get_warnings` |
| Verify elements | `verify_elements` |
| Filter elements | `ai_element_filter` |

`send_code_to_revit` is a last resort for creation, but it is acceptable as a native-helper when it still creates or modifies native Revit elements. Examples: geometry join, attach wall top/base, wall profile edits not covered by `edit_wall_profile`, creating openings, duplicating native types, or complex family type manipulation. Custom DirectShape geometry must be the final fallback and must be reported as non-editable placeholder geometry.

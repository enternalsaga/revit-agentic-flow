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

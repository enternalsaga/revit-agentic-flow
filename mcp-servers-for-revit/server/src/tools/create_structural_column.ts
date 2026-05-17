import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateStructuralColumnTool(server: McpServer) {
  server.tool(
    "create_structural_column",
    "Create one or more structural columns in Revit. Supports batch creation with base/top level control, offset, rotation, and family type selection. All units are in millimeters (mm).",
    {
      data: z
        .array(
          z.object({
            locationPoint: z
              .object({
                x: z.number().describe("X coordinate in mm"),
                y: z.number().describe("Y coordinate in mm"),
                z: z.number().describe("Z coordinate in mm"),
              })
              .describe("The position where the column will be placed"),
            typeId: z
              .number()
              .optional()
              .default(-1)
              .describe("ElementId of the column family type. -1 for default type."),
            baseLevelElevation: z
              .number()
              .default(0)
              .describe("Elevation of the base level in mm (used to find nearest level)"),
            baseOffset: z
              .number()
              .default(0)
              .describe("Offset from the base level in mm"),
            topLevelElevation: z
              .number()
              .optional()
              .describe("Elevation of the top level in mm. If not provided, uses the next level above base."),
            topOffset: z
              .number()
              .default(0)
              .describe("Offset from the top level in mm"),
            rotation: z
              .number()
              .default(0)
              .describe("Rotation angle in degrees (0-360) around the column axis"),
            width: z
              .number()
              .optional()
              .describe("Optional column width in mm (for parametric sizing)"),
            depth: z
              .number()
              .optional()
              .describe("Optional column depth in mm (for parametric sizing)"),
          })
        )
        .describe("Array of structural columns to create"),
    },
    async (args, extra) => {
      const params = args;
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "create_structural_column",
            params
          );
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Create structural column failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

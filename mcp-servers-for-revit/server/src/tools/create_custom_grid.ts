import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateCustomGridTool(server: McpServer) {
  server.tool(
    "create_custom_grid",
    "Create a grid system with custom (non-uniform) spacing per axis. Each axis accepts an array of spacing values and corresponding labels, enabling irregular grid layouts common in industrial buildings. All units are in millimeters (mm).",
    {
      xGrids: z
        .array(
          z.object({
            label: z.string().describe("Label for this grid line (e.g., 'A', '1')"),
            position: z.number().describe("Absolute position of this grid line along X-axis in mm"),
          })
        )
        .min(1)
        .describe("Array of X-axis grid definitions with label and absolute position in mm"),
      yGrids: z
        .array(
          z.object({
            label: z.string().describe("Label for this grid line (e.g., '1', 'A')"),
            position: z.number().describe("Absolute position of this grid line along Y-axis in mm"),
          })
        )
        .min(1)
        .describe("Array of Y-axis grid definitions with label and absolute position in mm"),
      xExtentMin: z
        .number()
        .default(0)
        .describe("Minimum extent along X-axis in mm (where Y-axis grids start)"),
      xExtentMax: z
        .number()
        .default(50000)
        .describe("Maximum extent along X-axis in mm (where Y-axis grids end)"),
      yExtentMin: z
        .number()
        .default(0)
        .describe("Minimum extent along Y-axis in mm (where X-axis grids start)"),
      yExtentMax: z
        .number()
        .default(50000)
        .describe("Maximum extent along Y-axis in mm (where X-axis grids end)"),
      elevation: z
        .number()
        .default(0)
        .describe("Elevation for grid lines in mm (Z-coordinate)"),
    },
    async (args, extra) => {
      const params = {
        xGrids: args.xGrids,
        yGrids: args.yGrids,
        xExtentMin: args.xExtentMin,
        xExtentMax: args.xExtentMax,
        yExtentMin: args.yExtentMin,
        yExtentMax: args.yExtentMax,
        elevation: args.elevation,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_custom_grid", params);
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
              text: `Create custom grid failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

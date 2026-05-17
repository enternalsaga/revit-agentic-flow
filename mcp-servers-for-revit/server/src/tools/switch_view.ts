import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSwitchViewTool(server: McpServer) {
  server.tool(
    "switch_view",
    "Switch the active view in Revit. Accepts a view name or ID. If neither is provided, switches to the default {3D} view. Useful for verifying model state from different angles.",
    {
      viewName: z
        .string()
        .optional()
        .describe(
          "Name of the target view (e.g., '{3D}', 'Level 1', 'Ground Floor'). Partial match supported."
        ),
      viewId: z
        .number()
        .optional()
        .describe("ElementId of the target view. Takes precedence over viewName."),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("switch_view", {
            viewName: args.viewName,
            viewId: args.viewId,
          });
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
              text: `Switch view failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

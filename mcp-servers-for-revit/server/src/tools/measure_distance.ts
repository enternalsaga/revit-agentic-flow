import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMeasureDistanceTool(server: McpServer) {
  server.tool(
    "measure_distance",
    "Measure 2D and 3D distance between two specific elements, or the first two selected elements if IDs are not provided.",
    {
      elementId1: z.number().optional().describe("First element ID (optional, will use selection if omitted)"),
      elementId2: z.number().optional().describe("Second element ID (optional, will use selection if omitted)")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("measure_distance", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Measure distance failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

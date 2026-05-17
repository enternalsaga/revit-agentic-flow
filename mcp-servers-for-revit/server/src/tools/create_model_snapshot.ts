import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateModelSnapshotTool(server: McpServer) {
  server.tool(
    "create_model_snapshot",
    "Create a hashed snapshot of elements in the model (geometry and parameters) to track changes over time.",
    {
      category: z.string().optional().describe("Optional Revit category to limit the snapshot (e.g., OST_Walls).")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_model_snapshot", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Create model snapshot failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

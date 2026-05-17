import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetSelectedSummaryTool(server: McpServer) {
  server.tool(
    "get_selected_summary",
    "Get a statistical summary of the currently selected elements, grouped by Category and Type.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_selected_summary", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get selected summary failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

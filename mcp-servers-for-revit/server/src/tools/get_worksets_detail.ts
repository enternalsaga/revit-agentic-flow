import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetWorksetsDetailTool(server: McpServer) {
  server.tool(
    "get_worksets_detail",
    "Get detailed workset statistics including element counts broken down by category for each workset. Only works with workshared projects.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_worksets_detail", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get worksets detail failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

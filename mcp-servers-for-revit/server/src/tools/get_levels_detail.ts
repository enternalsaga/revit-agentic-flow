import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetLevelsDetailTool(server: McpServer) {
  server.tool(
    "get_levels_detail",
    "Get detailed information about all levels in the current Revit project including elevations, building story status, and element counts per level.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_levels_detail", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get levels detail failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

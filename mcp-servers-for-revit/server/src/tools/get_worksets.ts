import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetWorksetsTool(server: McpServer) {
  server.tool(
    "get_worksets",
    "Get a list of all worksets in the current Revit project. Returns workset names, IDs, and whether they are open or closed. Only works with workshared projects.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_worksets", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get worksets failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

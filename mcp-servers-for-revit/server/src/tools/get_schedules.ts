import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetSchedulesTool(server: McpServer) {
  server.tool(
    "get_schedules",
    "Get a list of all schedules in the current Revit project with their names and types.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_schedules", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get schedules failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

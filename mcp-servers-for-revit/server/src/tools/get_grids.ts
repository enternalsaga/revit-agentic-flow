import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetGridsTool(server: McpServer) {
  server.tool(
    "get_grids",
    "Get a list of all grid lines in the current Revit project with their names and endpoint coordinates. Useful for understanding the structural grid system.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_grids", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get grids failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

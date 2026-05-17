import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerToggleRevitLinksTool(server: McpServer) {
  server.tool(
    "toggle_revit_links",
    "Toggle the visibility of all Revit Links in the current view.",
    {
      visible: z.boolean().describe("True to show links, False to hide them")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("toggle_revit_links", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Toggle Revit Links failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

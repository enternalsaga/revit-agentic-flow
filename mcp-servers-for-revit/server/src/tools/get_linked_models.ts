import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetLinkedModelsTool(server: McpServer) {
  server.tool(
    "get_linked_models",
    "Get information about all linked Revit models in the current project. Returns link names, file paths, and load status.",
    {},
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_linked_models", {});
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get linked models failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

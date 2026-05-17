import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerFindAndSelectTool(server: McpServer) {
  server.tool(
    "find_and_select",
    "Find elements by category or parameter filter and highlight them in the active Revit view.",
    {
      category: z.string().optional().describe("Revit Category to filter (e.g., OST_Walls)"),
      parameterName: z.string().optional().describe("Parameter name to search for"),
      parameterValue: z.string().optional().describe("Parameter value to match (contains)"),
      isolate: z.boolean().default(false).describe("If true, isolates the found elements in the view instead of just selecting them")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("find_and_select", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Find and select failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

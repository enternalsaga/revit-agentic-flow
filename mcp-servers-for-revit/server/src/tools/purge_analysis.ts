import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerPurgeAnalysisTool(server: McpServer) {
  server.tool(
    "purge_analysis",
    "Analyze the model to find unused elements like View Templates and Filters.",
    {
      analyzeOnly: z.boolean().default(true).describe("If true, only returns a list of unused elements. If false, actually deletes them.")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("purge_analysis", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Purge analysis failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

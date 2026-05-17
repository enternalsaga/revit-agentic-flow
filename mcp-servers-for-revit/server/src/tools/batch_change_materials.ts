import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerBatchChangeMaterialsTool(server: McpServer) {
  server.tool(
    "batch_change_materials",
    "Find and replace a specific material across elements in the project. It checks Instance parameters, Type parameters, and Compound Structures (walls, floors, etc).",
    {
      category: z.string().optional().describe("Optional Revit category to limit the search (e.g., OST_Walls)."),
      searchMaterialName: z.string().describe("The exact name of the material you want to replace."),
      replaceMaterialName: z.string().describe("The exact name of the material to use as replacement."),
      dryRun: z.boolean().default(false).describe("If true, performs a test run to count occurrences without making changes.")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("batch_change_materials", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Batch change materials failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

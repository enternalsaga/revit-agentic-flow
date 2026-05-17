import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCheckSprinklerCoverageTool(server: McpServer) {
  server.tool(
    "check_sprinkler_coverage",
    "Analyze sprinkler coverage on a specific level. Checks if sprinklers cover the space given a specific coverage radius.",
    {
      levelId: z.number().describe("The ID of the level to analyze"),
      coverageRadius: z.number().default(2000).describe("Coverage radius per sprinkler in mm (default 2000mm)")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("check_sprinkler_coverage", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Check sprinkler coverage failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

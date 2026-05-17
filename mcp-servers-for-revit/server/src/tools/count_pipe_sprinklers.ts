import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCountPipeSprinklersTool(server: McpServer) {
  server.tool(
    "count_pipe_sprinklers",
    "Count all sprinklers downstream of a given pipe and check if the pipe diameter satisfies standard NFPA sizing rules.",
    {
      pipeId: z.number().describe("The ID of the pipe element to analyze")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("count_pipe_sprinklers", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Count pipe sprinklers failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

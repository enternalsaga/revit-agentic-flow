import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerTraceMepConnectionsTool(server: McpServer) {
  server.tool(
    "trace_mep_connections",
    "Trace MEP network connections starting from a specific element. Returns connected elements up to a max depth.",
    {
      elementId: z.number().describe("The ID of the MEP element to start tracing from"),
      maxDepth: z.number().default(50).describe("Maximum depth of tracing")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("trace_mep_connections", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Trace MEP connections failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

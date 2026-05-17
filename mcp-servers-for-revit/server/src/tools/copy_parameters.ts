import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCopyParametersTool(server: McpServer) {
  server.tool(
    "copy_parameters",
    "Copy parameter values from a source element to multiple target elements.",
    {
      sourceElementId: z.number().describe("The ID of the element to copy values from"),
      targetElementIds: z.array(z.number()).describe("Array of element IDs to apply the values to"),
      parameterNames: z.array(z.string()).optional().describe("Optional list of specific parameters to copy. If empty, tries to copy all writable parameters"),
      overwrite: z.boolean().default(true).describe("Whether to overwrite existing values in targets")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("copy_parameters", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Copy parameters failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

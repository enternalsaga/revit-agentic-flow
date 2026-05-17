import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetParameterBulkTool(server: McpServer) {
  server.tool(
    "set_parameter_bulk",
    "Set a parameter value for multiple elements simultaneously. Returns a list of successful and failed element updates.",
    {
      elementIds: z.array(z.number()).describe("Array of element IDs to update"),
      parameterName: z.string().describe("The name of the parameter to set"),
      value: z.string().describe("The value to set (will be converted to the parameter's underlying type)")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_parameter_bulk", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Set parameter bulk failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

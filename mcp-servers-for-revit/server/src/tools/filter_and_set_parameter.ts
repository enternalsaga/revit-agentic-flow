import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerFilterAndSetParameterTool(server: McpServer) {
  server.tool(
    "filter_and_set_parameter",
    "Find elements using a filter parameter/value and set a different parameter to a new value for all matching elements. All-in-one query + write.",
    {
      category: z.string().optional().describe("Optional category to filter by (e.g. OST_Doors)"),
      filterParameter: z.string().describe("Parameter name to filter elements by"),
      filterValue: z.string().describe("Value to search for in the filter parameter (exact match or contains)"),
      setParameter: z.string().describe("Parameter name to update"),
      setValue: z.string().describe("New value to assign"),
      dryRun: z.boolean().default(false).describe("If true, only returns matched elements without changing values")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("filter_and_set_parameter", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Filter and set parameter failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementsSummaryTool(server: McpServer) {
  server.tool(
    "get_elements_summary",
    "Get a statistical summary of elements grouped by type or parameter value. Returns counts and optionally aggregated numeric values (sum, min, max, average) for a given parameter. Useful for generating reports like 'total pipe length by type'.",
    {
      category: z
        .string()
        .describe("The Revit category name (e.g., 'Pipes', 'Walls', 'Doors')"),
      parameterName: z
        .string()
        .optional()
        .describe("Parameter name to group by or aggregate. If not specified, groups by element type name."),
      aggregation: z
        .enum(["count", "sum", "average", "min", "max"])
        .optional()
        .default("count")
        .describe("Aggregation method for numeric parameters. Defaults to 'count'."),
    },
    async (args, extra) => {
      const params = {
        category: args.category,
        parameterName: args.parameterName,
        aggregation: args.aggregation ?? "count",
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_elements_summary", params);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Get elements summary failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

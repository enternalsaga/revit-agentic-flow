import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementsParameterValuesTool(server: McpServer) {
  server.tool(
    "get_elements_parameter_values",
    "Extract a specific parameter value from multiple elements filtered by category. Returns a list of element IDs paired with their parameter values. Useful for batch reading parameter data across many elements.",
    {
      category: z
        .string()
        .describe("The Revit category name to filter elements (e.g., 'Pipes', 'Walls', 'Doors')"),
      parameterName: z
        .string()
        .describe("The name of the parameter to extract values from"),
      limit: z
        .number()
        .optional()
        .default(500)
        .describe("Maximum number of elements to return. Defaults to 500."),
    },
    async (args, extra) => {
      const params = {
        category: args.category,
        parameterName: args.parameterName,
        limit: args.limit ?? 500,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_elements_parameter_values", params);
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
              text: `Get elements parameter values failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

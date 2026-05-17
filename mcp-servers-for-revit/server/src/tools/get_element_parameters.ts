import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementParametersTool(server: McpServer) {
  server.tool(
    "get_element_parameters",
    "Get all parameters of a specific Revit element by its ElementId. Returns parameter names, values, types, and whether they are read-only. Useful for inspecting element properties before modifying them.",
    {
      elementId: z
        .number()
        .describe("The ElementId of the element to get parameters from"),
      includeReadOnly: z
        .boolean()
        .optional()
        .default(true)
        .describe("Whether to include read-only parameters. Defaults to true."),
    },
    async (args, extra) => {
      const params = {
        elementId: args.elementId,
        includeReadOnly: args.includeReadOnly ?? true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_parameters", params);
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
              text: `Get element parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

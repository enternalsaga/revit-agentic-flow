import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetElementParameterTool(server: McpServer) {
  server.tool(
    "set_element_parameter",
    "Set a parameter value on a specific Revit element. Can modify instance parameters like Comments, Mark, or any writable parameter. Returns success/failure status.",
    {
      elementId: z
        .number()
        .describe("The ElementId of the element to modify"),
      parameterName: z
        .string()
        .describe("The name of the parameter to set"),
      value: z
        .union([z.string(), z.number(), z.boolean()])
        .describe("The value to set. Can be string, number, or boolean depending on parameter type."),
    },
    async (args, extra) => {
      const params = {
        elementId: args.elementId,
        parameterName: args.parameterName,
        value: args.value,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_parameter", params);
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
              text: `Set element parameter failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

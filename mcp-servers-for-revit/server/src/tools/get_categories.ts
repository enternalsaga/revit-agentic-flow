import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetCategoriesTool(server: McpServer) {
  server.tool(
    "get_categories",
    "Get a list of all element categories in the current Revit project with element counts. Returns category names and the number of instances in each category. Useful for understanding the model composition before querying specific elements.",
    {
      includeEmpty: z
        .boolean()
        .optional()
        .default(false)
        .describe("Whether to include categories with zero elements. Defaults to false."),
    },
    async (args, extra) => {
      const params = {
        includeEmpty: args.includeEmpty ?? false,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_categories", params);
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
              text: `Get categories failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

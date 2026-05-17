import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetViewsTool(server: McpServer) {
  server.tool(
    "get_views",
    "Get a list of all views and sheets in the current Revit project. Returns view names, types, and associated levels. Useful for navigating the project and understanding its structure.",
    {
      includeTemplates: z
        .boolean()
        .optional()
        .default(false)
        .describe("Whether to include view templates. Defaults to false."),
    },
    async (args, extra) => {
      const params = {
        includeTemplates: args.includeTemplates ?? false,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_views", params);
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
              text: `Get views failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

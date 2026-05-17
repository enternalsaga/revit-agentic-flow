import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetWarningsTool(server: McpServer) {
  server.tool(
    "get_warnings",
    "Get all warnings in the current Revit project. Returns warning descriptions, severity levels, and related element IDs. Useful for model health checks and quality assurance.",
    {
      limit: z
        .number()
        .optional()
        .default(200)
        .describe("Maximum number of warnings to return. Defaults to 200."),
    },
    async (args, extra) => {
      const params = {
        limit: args.limit ?? 200,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_warnings", params);
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
              text: `Get warnings failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

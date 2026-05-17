import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateParametricDoorTool(server: McpServer) {
  server.tool(
    "create_parametric_door",
    "Create a door with specific width and height dimensions. This will find a default door family, duplicate it to match the requested dimensions, and place it on the specified host wall.",
    {
      width: z.number().describe("Width of the door in feet"),
      height: z.number().describe("Height of the door in feet"),
      hostWallId: z.number().describe("The ElementId of the host wall where the door will be placed"),
      locationParameter: z.number().optional().default(0.5).describe("Normalized location on the wall (0.0 to 1.0, default 0.5 for center)")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "create_parametric_door",
            args
          );
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
              text: `Create parametric door failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

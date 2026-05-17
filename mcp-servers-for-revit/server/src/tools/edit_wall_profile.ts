import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// Validation schema for 3D points
const Point3DSchema = z.object({
  x: z.number().describe("X coordinate in mm"),
  y: z.number().describe("Y coordinate in mm"),
  z: z.number().describe("Z coordinate in mm"),
});

export function registerEditWallProfileTool(server: McpServer) {
  server.tool(
    "edit_wall_profile",
    "Edit the profile sketch of an existing wall by providing a new closed loop of lines",
    {
      wallId: z.number().describe("The ElementId of the wall to edit"),
      profilePoints: z
        .array(Point3DSchema)
        .describe("Array of 3D points defining the new closed outer loop of the wall profile"),
    },
    async ({ wallId, profilePoints }) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "edit_wall_profile",
            {
              wallId,
              profilePoints,
            }
          );
        });

        return {
          content: [
            {
              type: "text",
              text: typeof response === "string" ? response : JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Failed to edit wall profile: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
          isError: true,
        };
      }
    }
  );
}

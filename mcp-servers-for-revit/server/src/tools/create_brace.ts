import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateBraceTool(server: McpServer) {
  server.tool(
    "create_brace",
    "Create one or more structural braces in Revit. Braces are diagonal structural members that provide lateral stability to frames. They connect two points at different elevations, forming X-bracing, V-bracing, or single diagonal patterns. All units are in millimeters (mm).",
    {
      data: z
        .array(
          z.object({
            startPoint: z
              .object({
                x: z.number().describe("X coordinate of start point"),
                y: z.number().describe("Y coordinate of start point"),
                z: z.number().describe("Z coordinate of start point"),
              })
              .describe("Start point of the brace (mm)"),
            endPoint: z
              .object({
                x: z.number().describe("X coordinate of end point"),
                y: z.number().describe("Y coordinate of end point"),
                z: z.number().describe("Z coordinate of end point"),
              })
              .describe("End point of the brace (mm)"),
            baseLevelElevation: z
              .number()
              .describe("Elevation of the reference level in mm"),
            typeId: z
              .number()
              .optional()
              .default(-1)
              .describe(
                "ElementId of the structural framing family type. -1 to use the first available type."
              ),
          })
        )
        .describe("Array of structural braces to create"),
    },
    async (args, extra) => {
      const params = args;

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_brace", params);
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
              text: `Create brace failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

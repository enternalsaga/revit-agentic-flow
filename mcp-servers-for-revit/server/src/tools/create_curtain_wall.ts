import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateCurtainWallTool(server: McpServer) {
  server.tool(
    "create_curtain_wall",
    "Create one or more curtain walls in Revit. Curtain walls are non-bearing walls made of panels (glass, metal, etc.) divided by a grid of mullions. Useful for glass facades, canopies, and storefronts. All units are in millimeters (mm).",
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
              .describe("Start point of the curtain wall baseline"),
            endPoint: z
              .object({
                x: z.number().describe("X coordinate of end point"),
                y: z.number().describe("Y coordinate of end point"),
                z: z.number().describe("Z coordinate of end point"),
              })
              .describe("End point of the curtain wall baseline"),
            height: z
              .number()
              .describe("Height of the curtain wall in mm"),
            baseLevel: z
              .number()
              .describe("Elevation of the base level in mm"),
            baseOffset: z
              .number()
              .optional()
              .default(0)
              .describe("Offset from the base level in mm"),
            typeId: z
              .number()
              .optional()
              .default(-1)
              .describe(
                "ElementId of the curtain wall type. -1 to use the first available curtain wall type."
              ),
          })
        )
        .describe("Array of curtain walls to create"),
    },
    async (args, extra) => {
      const params = args;

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_curtain_wall", params);
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
              text: `Create curtain wall failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

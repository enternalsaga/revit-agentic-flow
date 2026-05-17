import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateSlopedRoofTool(server: McpServer) {
  server.tool(
    "create_sloped_roof",
    "Create one or more roofs with slope control in Revit. Supports footprint-based roofs where each boundary edge can define slope independently. Useful for gable roofs, shed roofs, hip roofs, ridge vents, and canopies. All units are in millimeters (mm), slopes in degrees.",
    {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .optional()
              .describe("Description of the roof (e.g., 'Main Roof', 'Canopy', 'Ridge Vent')"),
            typeId: z
              .number()
              .optional()
              .default(-1)
              .describe("ElementId of the roof type. -1 for default type."),
            baseLevelElevation: z
              .number()
              .describe("Elevation of the base level in mm (used to find nearest level)"),
            baseOffset: z
              .number()
              .default(0)
              .describe("Offset from the base level in mm (e.g., eave height above level)"),
            boundary: z
              .array(
                z.object({
                  p0: z.object({
                    x: z.number().describe("X coordinate of start point in mm"),
                    y: z.number().describe("Y coordinate of start point in mm"),
                    z: z.number().describe("Z coordinate of start point in mm"),
                  }),
                  p1: z.object({
                    x: z.number().describe("X coordinate of end point in mm"),
                    y: z.number().describe("Y coordinate of end point in mm"),
                    z: z.number().describe("Z coordinate of end point in mm"),
                  }),
                  definesSlope: z
                    .boolean()
                    .default(false)
                    .describe("Whether this edge defines a slope (true) or is flat/gable end (false)"),
                  slopeAngle: z
                    .number()
                    .default(0)
                    .describe("Slope angle in degrees from horizontal (e.g., 10 for 10°). Only used when definesSlope is true."),
                })
              )
              .min(3)
              .describe("Array of boundary line segments with optional slope definition per edge"),
            overhang: z
              .number()
              .optional()
              .default(0)
              .describe("Roof overhang distance in mm beyond the boundary edges"),
          })
        )
        .describe("Array of sloped roofs to create"),
    },
    async (args, extra) => {
      const params = args;
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "create_sloped_roof",
            params
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
              text: `Create sloped roof failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

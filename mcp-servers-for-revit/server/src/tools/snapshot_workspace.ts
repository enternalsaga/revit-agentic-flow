import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSnapshotWorkspaceTool(server: McpServer) {
  server.tool(
    "snapshot_workspace",
    "Capture the active Revit workspace with a rendered view image path, active view metadata, UI viewport bounds, visible element summaries, and current selection.",
    {
      includeImage: z
        .boolean()
        .optional()
        .describe("Export the active Revit view to an image file. Defaults to true."),
      includeVisibleElements: z
        .boolean()
        .optional()
        .describe("Include visible element summaries from the active view. Defaults to true."),
      includeSelection: z
        .boolean()
        .optional()
        .describe("Include currently selected elements. Defaults to true."),
      elementLimit: z
        .number()
        .int()
        .min(0)
        .max(1000)
        .optional()
        .describe("Maximum number of visible elements to return. Defaults to 100."),
      pixelSize: z
        .number()
        .int()
        .min(256)
        .max(4096)
        .optional()
        .describe("Image export pixel size for the longest side. Defaults to 1600."),
      outputDirectory: z
        .string()
        .optional()
        .describe("Optional directory for exported images. Defaults to the system temp RevitMCP folder."),
    },
    async (args) => {
      const params = {
        includeImage: args.includeImage ?? true,
        includeVisibleElements: args.includeVisibleElements ?? true,
        includeSelection: args.includeSelection ?? true,
        elementLimit: args.elementLimit ?? 100,
        pixelSize: args.pixelSize ?? 1600,
        outputDirectory: args.outputDirectory,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("snapshot_workspace", params);
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
              text: `snapshot workspace failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

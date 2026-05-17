import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerAssignParameterBetweenLevelsTool(server: McpServer) {
  server.tool(
    "assign_parameter_between_levels",
    "Find elements using a 3D bounding box between two levels and set a parameter value for them. Useful for assigning level data to objects like columns or stairs.",
    {
      lowerLevelId: z.number().describe("The Element ID of the lower level"),
      upperLevelId: z.number().describe("The Element ID of the upper level"),
      parameterName: z.string().describe("Parameter to set"),
      value: z.string().describe("Value to assign"),
      category: z.string().optional().describe("Optional category to limit the search (e.g., OST_Walls)"),
      dryRun: z.boolean().default(false).describe("If true, only returns matched elements without changing values")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("assign_parameter_between_levels", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Assign parameter failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

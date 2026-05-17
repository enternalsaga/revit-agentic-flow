import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetScheduleDataTool(server: McpServer) {
  server.tool(
    "get_schedule_data",
    "Get the data content of a specific schedule by name. Returns the schedule as a table with headers and rows. Useful for extracting tabular data from Revit schedules.",
    {
      scheduleName: z
        .string()
        .describe("The name of the schedule to extract data from"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_schedule_data", { scheduleName: args.scheduleName });
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Get schedule data failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

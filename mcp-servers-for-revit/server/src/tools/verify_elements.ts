import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerVerifyElementsTool(server: McpServer) {
  server.tool(
    "verify_elements",
    "Check if a list of element IDs still exist in the current Revit model. Useful for self-healing and verifying stale IDs.",
    {
      elementIds: z.array(z.number()).describe("Array of Revit element IDs to verify")
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("verify_elements", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Verify elements failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}

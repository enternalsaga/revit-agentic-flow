import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

interface CommandEntry {
  commandName: string;
  description: string;
  assemblyPath: string;
}

interface CommandJson {
  commands: CommandEntry[];
}

export function registerListAvailableCommandsTool(server: McpServer) {
  server.tool(
    "list_available_commands",
    "List all available MCP tools and Revit commands. Call this BEFORE planning any modeling task to discover which dedicated tools exist. Returns: (1) registered MCP tools from the server, (2) Revit-side commands from command.json. Use this to avoid falling back to send_code_to_revit when a dedicated tool exists.",
    {},
    async () => {
      try {
        const __filename = fileURLToPath(import.meta.url);
        const __dirname = path.dirname(__filename);

        const toolFiles = fs
          .readdirSync(__dirname)
          .filter(
            (file) =>
              (file.endsWith(".ts") || file.endsWith(".js")) &&
              !["index.ts", "index.js", "register.ts", "register.js"].includes(file)
          )
          .map((file) => file.replace(/\.(ts|js)$/, ""))
          .filter((name, index, self) => self.indexOf(name) === index)
          .sort();

        let revitCommands: CommandEntry[] = [];
        const commandJsonPath = path.resolve(__dirname, "../../../command.json");
        if (fs.existsSync(commandJsonPath)) {
          const raw = fs.readFileSync(commandJsonPath, "utf-8");
          const parsed: CommandJson = JSON.parse(raw);
          revitCommands = parsed.commands || [];
        }

        const mcpToolSet = new Set(toolFiles);
        const revitCommandNames = revitCommands.map((c) => c.commandName);
        const revitCommandSet = new Set(revitCommandNames);

        const toolsWithoutCommand = toolFiles.filter((t) => !revitCommandSet.has(t));
        const commandsWithoutTool = revitCommandNames.filter((c) => !mcpToolSet.has(c));

        const result = {
          mcpTools: toolFiles,
          mcpToolCount: toolFiles.length,
          revitCommands: revitCommands.map((c) => ({
            name: c.commandName,
            description: c.description,
          })),
          revitCommandCount: revitCommands.length,
          coverage: {
            toolsWithoutRevitCommand: toolsWithoutCommand,
            revitCommandsWithoutMcpTool: commandsWithoutTool,
          },
          guidance:
            "Use dedicated MCP tools (listed in mcpTools) instead of send_code_to_revit. " +
            "Only use send_code_to_revit for operations not covered by any tool above.",
        };

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Failed to list commands: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}

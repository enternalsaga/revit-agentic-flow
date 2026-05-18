# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP (Model Context Protocol) server for Autodesk Revit. It enables AI assistants to read, create, modify, and delete elements in Revit projects.

The repository now has a Phase 1 C# MCP server alongside the original TypeScript/WebSocket implementation. Prefer the C# Phase 1 path for Revit 2025 work unless a task explicitly targets the legacy server.

## Phase 1 C# Architecture

Primary execution chain:

```text
AI Client <--stdio--> RevitMcpServer (C# MCP) <--named pipe: revit-mcp--> RevitMcpPlugin (C# add-in) --> Command Set (C#) --> Revit API
```

- **`src/RevitMcpServer/`** - .NET 8 stdio MCP server. Tool wrappers live in `Tools/` and forward JSON-RPC requests through `Pipes/PipeClient`.
- **`src/RevitMcpSdk/`** - shared SDK contracts and JSON-RPC models used by the server and plugin.
- **`src/RevitMcpPlugin/`** - Revit 2025 add-in. It starts a named pipe service, dispatches requests through `CommandExecutor`, and loads command assemblies via `CommandManager`.
- **`mcp-servers-for-revit/commandset/`** - existing Revit command implementations. Phase 1 loads the legacy `RevitMCPSDK` commandset through a reflection adapter.
- **`mcp-servers-for-revit/command.json`** - command manifest used by the plugin configuration sync.

Named pipe protocol:
- Pipe name: `revit-mcp`
- Framing: 4-byte little-endian message length followed by a UTF-8 JSON-RPC payload
- Server timeout: 120 seconds per Revit command
- Pipe connect timeout: 5 seconds

## Phase 1 Build, Publish, Deploy

Build all C# projects:

```powershell
dotnet build src/RevitMcpServer.sln -c Release
```

Publish the self-contained MCP server:

```powershell
dotnet publish src/RevitMcpServer/RevitMcpServer.csproj -c Release -r win-x64 --self-contained
```

Build the Revit 2025 commandset:

```powershell
dotnet build "mcp-servers-for-revit/commandset/RevitMCPCommandSet.csproj" -c "Release R25"
```

Deploy Phase 1 plugin layout for Revit 2025:

```powershell
$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2025"
$target = Join-Path $addins "revit-mcp-v2"
$commandTarget = Join-Path $target "Commands\RevitMCPCommandSet\2025"
New-Item -ItemType Directory -Path $target -Force
New-Item -ItemType Directory -Path $commandTarget -Force
Copy-Item "src\RevitMcpPlugin\bin\Release\net8.0-windows\*" -Destination $target -Recurse -Force
Copy-Item "mcp-servers-for-revit\commandset\bin\Release R25\*" -Destination $commandTarget -Recurse -Force
Copy-Item "mcp-servers-for-revit\command.json" -Destination "$target\Commands\RevitMCPCommandSet\command.json" -Force
```

The `.addin` file must point to the deployed `RevitMcpPlugin.dll`. An absolute assembly path is the least ambiguous option when the add-in file lives directly under `%APPDATA%\Autodesk\Revit\Addins\2025`.

Claude Desktop MCP config example:

```json
{
  "mcpServers": {
    "Revit": {
      "type": "stdio",
      "command": "H:\\OneDrive\\Work\\AI\\Work\\MCP_Revit\\src\\RevitMcpServer\\bin\\Release\\net8.0-windows\\win-x64\\publish\\RevitMcpServer.exe"
    }
  }
}
```

## Legacy TypeScript Architecture

The original implementation remains under `mcp-servers-for-revit/`:

```text
AI Client <--stdio--> MCP Server (TypeScript) <--WebSocket:8080--> Revit Plugin (C#) --> Command Set (C#) --> Revit API
```

- **`mcp-servers-for-revit/server/`** - TypeScript MCP server. Each tool is a file in `server/src/tools/` exporting a `register*Tool(server)` function. Uses `withRevitConnection()` from `utils/ConnectionManager.ts`.
- **`mcp-servers-for-revit/plugin/`** - legacy C# Revit add-in that listens on WebSocket port 8080.
- **`mcp-servers-for-revit/commandset/`** - shared command implementations used by both legacy and Phase 1 paths.

Legacy server commands:

```bash
cd mcp-servers-for-revit/server
npm install
npm run build
npx tsx src/index.ts
```

## Tests

C# solution build:

```powershell
dotnet build src/RevitMcpServer.sln -c Release
```

Live Revit integration testing requires Revit 2025 open with the Phase 1 plugin loaded:

```powershell
npx @anthropic-ai/mcp-inspector src\RevitMcpServer\bin\Release\net8.0-windows\win-x64\publish\RevitMcpServer.exe
```

Use `say_hello` and `get_current_view_info` as smoke tests.

Existing commandset integration tests require Revit open:

```bash
dotnet test -c Debug.R26 -r win-x64 tests/commandset
dotnet test -c Debug.R25 -r win-x64 tests/commandset
```

## Adding a New MCP Tool

Phase 1:
1. Add a C# wrapper method in the appropriate `src/RevitMcpServer/Tools/*Tools.cs` file.
2. Forward to Revit through `PipeClient.SendCommandAsync(commandName, parameters)`.
3. Add or update the command implementation under `mcp-servers-for-revit/commandset/Commands` and `Services`.
4. Add the command entry to `mcp-servers-for-revit/command.json`.
5. Rebuild and redeploy the plugin and commandset.

Legacy TypeScript:
1. Create `server/src/tools/<tool_name>.ts` exporting `register<ToolName>Tool(server: McpServer)`.
2. Use `withRevitConnection()` for Revit communication.
3. Add/update the commandset command and `command.json`.

## Revit API Lookup

When writing or modifying C# code that calls Revit API, use the `revit-api-reference` skill to look up correct class names, method signatures, and parameters. Do not guess API names from memory.

## Revit Modeling Knowledge Base

Before creating or revising building geometry in Revit, read `.claude/knowledge-base/revit-modeling-rules.md` and `.agents/lesson_learned.md`.

Minimum modeling checks:
- Separate structural frame elements from architectural envelope elements.
- Give wall, cladding, roof, and canopy layers explicit offsets and clearances from columns, rafters, beams, and purlins.
- Split or cut envelope panels around openings and exposed structural members.
- After modeling, run `snapshot_workspace`, inspect the image for intersections or misalignment, and fix issues before reporting completion.

## Key Constraints

- All elevation and distance units passed to Revit tools are in millimeters.
- Phase 1 uses named pipe `revit-mcp`; legacy uses WebSocket `localhost:8080`.
- Revit add-ins must be installed under `%APPDATA%\Autodesk\Revit\Addins\<version>\`.
- `RevitMcpServer` publish is intentionally untrimmed because MCP tool discovery and plugin command loading use reflection.

## C# Coding Pitfalls

- **Namespace conflicts**: `ProjectInfo`, `Wall`, etc. can clash with `Autodesk.Revit.DB.*`. Use fully qualified names or rename custom classes.
- **Multi-version Revit API**: Use conditional compilation for API differences. Example: `ElementId.IntegerValue` is deprecated in Revit 2024+; use `Id.Value` where supported.
- **.NET 4.8 restrictions** for Revit 2020-2024: avoid newer .NET APIs when editing multi-version commandset code.
- **Build config format**: commandset configuration names have spaces. Always quote them, for example `dotnet build -c "Release R25"`.

## Operational Notes

- Restart the MCP client after adding or renaming MCP tools.
- Restart Revit after replacing add-in DLLs.
- If Phase 1 returns `Method not found`, confirm `Commands\RevitMCPCommandSet\command.json` exists below the deployed plugin directory and that `RevitMCPCommandSet.dll` exists in the matching version folder.

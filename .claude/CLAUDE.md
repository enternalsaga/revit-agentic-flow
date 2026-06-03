# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP (Model Context Protocol) server for Autodesk Revit. It enables AI assistants to read, create, modify, and delete elements in Revit projects.

## Architecture

Primary execution chain:

```text
AI Client <--stdio--> RevitMcpServer (C# MCP) <--named pipe: revit-mcp--> RevitMcpPlugin (C# add-in) --> Command Set (C#) --> Revit API
```

- **`src/RevitMcpServer/`** - .NET 8 stdio MCP server. Tool wrappers live in `Tools/` and forward JSON-RPC requests through `Pipes/PipeClient`.
- **`src/RevitMcpSdk/`** - shared SDK contracts and JSON-RPC models used by the server and plugin.
- **`src/RevitMcpPlugin/`** - Revit add-in for 2024 (`net48`) and 2025 (`net8.0-windows`). It starts a named pipe service, dispatches requests through `CommandExecutor`, and loads command assemblies via `CommandManager`.
- **`src/RevitMcpCommandSet/`** - Revit command implementations. The plugin loads this assembly via `CommandManager`.
- **`src/RevitMcpCommandSet/command.json`** - command manifest used by the plugin configuration sync.

Named pipe protocol:
- Pipe name: `revit-mcp`
- Framing: 4-byte little-endian message length followed by a UTF-8 JSON-RPC payload
- Server timeout: 120 seconds per Revit command
- Pipe connect timeout: 5 seconds

## Build, Publish, Deploy

Build all C# projects:

```powershell
dotnet build src/RevitMcpServer.sln -c Release
```

Publish the self-contained MCP server:

```powershell
dotnet publish src/RevitMcpServer/RevitMcpServer.csproj -c Release -r win-x64 --self-contained
```

Build the Revit 2024 or 2025 commandset:

```powershell
dotnet build "src/RevitMcpCommandSet/RevitMCPCommandSet.csproj" -c "Release R24"
dotnet build "src/RevitMcpCommandSet/RevitMCPCommandSet.csproj" -c "Release R25"
```

Deploy plugin layout for Revit 2024 or 2025:

```powershell
.\.scripts\deploy-phase1.ps1 -RevitVersion 2024
.\.scripts\deploy-phase1.ps1 -RevitVersion 2025
```

The deploy script writes a version-specific `.addin` file with an absolute assembly path under `%APPDATA%\Autodesk\Revit\Addins\<version>`.

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

## Tests

C# solution build:

```powershell
dotnet build src/RevitMcpServer.sln -c Release
```

Live Revit integration testing requires Revit 2024 or 2025 open with the Phase 1 plugin loaded:

```powershell
npx @anthropic-ai/mcp-inspector src\RevitMcpServer\bin\Release\net8.0-windows\win-x64\publish\RevitMcpServer.exe
```

Use `say_hello` and `get_current_view_info` as smoke tests.

Existing commandset integration tests require Revit open:

```powershell
dotnet test .\tests\RevitMcpCommandSet.Tests\RevitMCPCommandSet.Tests.csproj -c Debug.R24 -r win-x64
dotnet test .\tests\RevitMcpCommandSet.Tests\RevitMCPCommandSet.Tests.csproj -c Debug.R25 -r win-x64
```

## Adding a New MCP Tool

1. Add or update a C# wrapper method in `src/RevitMcpServer/Tools/*Tools.cs`.
2. Forward to Revit through `PipeClient.SendCommandAsync(commandName, parameters)`.
3. Add or update the command implementation under `src/RevitMcpCommandSet/Commands` and `Services`.
4. Add the command entry to `src/RevitMcpCommandSet/command.json`.
5. Rebuild `src/RevitMcpServer.sln` and `src/RevitMcpCommandSet/RevitMCPCommandSet.csproj` for the target Revit version.
6. Redeploy with `.\.scripts\deploy-phase1.ps1`.

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
- The MCP server communicates with the Revit add-in via named pipe `revit-mcp`.
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
- If the server returns `Method not found`, confirm `RevitMcpCommandSet.dll` and `command.json` exist below the deployed plugin directory for the target Revit version.

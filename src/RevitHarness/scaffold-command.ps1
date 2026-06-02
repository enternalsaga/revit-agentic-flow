param(
    [Parameter(Mandatory=$true)][string]$ApprovedProposalPath,
    [switch]$DryRun,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

if (-not $DryRun -and -not $Apply) {
    $DryRun = $true
}

$validationScript = Join-Path $PSScriptRoot "validate-command-proposal.ps1"
if (-not (Test-Path $validationScript)) {
    throw "Missing validator: $validationScript"
}

$validation = powershell -NoProfile -ExecutionPolicy Bypass -File $validationScript -ProposalPath $ApprovedProposalPath -RequireApproved | ConvertFrom-Json
if (-not $validation.success) {
    throw "Proposal validation failed: $($validation.errors -join '; ')"
}

$proposal = Get-Content -Path $ApprovedProposalPath -Raw | ConvertFrom-Json
$workspaceRoot = Resolve-Path "$PSScriptRoot/../.."
$scaffoldRoot = Join-Path $workspaceRoot ".revit-harness/command-scaffolds/$($proposal.commandName)"

function Write-GeneratedFile {
    param([string]$RelativePath, [string]$Content)
    $targetPath = if ($DryRun) {
        Join-Path $scaffoldRoot $RelativePath
    } else {
        Join-Path $workspaceRoot $RelativePath
    }
    $targetDir = Split-Path -Parent $targetPath
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    Set-Content -Path $targetPath -Value $Content -Encoding UTF8
    return $targetPath
}

function Get-SwitchOrCreate3DViewTypeScript {
@'
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSwitchOrCreate3DViewTool(server: McpServer) {
  server.tool(
    "switch_or_create_3d_view",
    "Create or activate an isometric 3D view for verification snapshots.",
    {
      viewName: z.string().default("MCP_3D_Verification"),
      detailLevel: z.enum(["Coarse", "Medium", "Fine"]).default("Fine"),
      activate: z.boolean().default(true),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("switch_or_create_3d_view", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Switch or create 3D view failed: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
        };
      }
    }
  );
}
'@
}

function Get-SwitchOrCreate3DViewCommand {
@'
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class SwitchOrCreate3DViewCommand : ExternalEventCommandBase
    {
        public override string CommandName => "switch_or_create_3d_view";

        public SwitchOrCreate3DViewCommand(UIApplication uiApp) : base(uiApp)
        {
        }

        protected override object ExecuteInTransaction(JObject parameters, string requestId)
        {
            var viewName = parameters["viewName"]?.ToString();
            if (string.IsNullOrWhiteSpace(viewName))
                viewName = "MCP_3D_Verification";

            var detailLevelText = parameters["detailLevel"]?.ToString();
            if (string.IsNullOrWhiteSpace(detailLevelText))
                detailLevelText = "Fine";

            var activate = parameters["activate"]?.Value<bool?>() ?? true;
            var doc = UiApplication.ActiveUIDocument.Document;

            View3D view = null;
            foreach (var existing in new FilteredElementCollector(doc).OfClass(typeof(View3D)))
            {
                var candidate = existing as View3D;
                if (candidate != null && !candidate.IsTemplate && candidate.Name == viewName)
                {
                    view = candidate;
                    break;
                }
            }

            var created = false;
            if (view == null)
            {
                ViewFamilyType viewFamilyType = null;
                foreach (var item in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)))
                {
                    var candidate = item as ViewFamilyType;
                    if (candidate != null && candidate.ViewFamily == ViewFamily.ThreeDimensional)
                    {
                        viewFamilyType = candidate;
                        break;
                    }
                }

                if (viewFamilyType == null)
                    throw new InvalidOperationException("No 3D ViewFamilyType is available in this document.");

                view = View3D.CreateIsometric(doc, viewFamilyType.Id);
                view.Name = viewName;
                created = true;
            }

            if (detailLevelText == "Coarse")
                view.DetailLevel = ViewDetailLevel.Coarse;
            else if (detailLevelText == "Medium")
                view.DetailLevel = ViewDetailLevel.Medium;
            else
                view.DetailLevel = ViewDetailLevel.Fine;

            if (activate)
                UiApplication.ActiveUIDocument.RequestViewChange(view);

            return new
            {
                success = true,
                viewId = view.Id.Value,
                viewName = view.Name,
                created,
                activated = activate
            };
        }
    }
}
'@
}

function Get-CSharpWrapperSnippet {
@'
    [McpServerTool(Name = "switch_or_create_3d_view")]
    [Description("Create or activate an isometric 3D view for verification snapshots.")]
    public static async Task<string> SwitchOrCreate3DView(
        [Description("View name to create or activate.")] string viewName = "MCP_3D_Verification",
        [Description("Detail level: Coarse, Medium, or Fine.")] string detailLevel = "Fine",
        [Description("If true, request Revit to activate the view.")] bool activate = true)
    {
        return await SendCommand("switch_or_create_3d_view", new { viewName, detailLevel, activate });
    }
'@
}

function Get-ManifestEntry {
@'
    {
      "commandName": "switch_or_create_3d_view",
      "description": "Create or activate an isometric 3D view for verification snapshots",
      "assemblyPath": "RevitMCPCommandSet.dll"
    }
'@
}

function Get-LiveEval {
@'
param(
    [string]$ViewName = "MCP_3D_Verification"
)

$ErrorActionPreference = "Stop"
$invoke = Join-Path (Resolve-Path ".").Path "src/RevitHarness/invoke-command.ps1"
$params = @{ viewName = $ViewName; detailLevel = "Fine"; activate = $true } | ConvertTo-Json -Compress
$paramsPath = New-TemporaryFile
try {
    Set-Content -Path $paramsPath.FullName -Value $params -Encoding UTF8
    $result = powershell -NoProfile -ExecutionPolicy Bypass -File $invoke -CommandName switch_or_create_3d_view -ParamsPath $paramsPath.FullName | ConvertFrom-Json
} finally {
    Remove-Item -LiteralPath $paramsPath.FullName -Force -ErrorAction SilentlyContinue
}

if (-not $result.success) {
    throw "switch_or_create_3d_view failed: $($result.error.message)"
}

if (-not $result.result.Response -and -not $result.result.viewId -and -not $result.result.result) {
    throw "switch_or_create_3d_view returned no view information."
}

[ordered]@{
    success = $true
    command = "switch_or_create_3d_view"
    viewName = $ViewName
    result = $result.result
} | ConvertTo-Json -Depth 20
'@
}

if ($proposal.commandName -ne "switch_or_create_3d_view") {
    throw "Scaffold template not implemented for command: $($proposal.commandName)"
}

$created = @()
$created += Write-GeneratedFile -RelativePath "mcp-servers-for-revit/server/src/tools/switch_or_create_3d_view.ts" -Content (Get-SwitchOrCreate3DViewTypeScript)
$created += Write-GeneratedFile -RelativePath "mcp-servers-for-revit/commandset/Commands/SwitchOrCreate3DViewCommand.cs" -Content (Get-SwitchOrCreate3DViewCommand)
$created += Write-GeneratedFile -RelativePath "src/RevitHarness/evals/live/eval-switch-or-create-3d-view.ps1" -Content (Get-LiveEval)
$created += Write-GeneratedFile -RelativePath "src/RevitHarness/generated-snippets/AccessTools.switch_or_create_3d_view.cs.txt" -Content (Get-CSharpWrapperSnippet)
$created += Write-GeneratedFile -RelativePath "src/RevitHarness/generated-snippets/command-json.switch_or_create_3d_view.json" -Content (Get-ManifestEntry)

[ordered]@{
    success = $true
    dryRun = [bool]$DryRun
    applied = [bool]$Apply
    commandName = $proposal.commandName
    createdFiles = @($created)
    nextSteps = @(
        "Review generated files.",
        "When applying, insert the C# wrapper snippet into src/RevitMcpServer/Tools/AccessTools.cs.",
        "When applying, insert the command manifest entry into mcp-servers-for-revit/command.json.",
        "Run dotnet build src/RevitMcpServer.sln -c Release.",
        "Run the command-specific live eval with Revit open."
    )
} | ConvertTo-Json -Depth 20

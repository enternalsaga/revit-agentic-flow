param(
    [Parameter(Mandatory=$true)][string]$GapPath,
    [string]$OutputDirectory = ".revit-harness/command-proposals"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $GapPath)) {
    throw "Command gap file not found: $GapPath"
}

$gap = Get-Content -Path $GapPath -Raw | ConvertFrom-Json

function New-ProposalId {
    param([string]$CommandName)
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $suffix = [guid]::NewGuid().ToString("N").Substring(0, 8)
    return "${stamp}_${CommandName}_${suffix}"
}

function New-SwitchOrCreate3DViewProposal {
    param($Gap)
    [ordered]@{
        schemaVersion = "1.0"
        proposalId = New-ProposalId -CommandName $Gap.missingCommandName
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        commandName = "switch_or_create_3d_view"
        summary = "Create or activate an isometric 3D view for verification snapshots."
        reason = "The existing switch_view command fails when the requested 3D view does not exist, forcing agents to write ad hoc C# helpers."
        mode = "propose"
        requiresHumanApproval = $true
        inputSchema = [ordered]@{
            viewName = [ordered]@{ type = "string"; default = "MCP_3D_Verification" }
            detailLevel = [ordered]@{ type = "string"; enum = @("Coarse", "Medium", "Fine"); default = "Fine" }
            activate = [ordered]@{ type = "boolean"; default = $true }
        }
        outputSchema = [ordered]@{
            success = [ordered]@{ type = "boolean" }
            viewId = [ordered]@{ type = "integer" }
            viewName = [ordered]@{ type = "string" }
            created = [ordered]@{ type = "boolean" }
            activated = [ordered]@{ type = "boolean" }
        }
        filesToCreate = @(
            "src/RevitMcpServer/Tools/AccessTools.cs",
            "src/RevitMcpCommandSet/Commands/SwitchOrCreate3DViewCommand.cs",
            "src/RevitHarness/evals/live/eval-switch-or-create-3d-view.ps1"
        )
        filesToModify = @(
            "src/RevitMcpServer/Tools/AccessTools.cs",
            "src/RevitMcpCommandSet/command.json"
        )
        evalPlan = @(
            "Run validate-command-proposal with -RequireApproved after user approval.",
            "Run scaffold-command in dry-run mode.",
            "Run registry-report and confirm switch_or_create_3d_view appears in manifest, C# wrappers, and commandset implementations.",
            "Run live eval-switch-or-create-3d-view with Revit open.",
            "Run snapshot_workspace after activating the created 3D view."
        )
        approval = [ordered]@{
            status = "pending"
            approvedBy = $null
            approvedAtUtc = $null
        }
        sourceGapId = $Gap.gapId
        sourceTracePaths = @($Gap.sourceTracePaths)
    }
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

switch ($gap.missingCommandName) {
    "switch_or_create_3d_view" {
        $proposal = New-SwitchOrCreate3DViewProposal -Gap $gap
    }
    default {
        $proposal = [ordered]@{
            schemaVersion = "1.0"
            proposalId = New-ProposalId -CommandName $gap.missingCommandName
            createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
            commandName = $gap.missingCommandName
            summary = "Add a dedicated Revit MCP command for $($gap.missingCommandName)."
            reason = ($gap.evidence -join " ")
            mode = "propose"
            requiresHumanApproval = $true
            inputSchema = [ordered]@{}
            outputSchema = [ordered]@{ success = [ordered]@{ type = "boolean" } }
            filesToCreate = @(
                "src/RevitMcpCommandSet/Commands/$($gap.missingCommandName).cs"
            )
            filesToModify = @(
                "src/RevitMcpServer/Tools/CreationTools.cs",
                "src/RevitMcpCommandSet/command.json"
            )
            evalPlan = @("Run registry-report and add a command-specific live smoke eval.")
            approval = [ordered]@{ status = "pending"; approvedBy = $null; approvedAtUtc = $null }
            sourceGapId = $gap.gapId
            sourceTracePaths = @($gap.sourceTracePaths)
        }
    }
}

$proposalPath = Join-Path $OutputDirectory ($proposal.proposalId + ".json")
$proposal | ConvertTo-Json -Depth 30 | Set-Content -Path $proposalPath -Encoding UTF8

[ordered]@{
    success = $true
    proposalPath = $proposalPath
    commandName = $proposal.commandName
    approvalStatus = $proposal.approval.status
} | ConvertTo-Json -Depth 10

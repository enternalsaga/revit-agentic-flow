param(
    [Parameter(Mandatory=$true)][string]$InputPath
)

$ErrorActionPreference = "Stop"
$inputObject = Get-Content -Path $InputPath -Raw | ConvertFrom-Json
$text = ($inputObject | ConvertTo-Json -Depth 50)

function New-Classification {
    param(
        [string]$Category,
        [double]$Confidence,
        [string[]]$Evidence,
        [string]$SuggestedNextAction,
        [string[]]$PatchTargets
    )
    [ordered]@{
        category = $Category
        confidence = $Confidence
        evidence = $Evidence
        suggestedNextAction = $SuggestedNextAction
        patchTargets = $PatchTargets
    }
}

if ($text -match 'Invalid JSON primitive|ConvertFrom-Json|Invalid object passed in') {
    New-Classification "json_quoting" 0.95 @("Input contains PowerShell JSON parsing failure.") "Pass params through invoke-command.ps1 using JSON file or canonical JSON." @("src/RevitHarness/invoke-command.ps1", ".agents/skills/run-revit-mcp/SKILL.md") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match 'Method .* not found|-32601') {
    New-Classification "command_not_registered" 0.92 @("Runtime bridge reported method not found.") "Run bootstrap and registry report to detect stale runtime or missing command registration." @("src/RevitHarness/bootstrap.ps1", "src/RevitMcpCommandSet/command.json") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match 'created 0 .*roof|not created by pick walls|invalid geometry') {
    New-Classification "invalid_geometry" 0.88 @("Command output indicates geometry was rejected or created zero elements.") "Retry with bounded valid geometry; if repeated, inspect command handler geometry assumptions." @("src/RevitMcpCommandSet/Services/CreateSlopedRoofEventHandler.cs", ".agents/skills/run-revit-mcp/SKILL.md") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match 'View not found') {
    New-Classification "view_missing" 0.9 @("Requested view does not exist in the active model.") "Create or discover a valid 3D view before switching, or snapshot the current view and report the limitation." @("src/RevitMcpCommandSet/Services/SwitchViewEventHandler.cs", "src/RevitHarness/evals/live/eval-create-or-switch-3d-view.ps1") | ConvertTo-Json -Depth 10
    exit 0
}

if ($text -match '"finalStatus"\s*:\s*"completed"' -and $text -match '"snapshots"\s*:\s*\[\s*\]' -and $text -match '"modelStatistics"\s*:\s*null') {
    New-Classification "verification_gap" 0.86 @("Trace completed without snapshot or model statistics.") "Run snapshot, statistics, and warnings check before final reporting." @(".agents/skills/run-revit-mcp/SKILL.md", "src/RevitHarness/trace-writer.ps1") | ConvertTo-Json -Depth 10
    exit 0
}

New-Classification "unknown" 0.2 @("No deterministic classifier rule matched.") "Record trace and review manually." @() | ConvertTo-Json -Depth 10

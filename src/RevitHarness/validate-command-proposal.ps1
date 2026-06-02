param(
    [Parameter(Mandatory=$true)][string]$ProposalPath,
    [switch]$RequireApproved
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ProposalPath)) {
    throw "Proposal file not found: $ProposalPath"
}

$proposal = Get-Content -Path $ProposalPath -Raw | ConvertFrom-Json
$errors = @()

foreach ($field in @("schemaVersion", "proposalId", "createdAtUtc", "commandName", "summary", "reason", "mode", "inputSchema", "outputSchema", "filesToCreate", "filesToModify", "evalPlan", "approval")) {
    if ($null -eq $proposal.$field) {
        $errors += "Missing required field: $field"
    }
}

if ($proposal.commandName -and ($proposal.commandName -notmatch '^[a-z][a-z0-9_]*$')) {
    $errors += "commandName must be snake_case and start with a lowercase letter."
}

if ($proposal.requiresHumanApproval -ne $true) {
    $errors += "requiresHumanApproval must be true."
}

if ($proposal.mode -notin @("propose", "implement")) {
    $errors += "mode must be propose or implement."
}

if ($RequireApproved -and $proposal.approval.status -ne "approved") {
    $errors += "Approved proposal required before scaffolding. Current status: $($proposal.approval.status)"
}

if ($proposal.filesToCreate) {
    foreach ($path in @($proposal.filesToCreate)) {
        if ($path -match '(^|[\\/])bin([\\/]|$)|(^|[\\/])obj([\\/]|$)|\.revit-harness[\\/]runs') {
            $errors += "filesToCreate contains forbidden generated path: $path"
        }
    }
}

$result = [ordered]@{
    success = ($errors.Count -eq 0)
    proposalPath = $ProposalPath
    commandName = $proposal.commandName
    errorCount = $errors.Count
    errors = @($errors)
}

$result | ConvertTo-Json -Depth 20

if ($errors.Count -gt 0) {
    exit 1
}

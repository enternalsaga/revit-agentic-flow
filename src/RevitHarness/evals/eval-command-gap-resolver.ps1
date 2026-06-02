$ErrorActionPreference = "Stop"
$workspaceRoot = (Resolve-Path ".").Path

$detect = Join-Path $workspaceRoot "src/RevitHarness/detect-command-gap.ps1"
$generate = Join-Path $workspaceRoot "src/RevitHarness/generate-command-proposal.ps1"
$validate = Join-Path $workspaceRoot "src/RevitHarness/validate-command-proposal.ps1"
$scaffold = Join-Path $workspaceRoot "src/RevitHarness/scaffold-command.ps1"
$traceFixture = Join-Path $workspaceRoot "src/RevitHarness/fixtures/command-gaps/view-missing-trace.json"
$approvedFixture = Join-Path $workspaceRoot "src/RevitHarness/fixtures/command-proposals/switch_or_create_3d_view.json"

$assertions = @()

function Add-Assertion {
    param([string]$Name, [bool]$Passed, [string]$Message)
    $script:assertions += [ordered]@{ name = $Name; passed = $Passed; message = $Message }
}

$gapResult = powershell -NoProfile -ExecutionPolicy Bypass -File $detect -TracePath $traceFixture | ConvertFrom-Json
Add-Assertion -Name "detectsOneGap" -Passed ($gapResult.gapCount -eq 1) -Message "Expected one command gap from fixture."
Add-Assertion -Name "detectsSwitchOrCreate3DView" -Passed ($gapResult.gaps[0].missingCommandName -eq "switch_or_create_3d_view") -Message "Expected switch_or_create_3d_view gap."

$gapPath = Get-ChildItem ".revit-harness/command-gaps" -Filter "*switch_or_create_3d_view*.json" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

$proposalResult = powershell -NoProfile -ExecutionPolicy Bypass -File $generate -GapPath $gapPath | ConvertFrom-Json
Add-Assertion -Name "generatesProposal" -Passed ($proposalResult.commandName -eq "switch_or_create_3d_view") -Message "Expected proposal command name."
Add-Assertion -Name "proposalDefaultsPending" -Passed ($proposalResult.approvalStatus -eq "pending") -Message "Generated proposals must default to pending."

$validation = powershell -NoProfile -ExecutionPolicy Bypass -File $validate -ProposalPath $approvedFixture -RequireApproved | ConvertFrom-Json
Add-Assertion -Name "validatesApprovedFixture" -Passed ($validation.success -eq $true) -Message "Approved fixture must validate."

$scaffoldResult = powershell -NoProfile -ExecutionPolicy Bypass -File $scaffold -ApprovedProposalPath $approvedFixture -DryRun | ConvertFrom-Json
Add-Assertion -Name "scaffoldDryRunSucceeds" -Passed ($scaffoldResult.success -eq $true) -Message "Dry-run scaffold must succeed."
Add-Assertion -Name "scaffoldCreatesFiveFiles" -Passed ($scaffoldResult.createdFiles.Count -eq 5) -Message "Expected five generated dry-run files."

$failed = @($assertions | Where-Object { -not $_.passed })
$result = [ordered]@{
    evalName = "eval-command-gap-resolver"
    success = ($failed.Count -eq 0)
    assertionCount = $assertions.Count
    failedCount = $failed.Count
    assertions = @($assertions)
}

$result | ConvertTo-Json -Depth 20

if ($failed.Count -gt 0) {
    exit 1
}

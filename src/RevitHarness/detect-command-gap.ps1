param(
    [Parameter(Mandatory=$true)][string]$TracePath,
    [string]$RegistryReportPath = "",
    [string]$OutputDirectory = ".revit-harness/command-gaps"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $TracePath)) {
    throw "Trace file not found: $TracePath"
}

$trace = Get-Content -Path $TracePath -Raw | ConvertFrom-Json
$registry = $null
if ($RegistryReportPath -and (Test-Path $RegistryReportPath)) {
    $registry = Get-Content -Path $RegistryReportPath -Raw | ConvertFrom-Json
}

function New-GapId {
    param([string]$CommandName)
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $suffix = [guid]::NewGuid().ToString("N").Substring(0, 8)
    return "${stamp}_${CommandName}_${suffix}"
}

function Test-CommandKnown {
    param([string]$CommandName, $Registry)
    if (-not $Registry) { return $false }
    $known = @()
    if ($Registry.manifestCommands) { $known += @($Registry.manifestCommands) }
    if ($Registry.typescriptTools) { $known += @($Registry.typescriptTools) }
    if ($Registry.csharpMcpTools) { $known += @($Registry.csharpMcpTools) }
    if ($Registry.commandsetImplementations) { $known += @($Registry.commandsetImplementations) }
    return $known -contains $CommandName
}

$gaps = @()

foreach ($failure in @($trace.classifiedFailures)) {
    if ($failure.category -eq "view_missing") {
        $commandName = "switch_or_create_3d_view"
        $known = Test-CommandKnown -CommandName $commandName -Registry $registry
        if (-not $known) {
            $gaps += [ordered]@{
                schemaVersion = "1.0"
                gapId = New-GapId -CommandName $commandName
                createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
                missingCommandName = $commandName
                category = "view_missing"
                confidence = [double]$failure.confidence
                evidence = @($failure.evidence)
                sourceTracePaths = @($TracePath)
                fallbackUsed = "custom C# helper or manual 3D view creation"
                recommendedMode = "propose"
                requiresHumanApproval = $true
                suggestedSchema = [ordered]@{
                    viewName = "string, default MCP_3D_Verification"
                    detailLevel = "Coarse|Medium|Fine, default Fine"
                    activate = "boolean, default true"
                }
                suggestedEval = "eval-switch-or-create-3d-view"
            }
        }
    }

    if ($failure.category -eq "command_not_registered") {
        $missingName = $failure.missingCommandName
        if (-not $missingName) {
            foreach ($command in @($trace.commands)) {
                if ($command.error.message -match "Method '([^']+)' not found") {
                    $missingName = $Matches[1]
                    break
                }
            }
        }
        if ($missingName) {
            $gaps += [ordered]@{
                schemaVersion = "1.0"
                gapId = New-GapId -CommandName $missingName
                createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
                missingCommandName = $missingName
                category = "command_not_registered"
                confidence = [double]$failure.confidence
                evidence = @($failure.evidence)
                sourceTracePaths = @($TracePath)
                fallbackUsed = "none"
                recommendedMode = "propose"
                requiresHumanApproval = $true
                suggestedSchema = [ordered]@{}
                suggestedEval = "registry and live smoke eval for $missingName"
            }
        }
    }
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

$output = [ordered]@{
    success = $true
    tracePath = $TracePath
    gapCount = $gaps.Count
    gaps = @($gaps)
}

if ($gaps.Count -gt 0) {
    foreach ($gap in $gaps) {
        $path = Join-Path $OutputDirectory ($gap.gapId + ".json")
        $gap | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding UTF8
    }
}

$output | ConvertTo-Json -Depth 20

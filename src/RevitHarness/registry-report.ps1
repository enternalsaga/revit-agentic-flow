param(
    [string]$WorkspaceRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

function Get-RelativeNameSet {
    param(
        [string]$Path,
        [string]$Pattern,
        [scriptblock]$Transform
    )

    if (-not (Test-Path $Path)) {
        return @()
    }

    Get-ChildItem -Path $Path -Filter $Pattern -File |
        ForEach-Object { & $Transform $_ } |
        Where-Object { $_ } |
        Sort-Object -Unique
}

$commandJsonPath = Join-Path $WorkspaceRoot "src/RevitMcpCommandSet/command.json"
$csharpToolsPath = Join-Path $WorkspaceRoot "src/RevitMcpServer/Tools"
$commandsetPath = Join-Path $WorkspaceRoot "src/RevitMcpCommandSet/Commands"

if (-not (Test-Path $commandJsonPath)) {
    throw "Missing command manifest: $commandJsonPath"
}

$manifest = Get-Content -Path $commandJsonPath -Raw | ConvertFrom-Json
$manifestCommands = @($manifest.commands | ForEach-Object { $_.commandName } | Sort-Object -Unique)

$csharpMcpTools = @()
if (Test-Path $csharpToolsPath) {
    $csharpMcpTools = Get-ChildItem -Path $csharpToolsPath -Filter "*.cs" -File |
        ForEach-Object {
            Select-String -Path $_.FullName -Pattern 'McpServerTool\(Name\s*=\s*"([^"]+)"' -AllMatches |
                ForEach-Object { $_.Matches.Groups[1].Value }
        } |
        Sort-Object -Unique
}

$commandsetImplementations = @()
if (Test-Path $commandsetPath) {
    $commandsetImplementations = Get-ChildItem -Path $commandsetPath -Filter "*.cs" -Recurse -File |
        ForEach-Object {
            Select-String -Path $_.FullName -Pattern 'CommandName\s*=>\s*"([^"]+)"' -AllMatches |
                ForEach-Object { $_.Matches.Groups[1].Value }
        } |
        Sort-Object -Unique
}

function Compare-Layers {
    param([string[]]$Left, [string[]]$Right)
    $rightSet = @{}
    foreach ($item in $Right) { $rightSet[$item] = $true }
    @($Left | Where-Object { -not $rightSet.ContainsKey($_) })
}

$report = [ordered]@{
    success = $true
    workspaceRoot = $WorkspaceRoot
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    manifestCommands = $manifestCommands
    csharpMcpTools = $csharpMcpTools
    commandsetImplementations = $commandsetImplementations
    runtimeRegisteredCommands = $null
    counts = [ordered]@{
        manifest = $manifestCommands.Count
        csharpMcpTools = $csharpMcpTools.Count
        commandsetImplementations = $commandsetImplementations.Count
        runtimeRegisteredCommands = $null
    }
    coverageGaps = [ordered]@{
        manifest_without_csharp_wrapper = [object[]]@(Compare-Layers $manifestCommands $csharpMcpTools)
        manifest_without_commandset_implementation = [object[]]@(Compare-Layers $manifestCommands $commandsetImplementations)
        csharp_wrapper_without_manifest = [object[]]@(Compare-Layers $csharpMcpTools $manifestCommands)
        runtime_missing_manifest_command = $null
        runtime_extra_command = $null
    }
}

$report | ConvertTo-Json -Depth 20

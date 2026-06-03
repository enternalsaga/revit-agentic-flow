param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("2024", "2025")]
    [string]$RevitVersion,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$targetFramework = if ($RevitVersion -eq "2024") { "net48" } else { "net8.0-windows" }
$commandConfig = if ($RevitVersion -eq "2024") { "Release R24" } else { "Release R25" }

if (-not $SkipBuild) {
    dotnet build (Join-Path $repoRoot "src\RevitMcpPlugin\RevitMcpPlugin.csproj") -c Release -f $targetFramework
    dotnet build (Join-Path $repoRoot "src\RevitMcpCommandSet\RevitMCPCommandSet.csproj") -c $commandConfig
}

$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$target = Join-Path $addins "revit-mcp-v2"
$commandSetRoot = Join-Path $target "Commands\RevitMCPCommandSet"
$commandTarget = Join-Path $commandSetRoot $RevitVersion

New-Item -ItemType Directory -Path $target -Force | Out-Null
New-Item -ItemType Directory -Path $commandTarget -Force | Out-Null

$pluginOutput = Join-Path $repoRoot "build\bin\RevitMcpPlugin\Release\$targetFramework"
$commandOutput = Join-Path $repoRoot "src\RevitMcpCommandSet\bin\$commandConfig"

Copy-Item (Join-Path $pluginOutput "*") -Destination $target -Recurse -Force
Copy-Item (Join-Path $commandOutput "*") -Destination $commandTarget -Recurse -Force
Copy-Item (Join-Path $repoRoot "src\RevitMcpCommandSet\command.json") -Destination (Join-Path $commandSetRoot "command.json") -Force

$assembly = Join-Path $target "RevitMcpPlugin.dll"
$addinPath = Join-Path $addins "revit-mcp-v2.addin"
$addin = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit MCP Phase 1</Name>
    <Assembly>$assembly</Assembly>
    <AddInId>090A4C8C-61DC-426D-87DF-E4BAE0F80EC1</AddInId>
    <FullClassName>RevitMcpPlugin.App</FullClassName>
    <VendorId>revit-mcp</VendorId>
    <VendorDescription>Revit MCP Server Phase 1</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

[System.IO.File]::WriteAllText($addinPath, $addin)

[pscustomobject]@{
    RevitVersion = $RevitVersion
    TargetFramework = $targetFramework
    CommandConfiguration = $commandConfig
    Addin = $addinPath
    PluginDirectory = $target
    CommandDirectory = $commandTarget
    PluginDll = Test-Path (Join-Path $target "RevitMcpPlugin.dll")
    CommandDll = Test-Path (Join-Path $commandTarget "RevitMCPCommandSet.dll")
    CommandJson = Test-Path (Join-Path $commandSetRoot "command.json")
} | Format-List

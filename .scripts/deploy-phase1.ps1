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

function Invoke-DotNet {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$DotNetArgs
    )

    & dotnet @DotNetArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($DotNetArgs -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Test-FileLocked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        return $false
    }
    catch [System.UnauthorizedAccessException] {
        return $true
    }
    catch [System.IO.IOException] {
        return $true
    }
    finally {
        if ($stream) {
            $stream.Dispose()
        }
    }
}

function Assert-DeployFileNotLocked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (Test-FileLocked -Path $Path) {
        $message = @"
Khong the deploy vi $Label dang bi khoa:
$Path

Hay dong Revit $RevitVersion truoc khi deploy thay doi vao RevitMcpPlugin.dll hoac RevitMCPCommandSet.dll.

Neu chi sua RevitMcpServer thi khong can restart Revit. Nhung cac DLL add-in da duoc Revit load thi khong the thay the khi Revit dang dung chung.
"@
        throw $message
    }
}

if (-not $SkipBuild) {
    Invoke-DotNet build (Join-Path $repoRoot "src\RevitMcpPlugin\RevitMcpPlugin.csproj") -c Release -f $targetFramework
    Invoke-DotNet build (Join-Path $repoRoot "src\RevitMcpCommandSet\RevitMCPCommandSet.csproj") -c $commandConfig
}

$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$target = Join-Path $addins "revit-mcp"
$commandSetRoot = Join-Path $target "Commands\RevitMCPCommandSet"
$commandTarget = Join-Path $commandSetRoot $RevitVersion
$deployedPluginDll = Join-Path $target "RevitMcpPlugin.dll"
$deployedCommandDll = Join-Path $commandTarget "RevitMCPCommandSet.dll"

Assert-DeployFileNotLocked -Path $deployedPluginDll -Label "RevitMcpPlugin.dll"
Assert-DeployFileNotLocked -Path $deployedCommandDll -Label "RevitMCPCommandSet.dll"

New-Item -ItemType Directory -Path $target -Force | Out-Null
if (Test-Path $commandTarget) {
    Remove-Item -LiteralPath $commandTarget -Recurse -Force
}
New-Item -ItemType Directory -Path $commandTarget -Force | Out-Null

$pluginOutput = Join-Path $repoRoot "build\bin\RevitMcpPlugin\Release\$targetFramework"
$commandOutput = Join-Path $repoRoot "build\bin\RevitMCPCommandSet\$commandConfig"

Copy-Item (Join-Path $pluginOutput "*") -Destination $target -Recurse -Force
Get-ChildItem -LiteralPath $commandOutput -Force |
    Where-Object { $_.Name -ne "publish" } |
    Copy-Item -Destination $commandTarget -Recurse -Force
Copy-Item (Join-Path $repoRoot "src\RevitMcpCommandSet\command.json") -Destination (Join-Path $commandSetRoot "command.json") -Force

$assembly = Join-Path $target "RevitMcpPlugin.dll"
$addinPath = Join-Path $addins "revit-mcp.addin"
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

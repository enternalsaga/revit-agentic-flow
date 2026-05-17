<#
.SYNOPSIS
Reusable helper for calling the Revit MCP TCP JSON-RPC bridge and running compiled C# helpers.

.EXAMPLES
.\Invoke-RevitMcpJsonRpc.ps1 -Method get_project_info

.\Invoke-RevitMcpJsonRpc.ps1 -Method snapshot_workspace -ParamsJson '{"includeImage":true,"includeVisibleElements":true,"pixelSize":1600}'

.\Invoke-RevitMcpJsonRpc.ps1 -CodeFile .\snippet.cs

.\Invoke-RevitMcpJsonRpc.ps1 -SourcePath .\model\MyModel.cs -TypeName MyNamespace.MyModel -MethodName Execute
#>

[CmdletBinding(DefaultParameterSetName = "Command")]
param(
    [Parameter(ParameterSetName = "Command")]
    [string]$Method = "get_project_info",

    [Parameter(ParameterSetName = "Command")]
    [string]$ParamsJson = "{}",

    [Parameter(ParameterSetName = "CodeInline", Mandatory = $true)]
    [string]$Code,

    [Parameter(ParameterSetName = "CodeFile", Mandatory = $true)]
    [string]$CodeFile,

    [Parameter(ParameterSetName = "CompileAndRun", Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(ParameterSetName = "CompileAndRun", Mandatory = $true)]
    [string]$TypeName,

    [Parameter(ParameterSetName = "CompileAndRun")]
    [string]$MethodName = "Execute",

    [Parameter(ParameterSetName = "CompileAndRun")]
    [string]$OutputDirectory,

    [Parameter(ParameterSetName = "CompileAndRun")]
    [string]$AssemblyName,

    [Parameter(ParameterSetName = "CompileAndRun")]
    [string[]]$Reference,

    [Parameter(ParameterSetName = "CompileAndRun")]
    [switch]$CompileOnly,

    [string]$HostName = "127.0.0.1",

    [int]$Port = 8080,

    [int]$TimeoutSeconds = 120,

    [ValidateSet("auto", "none")]
    [string]$TransactionMode = "auto",

    [int]$RevitVersion = 2024,

    [switch]$Raw
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $executionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function ConvertFrom-JsonObject {
    param([string]$Json)
    if ([string]::IsNullOrWhiteSpace($Json)) {
        return @{}
    }

    return $Json | ConvertFrom-Json
}

function Invoke-RevitJsonRpc {
    param(
        [Parameter(Mandatory = $true)][string]$RpcMethod,
        [Parameter(Mandatory = $true)]$RpcParams
    )

    $requestId = "ps_{0}_{1}" -f ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()), ([Guid]::NewGuid().ToString("N").Substring(0, 8))
    $request = [ordered]@{
        jsonrpc = "2.0"
        method = $RpcMethod
        params = $RpcParams
        id = $requestId
    }
    $payload = ($request | ConvertTo-Json -Depth 100 -Compress)

    $client = [System.Net.Sockets.TcpClient]::new()
    $async = $client.BeginConnect($HostName, $Port, $null, $null)
    if (-not $async.AsyncWaitHandle.WaitOne([TimeSpan]::FromSeconds(10))) {
        $client.Close()
        throw "Timed out connecting to Revit MCP bridge at ${HostName}:${Port}. Is Revit open and the MCP plugin running?"
    }
    $client.EndConnect($async)

    try {
        $stream = $client.GetStream()
        $stream.ReadTimeout = $TimeoutSeconds * 1000
        $stream.WriteTimeout = 10000

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $stream.Write($bytes, 0, $bytes.Length)

        $buffer = New-Object byte[] 8192
        $builder = [System.Text.StringBuilder]::new()
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

        while ([DateTime]::UtcNow -lt $deadline) {
            if ($stream.DataAvailable) {
                $read = $stream.Read($buffer, 0, $buffer.Length)
                if ($read -le 0) {
                    break
                }
                [void]$builder.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $read))
                $text = $builder.ToString()
                try {
                    $response = $text | ConvertFrom-Json
                    $errorProperty = $response.PSObject.Properties["error"]
                    if ($errorProperty -and $null -ne $errorProperty.Value) {
                        $errorJson = $response.error | ConvertTo-Json -Depth 20 -Compress
                        throw "Revit JSON-RPC error: $errorJson"
                    }
                    $resultProperty = $response.PSObject.Properties["result"]
                    if (-not $resultProperty) {
                        throw "JSON-RPC response did not include a result property: $text"
                    }
                    return $resultProperty.Value
                }
                catch [System.ArgumentException] {
                    Start-Sleep -Milliseconds 50
                }
            }
            else {
                Start-Sleep -Milliseconds 50
            }
        }

        throw "Timed out waiting for JSON-RPC response from Revit for method '$RpcMethod'."
    }
    finally {
        $client.Close()
    }
}

function Find-CSharpCompiler {
    $candidates = @(
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $fromPath = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "Could not find csc.exe. Install .NET Framework developer tools or add csc.exe to PATH."
}

function Compile-CSharpHelper {
    param(
        [Parameter(Mandatory = $true)][string]$InputSource,
        [string]$OutDir,
        [string]$OutName,
        [string[]]$ExtraReferences
    )

    $sourceFullPath = Resolve-FullPath $InputSource
    if (-not (Test-Path -LiteralPath $sourceFullPath)) {
        throw "Source file not found: $sourceFullPath"
    }

    if ([string]::IsNullOrWhiteSpace($OutDir)) {
        $OutDir = Join-Path (Split-Path -Parent $sourceFullPath) "build"
    }
    $outFullDir = Resolve-FullPath $OutDir
    New-Item -ItemType Directory -Force -Path $outFullDir | Out-Null

    if ([string]::IsNullOrWhiteSpace($OutName)) {
        $baseName = [IO.Path]::GetFileNameWithoutExtension($sourceFullPath)
        $stamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"
        $OutName = "${baseName}_${stamp}.dll"
    }
    elseif (-not $OutName.EndsWith(".dll", [StringComparison]::OrdinalIgnoreCase)) {
        $OutName = "$OutName.dll"
    }

    $outputDll = Join-Path $outFullDir $OutName
    $revitInstall = "C:\Program Files\Autodesk\Revit $RevitVersion"
    $references = @(
        (Join-Path $revitInstall "RevitAPI.dll"),
        (Join-Path $revitInstall "RevitAPIUI.dll")
    )

    if ($ExtraReferences) {
        $references += $ExtraReferences
    }

    foreach ($ref in $references) {
        if (-not (Test-Path -LiteralPath $ref)) {
            throw "Reference not found: $ref"
        }
    }

    $csc = Find-CSharpCompiler
    $args = @("/nologo", "/target:library", "/out:$outputDll")
    foreach ($ref in $references) {
        $args += "/reference:$ref"
    }
    $args += $sourceFullPath

    $compilerOutput = & $csc @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "C# compile failed:`n$($compilerOutput -join [Environment]::NewLine)"
    }

    return $outputDll
}

function New-DllBootstrapCode {
    param(
        [Parameter(Mandatory = $true)][string]$DllPath,
        [Parameter(Mandatory = $true)][string]$TargetType,
        [Parameter(Mandatory = $true)][string]$TargetMethod
    )

    $escapedDll = $DllPath.Replace('"', '""')
    return @"
try {
    var asm = System.Reflection.Assembly.LoadFrom(@"$escapedDll");
    var targetType = asm.GetType("$TargetType");
    if (targetType == null) {
        return new System.Collections.Generic.Dictionary<string, object> {
            {"success", false},
            {"error", "Type not found: $TargetType"}
        };
    }
    var method = targetType.GetMethod("$TargetMethod", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    if (method == null) {
        return new System.Collections.Generic.Dictionary<string, object> {
            {"success", false},
            {"error", "Static public method not found: $TargetMethod"}
        };
    }
    return method.Invoke(null, new object[] { document });
}
catch (System.Reflection.TargetInvocationException ex) {
    var e = ex.InnerException;
    return new System.Collections.Generic.Dictionary<string, object> {
        {"success", false},
        {"error", e == null ? ex.Message : e.Message},
        {"type", e == null ? ex.GetType().FullName : e.GetType().FullName},
        {"stack", e == null ? ex.StackTrace : e.StackTrace}
    };
}
catch (System.Exception ex) {
    return new System.Collections.Generic.Dictionary<string, object> {
        {"success", false},
        {"error", ex.Message},
        {"type", ex.GetType().FullName},
        {"stack", ex.StackTrace}
    };
}
"@
}

switch ($PSCmdlet.ParameterSetName) {
    "Command" {
        $paramsObject = ConvertFrom-JsonObject $ParamsJson
        $result = Invoke-RevitJsonRpc -RpcMethod $Method -RpcParams $paramsObject
    }
    "CodeInline" {
        $result = Invoke-RevitJsonRpc -RpcMethod "send_code_to_revit" -RpcParams @{
            code = $Code
            parameters = @()
            transactionMode = $TransactionMode
        }
    }
    "CodeFile" {
        $codePath = Resolve-FullPath $CodeFile
        $codeText = Get-Content -LiteralPath $codePath -Raw
        $result = Invoke-RevitJsonRpc -RpcMethod "send_code_to_revit" -RpcParams @{
            code = $codeText
            parameters = @()
            transactionMode = $TransactionMode
        }
    }
    "CompileAndRun" {
        $dll = Compile-CSharpHelper -InputSource $SourcePath -OutDir $OutputDirectory -OutName $AssemblyName -ExtraReferences $Reference
        if ($CompileOnly) {
            $result = [ordered]@{
                success = $true
                compiledDll = $dll
            }
        }
        else {
            $bootstrap = New-DllBootstrapCode -DllPath $dll -TargetType $TypeName -TargetMethod $MethodName
            $result = Invoke-RevitJsonRpc -RpcMethod "send_code_to_revit" -RpcParams @{
                code = $bootstrap
                parameters = @()
                transactionMode = $TransactionMode
            }
        }
    }
}

if ($Raw) {
    $result
}
else {
    $result | ConvertTo-Json -Depth 100
}

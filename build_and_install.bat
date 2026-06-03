@echo off
setlocal

set "LOCAL_DOTNET=%USERPROFILE%\.dotnet"
if exist "%LOCAL_DOTNET%\dotnet.exe" (
    set "DOTNET_ROOT=%LOCAL_DOTNET%"
    set "PATH=%LOCAL_DOTNET%;%PATH%"
)

set "HAS_DOTNET_SDK="
for /f "delims=" %%S in ('dotnet --list-sdks 2^>nul') do set "HAS_DOTNET_SDK=1"
if not defined HAS_DOTNET_SDK (
    echo [ERROR] No .NET SDK found.
    echo [ERROR] Expected local SDK at "%LOCAL_DOTNET%" or an SDK on PATH.
    echo [ERROR] Install the SDK or place it under "%LOCAL_DOTNET%".
    exit /b 1
)

set "REVIT_VERSION=%~1"
if "%REVIT_VERSION%"=="" set "REVIT_VERSION=2024"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0.scripts\deploy-phase1.ps1" -RevitVersion "%REVIT_VERSION%"
pause

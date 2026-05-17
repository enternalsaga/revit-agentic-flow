@echo off
title MCP Servers for Revit - Build ^& Install

echo.
echo ============================================================
echo    MCP Servers for Revit - Build ^& Install
echo ============================================================
echo.

set "REVIT_VERSION=2024"
if not "%~1"=="" (
    set "REVIT_VERSION=%~1"
) else (
    set /p REVIT_VERSION="    Enter Revit version (2023/2024/2025/2026) [default: 2024]: "
)
:: Remove trailing spaces
set "REVIT_VERSION=%REVIT_VERSION: =%"

set "R_VER=%REVIT_VERSION:~-2%"
set "CONFIG=Debug R%R_VER%"

echo.
echo [1/2] Building C# Solution with configuration %CONFIG%...
echo.

cd /d "%~dp0mcp-servers-for-revit"
dotnet build mcp-servers-for-revit.sln -c "%CONFIG%"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed. Please check the errors above.
    pause
    exit /b 1
)

echo.
echo [2/2] Build successful! Calling installation script...
echo.
cd /d "%~dp0"
call install.bat %REVIT_VERSION%

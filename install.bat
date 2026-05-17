@echo off
title MCP Servers for Revit - Install

echo.
echo ============================================================
echo    MCP Servers for Revit - Install Add-in
echo ============================================================
echo.

:: Support Revit Version selection
set "REVIT_VERSION=2024"
if not "%~1"=="" (
    set "REVIT_VERSION=%~1"
) else (
    set /p REVIT_VERSION="    Enter Revit version (2023/2024/2025/2026) [default: 2024]: "
)
:: Remove trailing spaces
set "REVIT_VERSION=%REVIT_VERSION: =%"

set "R_VER=%REVIT_VERSION:~-2%"
set "SOURCE_DIR=%~dp0mcp-servers-for-revit\plugin\bin\AddIn %REVIT_VERSION% Debug R%R_VER%"

if not exist "%SOURCE_DIR%\mcp-servers-for-revit.addin" (
    echo [ERROR] Could not find compiled Addin at:
    echo        %SOURCE_DIR%
    echo        Please build the project first or check the path!
    echo.
    pause
    exit /b 1
)

:: Check Administrator privileges
net session >nul 2>&1
if "%errorLevel%"=="0" (
    set "TARGET_DIR=%PROGRAMDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%"
    echo [INFO] Running as Administrator. 
    echo        Installing for ALL USERS ^(System Add-ins^).
) else (
    set "TARGET_DIR=%APPDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%"
    echo [INFO] Running as normal User. 
    echo        Installing for CURRENT USER ^(Local Add-ins^).
    echo        ^- To install for all users, run this file as Administrator.
)
echo.

echo [1/3] Copying Addin file...
echo       -^> %TARGET_DIR%

if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

xcopy "%SOURCE_DIR%\mcp-servers-for-revit.addin" "%TARGET_DIR%\" /Y /Q >nul

set "TARGET_PLUGIN_DIR=%TARGET_DIR%\revit_mcp_plugin"
if exist "%TARGET_PLUGIN_DIR%" rmdir /s /q "%TARGET_PLUGIN_DIR%"

echo [2/3] Copying DLL files...
xcopy "%SOURCE_DIR%\revit_mcp_plugin" "%TARGET_PLUGIN_DIR%" /E /I /Q /Y >nul
if errorlevel 1 (
    echo [ERROR] Failed to copy files.
    pause
    exit /b 1
)

echo [3/3] Unblocking DLL files...
powershell -Command "Get-ChildItem '%TARGET_PLUGIN_DIR%' -Recurse -Filter *.dll | Unblock-File" 2>nul

echo.
echo ============================================================
echo    INSTALLATION SUCCESSFUL!
echo ============================================================
echo.
echo    Usage instructions:
echo    1. Open Revit %REVIT_VERSION%
echo    2. The MCP Plugin will load automatically
echo    3. The Node.js MCP Server is ready for requests
echo.
pause

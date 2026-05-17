@echo off
chcp 65001 >nul
title MCP Servers for Revit - Cài đặt

echo.
echo ============================================================
echo    MCP Servers for Revit - Cài đặt tự động
echo ============================================================
echo.

:: Kiểm tra Node.js
where node >nul 2>&1
if errorlevel 1 (
    echo [LỖI] Không tìm thấy Node.js!
    echo        Tải và cài đặt Node.js từ https://nodejs.org/
    echo        Yêu cầu: Node.js 20 trở lên
    echo.
    pause
    exit /b 1
)

:: Kiểm tra phiên bản Node.js
for /f "tokens=1 delims=v." %%a in ('node -v') do set NODE_MAJOR=%%a
for /f "tokens=2 delims=v." %%a in ('node -v') do set NODE_MAJOR=%%a
echo    Node.js: 
node -v

:: Xác định đường dẫn
set "SCRIPT_DIR=%~dp0"
set "SERVER_DIR=%SCRIPT_DIR%server"
set "PLUGIN_DIR=%SCRIPT_DIR%plugin"

:: ============================================================
:: Bước 1: Build TypeScript MCP Server
:: ============================================================
echo.
echo [1/4] Đang cài đặt dependencies cho MCP Server...
cd /d "%SERVER_DIR%"
call npm install --silent
if errorlevel 1 (
    echo [LỖI] npm install thất bại.
    pause
    exit /b 1
)

echo [2/4] Đang build MCP Server...
call npm run build
if errorlevel 1 (
    echo [LỖI] Build MCP Server thất bại.
    pause
    exit /b 1
)

:: ============================================================
:: Bước 2: Build C# Plugin + CommandSet
:: ============================================================
echo.
echo [3/4] Đang build Revit Plugin...

:: Hỗ trợ chọn phiên bản Revit
set "REVIT_VERSION=2024"
set /p REVIT_VERSION="    Phiên bản Revit (2023/2024/2025/2026) [mặc định: 2024]: "

:: Map version sang config
if "%REVIT_VERSION%"=="2023" set "BUILD_CONFIG=Debug R23"
if "%REVIT_VERSION%"=="2024" set "BUILD_CONFIG=Debug R24"
if "%REVIT_VERSION%"=="2025" set "BUILD_CONFIG=Debug R25"
if "%REVIT_VERSION%"=="2026" set "BUILD_CONFIG=Debug R26"

cd /d "%SCRIPT_DIR%"
dotnet build mcp-servers-for-revit.sln -c "%BUILD_CONFIG%" --verbosity quiet
if errorlevel 1 (
    echo [LỖI] Build C# plugin thất bại.
    echo        Đảm bảo đã cài .NET SDK và đóng Revit trước khi build.
    pause
    exit /b 1
)

:: ============================================================
:: Bước 3: Unblock DLLs
:: ============================================================
echo [4/4] Đang unblock DLL files...
set "ADDINS_DIR=%APPDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%\revit_mcp_plugin"
if exist "%ADDINS_DIR%" (
    powershell -Command "Get-ChildItem '%ADDINS_DIR%' -Recurse -Filter *.dll | Unblock-File" 2>nul
)

:: ============================================================
:: Hoàn tất
:: ============================================================
echo.
echo ============================================================
echo    CÀI ĐẶT THÀNH CÔNG!
echo ============================================================
echo.
echo    Plugin đã được cài vào:
echo    %ADDINS_DIR%
echo.
echo    Hướng dẫn sử dụng:
echo    1. Mở Revit %REVIT_VERSION%
echo    2. Plugin MCP sẽ tự động load
echo    3. Trong Antigravity/Claude, MCP server sẵn sàng kết nối
echo.
echo    MCP Server path (dùng trong mcp_config.json):
echo    %SERVER_DIR%\build\index.js
echo.
pause

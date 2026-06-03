@echo off
setlocal

set "REVIT_VERSION=%~1"
if "%REVIT_VERSION%"=="" set "REVIT_VERSION=2024"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0.scripts\deploy-phase1.ps1" -RevitVersion "%REVIT_VERSION%"
exit /b %ERRORLEVEL%

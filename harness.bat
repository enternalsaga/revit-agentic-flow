@echo off
setlocal enabledelayedexpansion

set "HARNESS_DIR=%~dp0src\RevitHarness"
set "PS=powershell -NoProfile -ExecutionPolicy Bypass -File"
set "HELP_EXIT_CODE=0"

if "%~1"=="" goto :help
if /i "%~1"=="help" goto :help
if /i "%~1"=="check" goto :check
if /i "%~1"=="registry" goto :registry
if /i "%~1"=="invoke" goto :invoke
if /i "%~1"=="classify" goto :classify
if /i "%~1"=="trace" goto :trace
if /i "%~1"=="evals" goto :evals
if /i "%~1"=="gap" goto :gap

echo [ERROR] Unknown subcommand: %~1
echo.
set "HELP_EXIT_CODE=2"
goto :help

:check
%PS% "%HARNESS_DIR%\bootstrap.ps1" -WriteCache
exit /b %ERRORLEVEL%

:registry
%PS% "%HARNESS_DIR%\registry-report.ps1"
exit /b %ERRORLEVEL%

:invoke
if "%~2"=="" (
    echo [ERROR] Usage: harness invoke ^<command_name^> [--params-file ^<file^>] [--params ^<json^>] [--timeout ^<seconds^>]
    exit /b 2
)
set "CMD_NAME=%~2"
set "PARAMS_ARG="
set "TIMEOUT_ARG="
shift & shift
:invoke_args
if "%~1"=="" goto :invoke_run
if /i "%~1"=="--params-file" (
    set "PARAMS_ARG=-ParamsPath "%~2""
    shift & shift
    goto :invoke_args
)
if /i "%~1"=="--timeout" (
    set "TIMEOUT_ARG=-TimeoutSeconds %~2"
    shift & shift
    goto :invoke_args
)
if /i "%~1"=="--params" (
    set "PARAMS_ARG=-ParamsJson "%~2""
    shift & shift
    goto :invoke_args
)
shift
goto :invoke_args
:invoke_run
%PS% "%HARNESS_DIR%\invoke-command.ps1" -CommandName %CMD_NAME% %PARAMS_ARG% %TIMEOUT_ARG%
exit /b %ERRORLEVEL%

:classify
if "%~2"=="" (
    echo [ERROR] Usage: harness classify ^<error-file.json^>
    exit /b 2
)
%PS% "%HARNESS_DIR%\classify-failure.ps1" -InputPath "%~2"
exit /b %ERRORLEVEL%

:trace
if /i "%~2"=="new" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness trace new ^<task-label^> [--intent "description"]
        goto :usage_error
    )
    set "INTENT_ARG="
    if /i "%~4"=="--intent" set "INTENT_ARG=-UserIntent "%~5""
    %PS% "%HARNESS_DIR%\trace-writer.ps1" -Mode new -TaskLabel "%~3" %INTENT_ARG%
    exit /b !ERRORLEVEL!
)
if /i "%~2"=="append" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness trace append ^<runId^> ^<command-result.json^>
        goto :usage_error
    )
    if "%~4"=="" (
        echo [ERROR] Usage: harness trace append ^<runId^> ^<command-result.json^>
        goto :usage_error
    )
    %PS% "%HARNESS_DIR%\trace-writer.ps1" -Mode append-command -RunId "%~3" -CommandResultPath "%~4"
    exit /b !ERRORLEVEL!
)
if /i "%~2"=="finalize" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness trace finalize ^<runId^> ^<passed^|failed^|partial^>
        goto :usage_error
    )
    if "%~4"=="" (
        echo [ERROR] Usage: harness trace finalize ^<runId^> ^<passed^|failed^|partial^>
        goto :usage_error
    )
    %PS% "%HARNESS_DIR%\trace-writer.ps1" -Mode finalize -RunId "%~3" -FinalStatus "%~4"
    exit /b !ERRORLEVEL!
)
echo [ERROR] Unknown trace subcommand: %~2
echo Usage: harness trace ^<new^|append^|finalize^> ...
exit /b 2

:evals
if /i "%~2"=="--live" (
    %PS% "%HARNESS_DIR%\evals\run-evals.ps1" -IncludeLive
    exit /b !ERRORLEVEL!
)
%PS% "%HARNESS_DIR%\evals\run-evals.ps1"
exit /b %ERRORLEVEL%

:gap
if /i "%~2"=="detect" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness gap detect ^<trace.json^>
        goto :usage_error
    )
    %PS% "%HARNESS_DIR%\detect-command-gap.ps1" -TracePath "%~3"
    exit /b !ERRORLEVEL!
)
if /i "%~2"=="propose" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness gap propose ^<gap-file.json^>
        goto :usage_error
    )
    %PS% "%HARNESS_DIR%\generate-command-proposal.ps1" -GapPath "%~3"
    exit /b !ERRORLEVEL!
)
if /i "%~2"=="validate" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness gap validate ^<proposal.json^>
        goto :usage_error
    )
    %PS% "%HARNESS_DIR%\validate-command-proposal.ps1" -ProposalPath "%~3" -RequireApproved
    exit /b !ERRORLEVEL!
)
if /i "%~2"=="scaffold" (
    if "%~3"=="" (
        echo [ERROR] Usage: harness gap scaffold ^<proposal.json^> [--dry-run]
        goto :usage_error
    )
    set "SCAFFOLD_MODE=-Apply"
    if /i "%~3"=="--dry-run" set "SCAFFOLD_MODE=-DryRun"
    if /i "%~4"=="--dry-run" set "SCAFFOLD_MODE=-DryRun"
    %PS% "%HARNESS_DIR%\scaffold-command.ps1" -ApprovedProposalPath "%~3" %SCAFFOLD_MODE%
    exit /b !ERRORLEVEL!
)
echo [ERROR] Unknown gap subcommand: %~2
echo Usage: harness gap ^<detect^|propose^|validate^|scaffold^> ...
exit /b 2

:usage_error
exit /b 2

:help
echo.
echo   Revit MCP Harness CLI
echo   =====================
echo.
echo   USAGE: .\harness ^<command^> [options]
echo.
echo   COMMANDS:
echo     check                          Check transport ^& command state
echo     registry                       Full command coverage audit
echo     invoke ^<name^> [options]        Call a Revit command safely
echo       --params-file ^<file^>           JSON params file
echo       --params ^<json^>               Inline JSON params
echo       --timeout ^<seconds^>           Timeout in seconds
echo     classify ^<error-file^>           Classify a command error
echo     trace new ^<label^> [--intent ""]  Start a new trace
echo     trace append ^<runId^> ^<file^>     Append command result to trace
echo     trace finalize ^<runId^> ^<status^> Finalize trace (passed/failed/partial)
echo     evals                          Run offline evals
echo     evals --live                   Run all evals including live Revit
echo     gap detect ^<trace.json^>        Detect command gaps from trace
echo     gap propose ^<gap.json^>         Generate command proposal
echo     gap validate ^<proposal.json^>   Validate proposal before scaffold
echo     gap scaffold ^<proposal^> [--dry-run]  Scaffold command (dry-run or apply)
echo     help                           Show this help
echo.
exit /b %HELP_EXIT_CODE%

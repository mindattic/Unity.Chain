@echo off
rem Pulls latest, builds only if the source changed, publishes to C:\Apps\Unity.Chain,
rem refreshes the Desktop shortcut, and launches it (tools\deploy.ps1 does the work).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\deploy.ps1" -Launch %*
if errorlevel 1 (
    echo.
    echo   Deploy failed. See Logs\build-scene.log, build-settings.log or build-player.log.
    pause
    exit /b 1
)
timeout /t 5

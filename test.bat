@echo off
rem Core.Tests (dotnet test), then the EditMode and PlayMode Unity batch-mode suites -- the
rem PlayMode run always includes MainSceneSmokeTests (tools\test.ps1 does the work; the Editor
rem must be closed). TestResults-*.xml and Logs\test-*.log capture each run.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\test.ps1" %*
exit /b %ERRORLEVEL%

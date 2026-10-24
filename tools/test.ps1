<#
.SYNOPSIS
  Headless verification gate for Unity.Chain: the engine-free Core rules (dotnet test, via
  run-core-tests.ps1), the EditMode component tests, and the PlayMode suite -- today just
  MainSceneSmokeTests (Assets/Unity.Chain/Tests/PlayMode) -- each run in Unity batch mode
  (tools/unity.ps1; the Editor must be closed). Mirrors the README's "Build and test" PowerShell
  block; see Unity.SpaceGod's and Unity.GridGame's tools/test.ps1.
.PARAMETER SkipCore
  Skip Tests/Core.Tests.
.PARAMETER SkipEditMode
  Skip the Unity EditMode suite.
.PARAMETER SkipPlayMode
  Skip the Unity PlayMode suite (MainSceneSmokeTests).
.EXAMPLE
  powershell -File tools/test.ps1
  test.bat
  powershell -File tools/test.ps1 -SkipEditMode -SkipPlayMode   # Core.Tests only
#>
param([switch]$SkipCore, [switch]$SkipEditMode, [switch]$SkipPlayMode)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $SkipCore) {
    Write-Host '  Core.Tests (dotnet test)...' -ForegroundColor Cyan
    & (Join-Path $root 'run-core-tests.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SkipEditMode) {
    Write-Host '  EditMode tests (Unity batch mode)...' -ForegroundColor Cyan
    & "$PSScriptRoot/unity.ps1" -Log test-editmode -Extra "-runTests -testPlatform EditMode -testResults TestResults-EditMode.xml"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SkipPlayMode) {
    # The PlayMode assembly holds only MainSceneSmokeTests today; -testPlatform PlayMode with no
    # -assemblyNames filter always includes it, so a future PlayMode addition is covered for free.
    Write-Host '  PlayMode tests (Unity batch mode, includes MainSceneSmokeTests)...' -ForegroundColor Cyan
    & "$PSScriptRoot/unity.ps1" -Log test-playmode -Extra "-runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

exit 0

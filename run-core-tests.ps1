# Runs Core's pure-logic tests outside Unity (no editor needed).
$ErrorActionPreference = 'Stop'
dotnet test (Join-Path $PSScriptRoot 'Tests/Core.Tests') @args
exit $LASTEXITCODE

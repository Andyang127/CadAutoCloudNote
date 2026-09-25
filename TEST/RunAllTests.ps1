# CAD Auto CloudNote - Automated Test Suite Runner
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building and Running CAD Auto CloudNote Test Suite" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$testProjPath = Join-Path $PSScriptRoot "CadAutoCloudNote.Tests.csproj"
$outputExe = Join-Path $PSScriptRoot "..\build\Tests\CadAutoCloudNote.Tests.exe"

Write-Host "`n[1/2] Compiling test project: $testProjPath ..." -ForegroundColor Yellow
dotnet build -c Release "$testProjPath"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[ERROR] Test project build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`n[2/2] Running test runner: $outputExe ..." -ForegroundColor Yellow
& "$outputExe"

$testExitCode = $LASTEXITCODE
if ($testExitCode -eq 0) {
    Write-Host "`n[SUCCESS] All unit tests completed and passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n[FAILURE] Test suite encountered failures, exit code: $testExitCode" -ForegroundColor Red
    exit $testExitCode
}

# Simple Test Runner - AuthClient Tests Only
# This script can run without Visual Studio Developer Command Prompt

Write-Host "=== Running Omni2FA.AuthClient Tests ===" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet is available
$dotnetPath = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetPath) {
    Write-Host "ERROR: .NET SDK not found in PATH" -ForegroundColor Red
    Write-Host "Please install .NET SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Run tests
dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj --logger "console;verbosity=normal"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "? All tests passed!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "? Some tests failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Note: To run Adapter tests, use Visual Studio Test Explorer or RunTests.ps1" -ForegroundColor Yellow

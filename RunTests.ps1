# Run Tests for Omni2FA Projects
# This script runs tests using MSBuild and VSTest

Write-Host "=== Omni2FA Test Runner ===" -ForegroundColor Cyan
Write-Host ""

# Check if running in Developer Command Prompt
$msbuildPath = Get-Command msbuild -ErrorAction SilentlyContinue
if (-not $msbuildPath) {
    Write-Host "ERROR: MSBuild not found in PATH" -ForegroundColor Red
    Write-Host "Please run this script from Developer Command Prompt for Visual Studio" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Run AuthClient tests only with .NET Core CLI:" -ForegroundColor Yellow
    Write-Host "  dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj" -ForegroundColor White
    exit 1
}

Write-Host "Step 1: Building solution..." -ForegroundColor Green
msbuild Omni2FA.sln /t:Rebuild /p:Configuration=Debug /v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Run AuthClient tests with dotnet
Write-Host "Step 2: Running Omni2FA.AuthClient.Tests..." -ForegroundColor Green
dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj --no-build --logger "console;verbosity=normal"
$authClientResult = $LASTEXITCODE
Write-Host ""

# Check for vstest.console.exe
$vstestPath = Get-Command vstest.console.exe -ErrorAction SilentlyContinue
if ($vstestPath) {
    Write-Host "Step 3: Running Omni2FA.Adapter.Tests..." -ForegroundColor Green
    $adapterTestDll = "Omni2FA.Adapter.Tests\bin\Debug\net472\Omni2FA.Adapter.Tests.dll"
    if (Test-Path $adapterTestDll) {
        vstest.console.exe $adapterTestDll
        $adapterResult = $LASTEXITCODE
        Write-Host ""
    } else {
        Write-Host "Warning: Adapter test DLL not found at $adapterTestDll" -ForegroundColor Yellow
        Write-Host "This might be due to COM interop build issues with dotnet CLI." -ForegroundColor Yellow
        $adapterResult = 0 # Don't fail the script
    }
} else {
    Write-Host "Warning: vstest.console.exe not found. Skipping Adapter tests." -ForegroundColor Yellow
    Write-Host "To run Adapter tests, use Visual Studio Test Explorer" -ForegroundColor Yellow
    $adapterResult = 0 # Don't fail the script
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
if ($authClientResult -eq 0) {
    Write-Host "? AuthClient Tests: PASSED" -ForegroundColor Green
} else {
    Write-Host "? AuthClient Tests: FAILED" -ForegroundColor Red
}

if ($vstestPath -and (Test-Path "Omni2FA.Adapter.Tests\bin\Debug\net472\Omni2FA.Adapter.Tests.dll")) {
    if ($adapterResult -eq 0) {
        Write-Host "? Adapter Tests: PASSED" -ForegroundColor Green
    } else {
        Write-Host "? Adapter Tests: FAILED" -ForegroundColor Red
    }
} else {
    Write-Host "? Adapter Tests: NOT RUN (use Visual Studio Test Explorer)" -ForegroundColor Yellow
}

if ($authClientResult -ne 0) {
    exit 1
}

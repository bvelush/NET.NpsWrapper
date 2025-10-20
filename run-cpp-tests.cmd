@echo off
REM Script to build and run C++ unit tests for Omni2FA.NPS.Plugin
REM This script restores NuGet packages, builds the test project, and runs the tests

echo ========================================
echo Omni2FA.NPS.Plugin C++ Test Runner
echo ========================================
echo.

REM Check if the correct NuGet packages are installed
echo [1/4] Checking NuGet packages...
if not exist "packages\Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.1.8.1.7" (
    echo Google Test package not found. Restoring NuGet packages...
    
    REM Check if nuget.exe exists
    where nuget >nul 2>&1
    if errorlevel 1 (
        echo WARNING: nuget.exe not found in PATH.
        echo.
        echo Please install NuGet CLI or restore packages in Visual Studio:
        echo   1. Right-click on Solution in Solution Explorer
        echo   2. Select "Restore NuGet Packages"
        echo.
        pause
        exit /b 1
    )
    
    nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory .
    if errorlevel 1 (
        echo ERROR: Failed to restore NuGet packages.
        echo.
        echo Please try restoring in Visual Studio:
        echo   1. Right-click on Solution
        echo   2. Select "Restore NuGet Packages"
        pause
        exit /b 1
    )
    echo Packages restored successfully.
) else (
    echo Google Test package already installed.
)
echo.

REM Build the test project
echo [2/4] Building test project...
msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal
if errorlevel 1 (
    echo ERROR: Build failed.
    echo.
    echo Common issues:
    echo   - Visual Studio C++ build tools not installed
    echo   - Wrong Visual Studio version (project configured for VS2022)
    echo   - NuGet package references incorrect (run fix-test-project.ps1)
    echo.
    pause
    exit /b 1
)
echo Build completed successfully.
echo.

REM Check if test executable exists
echo [3/4] Locating test executable...
set TEST_EXE=Omni2FA.NPS.Plugin.Tests\x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
if not exist "%TEST_EXE%" (
    echo ERROR: Test executable not found at %TEST_EXE%
    echo Build may have failed silently.
    pause
    exit /b 1
)
echo Test executable found.
echo.

REM Run the tests
echo [4/4] Running C++ unit tests...
echo ========================================
"%TEST_EXE%" --gtest_color=yes
set TEST_RESULT=%errorlevel%
echo ========================================
echo.

if %TEST_RESULT% equ 0 (
    echo All tests passed!
) else (
    echo Some tests failed. Exit code: %TEST_RESULT%
)
echo.

pause
exit /b %TEST_RESULT%

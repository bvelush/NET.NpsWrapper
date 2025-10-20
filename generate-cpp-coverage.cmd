@echo off
REM Script to generate C++ code coverage for Omni2FA.NPS.Plugin.Tests
REM Uses OpenCppCoverage to generate HTML coverage reports

echo ========================================
echo C++ Code Coverage Report Generator
echo ========================================
echo.

REM Check if OpenCppCoverage is installed
where OpenCppCoverage >nul 2>&1
if errorlevel 1 (
    echo ERROR: OpenCppCoverage is not installed or not in PATH.
    echo.
    echo Please install it using one of these methods:
    echo   1. Chocolatey: choco install opencppcoverage
    echo   2. Download from: https://github.com/OpenCppCoverage/OpenCppCoverage/releases
    echo.
    pause
    exit /b 1
)

echo [1/4] Cleaning previous coverage results...
if exist CppCoverageReport rmdir /s /q CppCoverageReport
echo Done.
echo.

echo [2/4] Building C++ test project...
msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo
if errorlevel 1 (
    echo ERROR: Build failed.
    echo Please ensure Visual Studio C++ build tools are installed.
    pause
    exit /b 1
)
echo Done.
echo.

REM Check if test executable exists
set TEST_EXE=x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
if not exist "%TEST_EXE%" (
    echo ERROR: Test executable not found at %TEST_EXE%
    echo Please check the build output above.
    pause
    exit /b 1
)

echo [3/4] Running tests with coverage collection...
echo This may take a moment...
echo.

REM Run OpenCppCoverage
REM --sources: Only analyze our source code (not system headers or test code)
REM --modules: Only instrument our DLL/EXE
REM --export_type html: Generate HTML report
OpenCppCoverage ^
    --sources "Omni2FA.NPS.Plugin\*" ^
    --excluded_sources "Omni2FA.NPS.Plugin.Tests\*" ^
    --excluded_sources "*\packages\*" ^
    --excluded_sources "*\x64\*" ^
    --modules "Omni2FA.NPS.Plugin" ^
    --export_type html:CppCoverageReport ^
    --export_type cobertura:CppCoverageReport\coverage.xml ^
    --cover_children ^
    -- "%TEST_EXE%"

if errorlevel 1 (
    echo.
    echo ERROR: Coverage collection failed.
    echo Please check that tests are passing: run-cpp-tests.cmd
    pause
    exit /b 1
)

echo.
echo [4/4] Coverage report generated successfully!
echo.
echo ========================================
echo Coverage Report Location
echo ========================================
echo HTML Report: %CD%\CppCoverageReport\index.html
echo XML Report:  %CD%\CppCoverageReport\coverage.xml
echo ========================================
echo.

REM Open the HTML report in browser
echo Opening report in browser...
start "" "CppCoverageReport\index.html"

pause

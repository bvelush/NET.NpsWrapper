@echo off
REM Script to generate test coverage reports for Omni2FA projects
REM This script runs tests with code coverage and generates an HTML report

echo ========================================
echo Omni2FA Test Coverage Report Generator
echo ========================================
echo.

REM Clean previous results
echo [1/4] Cleaning previous coverage results...
if exist TestResults rmdir /s /q TestResults
if exist CoverageReport rmdir /s /q CoverageReport
echo Done.
echo.

REM Run tests with code coverage (only .NET projects)
echo [2/4] Running .NET tests with code coverage collection...
dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj --collect:"XPlat Code Coverage" --results-directory:./TestResults
if errorlevel 1 (
    echo ERROR: Tests failed or coverage collection failed.
    pause
    exit /b 1
)
echo Done.
echo.

REM Check if ReportGenerator is installed
echo [3/4] Checking for ReportGenerator tool...
dotnet tool list --global | findstr /C:"dotnet-reportgenerator-globaltool" >nul
if errorlevel 1 (
    echo ReportGenerator not found. Installing...
    dotnet tool install --global dotnet-reportgenerator-globaltool
    if errorlevel 1 (
        echo ERROR: Failed to install ReportGenerator.
        pause
        exit /b 1
    )
    echo Installed successfully.
) else (
    echo ReportGenerator already installed.
)
echo.

REM Generate HTML report
echo [4/4] Generating HTML coverage report...
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" -targetdir:"./CoverageReport" -reporttypes:Html
if errorlevel 1 (
    echo ERROR: Failed to generate HTML report.
    pause
    exit /b 1
)
echo Done.
echo.

echo ========================================
echo Coverage report generated successfully!
echo ========================================
echo.
echo Report location: %CD%\CoverageReport\index.html
echo.

REM Open the report in default browser
echo Opening report in browser...
start "" "CoverageReport\index.html"

pause

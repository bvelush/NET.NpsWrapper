@echo off
REM Master script to run all tests (both .NET and C++ tests)
REM This provides a unified test execution experience

echo ========================================
echo Omni2FA Complete Test Suite Runner
echo ========================================
echo.
echo This script will run:
echo   1. .NET tests with coverage (Omni2FA.AuthClient.Tests)
echo   2. C++ tests with coverage (Omni2FA.NPS.Plugin.Tests)
echo.

set OVERALL_RESULT=0

REM ============================================================================
REM .NET Tests
REM ============================================================================
echo ========================================
echo [1/2] .NET Unit Tests with Coverage
echo ========================================
echo.
call generate-coverage-report.cmd
if errorlevel 1 (
    echo.
    echo WARNING: .NET tests failed or had errors.
    set OVERALL_RESULT=1
) else (
    echo.
    echo ? .NET tests completed successfully.
)
echo.
echo.

REM ============================================================================
REM C++ Tests
REM ============================================================================
echo ========================================
echo [2/2] C++ Unit Tests with Coverage
echo ========================================
echo.

REM Check if OpenCppCoverage is available
where OpenCppCoverage >nul 2>&1
if errorlevel 1 (
    echo WARNING: OpenCppCoverage not found. Running tests without coverage...
    echo To enable C++ coverage, install: choco install opencppcoverage
    echo.
    call run-cpp-tests.cmd
) else (
    call generate-cpp-coverage.cmd
)

if errorlevel 1 (
    echo.
    echo WARNING: C++ tests failed or had errors.
    set OVERALL_RESULT=1
) else (
    echo.
    echo ? C++ tests completed successfully.
)
echo.
echo.

REM ============================================================================
REM Summary
REM ============================================================================
echo ========================================
echo Test Suite Summary
echo ========================================
if %OVERALL_RESULT% equ 0 (
    echo Status: ? ALL TESTS PASSED
    echo.
    echo Coverage Reports:
    echo   .NET:  %CD%\CoverageReport\index.html
    
    where OpenCppCoverage >nul 2>&1
    if not errorlevel 1 (
        echo   C++:   %CD%\CppCoverageReport\index.html
    ) else (
        echo   C++:   ^(coverage not available - install OpenCppCoverage^)
    )
) else (
    echo Status: ? SOME TESTS FAILED
    echo Please review the output above for details.
)
echo ========================================
echo.

pause
exit /b %OVERALL_RESULT%

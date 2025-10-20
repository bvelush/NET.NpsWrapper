# Testing Quick Start Guide

## Visual Studio Test Explorer Integration

### Setup (One-Time)
The **Test Adapter for Google Test** is built into Visual Studio 2022, so no additional installation is needed!

### Run All Tests in Visual Studio
1. **Restore NuGet Packages** (first time only):
   - Right-click on Solution ? **Restore NuGet Packages**
   
2. **Build the solution** (Ctrl+Shift+B)

3. **Open Test Explorer** (Ctrl+E, T or Test ? Test Explorer)

4. **Click "Run All"** to execute both C++ and .NET tests

5. View results, filter by outcome, debug failing tests

### Benefits
? Unified test experience (C++ and .NET together)  
? Run/debug individual tests  
? See test output and assertions  
? Filter and group tests  
? Code coverage for .NET tests  

---

## For C++ Tests (NPS.Plugin)

### First Time Setup
```cmd
# Restore Google Test package (run from solution root)
nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory .
```

**OR** use Visual Studio:
- Right-click on Solution ? **Restore NuGet Packages**

### Run Tests
```cmd
# Quick run - builds and executes C++ tests
run-cpp-tests.cmd
```

### Generate C++ Code Coverage

#### Option 1: OpenCppCoverage (Free, Recommended)

**Install OpenCppCoverage:**
```cmd
# Using Chocolatey
choco install opencppcoverage

# Or download from: https://github.com/OpenCppCoverage/OpenCppCoverage/releases
```

**Generate Coverage Report:**
```cmd
# Automated script - builds, runs tests, generates HTML report
generate-cpp-coverage.cmd
```

**Manual Command:**
```cmd
OpenCppCoverage --sources "Omni2FA.NPS.Plugin\*" --modules "Omni2FA.NPS.Plugin" --export_type html:CppCoverageReport -- x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
```

**Output:**
- HTML Report: `CppCoverageReport\index.html`
- XML Report: `CppCoverageReport\coverage.xml` (Cobertura format)

#### Option 2: Visual Studio Enterprise

If you have **Visual Studio Enterprise** edition:
1. Test ? Analyze Code Coverage ? All Tests
2. View coverage in Code Coverage Results window
3. See line-by-line highlighting in editor

**Note:** Code coverage is only available in Enterprise edition, not Community/Professional.

**Important Note:** The C++ test project has been updated to:
- ? Use the correct Microsoft Google Test package (v1.8.1.7)
- ? Target Visual Studio 2022 (v143 toolset)
- ? Fix all compilation errors related to const-correctness and RADIUS attribute structures
- ? Match exact memory layout of Windows RADIUS_ATTRIBUTE_ARRAY structure

If you encounter issues, see the Troubleshooting section below or consult `FIX-NUGET-ERROR.md`.

## For .NET Tests (AuthClient)

### Run Tests with Coverage
```cmd
# Generates coverage report and opens in browser
generate-coverage-report.cmd
```

## Run Everything
```cmd
# Runs both C++ and .NET test suites with coverage (if OpenCppCoverage installed)
run-all-tests.cmd
```

## Manual Commands

### C++ Tests
```cmd
# Build
msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64

# Run
Omni2FA.NPS.Plugin.Tests\x64\Debug\Omni2FA.NPS.Plugin.Tests.exe

# Run with coverage
generate-cpp-coverage.cmd
```

### .NET Tests
```cmd
# Build
dotnet build Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj

# Run with coverage
dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj --collect:"XPlat Code Coverage"
```

## Test Results Location

- **C++ Test Output**: Console output with pass/fail for each test
- **C++ Coverage Report**: `CppCoverageReport\index.html` (if OpenCppCoverage installed)
- **C++ Coverage XML**: `CppCoverageReport\coverage.xml` (Cobertura format)
- **.NET Coverage Report**: `CoverageReport\index.html`
- **.NET Coverage XML**: `TestResults\*\coverage.cobertura.xml`

## What Gets Tested?

### C++ (Omni2FA.NPS.Plugin)
? RadiusAlloc/RadiusFree memory functions  
? RadiusFindFirstIndex attribute searching  
? RadiusFindFirstAttribute attribute retrieval  
? RadiusReplaceFirstAttribute attribute manipulation  
? 22 comprehensive test cases covering edge cases, null handling, and data integrity  
? **Coverage tracking available with OpenCppCoverage**

### .NET (Omni2FA.AuthClient)
? Authentication client functionality  
? Integration tests  
? Full code coverage reporting

## Troubleshooting

### "NuGet package(s) are missing" Error
**This issue should now be resolved.** If you still see this error:

1. Close Visual Studio
2. Run the fix script:
```powershell
powershell -ExecutionPolicy Bypass -File fix-test-project.ps1
```
3. Restore NuGet packages:
```cmd
nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory .
```
4. Reopen Visual Studio and build

### Compilation Errors in RadUtilTests.cpp
**These have been fixed.** The test file now properly handles:
- Const-correctness for `RADIUS_ATTRIBUTE` structures
- Proper function pointer signatures matching Windows authif.h
- Writable test buffers using `TestRadiusAttribute` helper structure
- Exact memory layout matching (cbSize as first member)

### C++ Coverage Not Working
If `generate-cpp-coverage.cmd` fails:

1. **Check OpenCppCoverage installation:**
```cmd
where OpenCppCoverage
```

2. **Install if missing:**
```cmd
choco install opencppcoverage
```

3. **Ensure test executable exists:**
   - Build project first: `msbuild Omni2FA.NPS.Plugin.Tests\... /p:Configuration=Debug`

4. **Run tests without coverage first:**
```cmd
run-cpp-tests.cmd
```

### Visual Studio Version
The test project is configured for **Visual Studio 2022 (v143 toolset)**.

If you're using VS2019, you may need to change the toolset:
1. Right-click project ? Properties
2. Configuration Properties ? General
3. Set Platform Toolset to v142

## CI/CD Integration

The test scripts return proper exit codes:
- `0` = All tests passed
- `1` = Tests failed

Perfect for build pipelines!

### Example CI/CD Commands
```yaml
# .NET Tests with Coverage
- run: dotnet test --collect:"XPlat Code Coverage"

# C++ Tests (if OpenCppCoverage available)
- run: generate-cpp-coverage.cmd

# Or without coverage
- run: run-cpp-tests.cmd
```

## Test Coverage Details

### C++ Coverage Metrics
With OpenCppCoverage, you'll see:
- **Line Coverage**: Percentage of code lines executed
- **Function Coverage**: Percentage of functions called
- **Branch Coverage**: Percentage of conditional branches taken
- **Color-coded source view**: Green (covered), Red (not covered)

### .NET Coverage Metrics
With Coverlet, you'll see:
- Line coverage percentage
- Branch coverage percentage
- Method coverage
- Class coverage
- Detailed HTML reports with drill-down capability

The C++ tests include comprehensive coverage of all edge cases:
- **Null pointer handling**: Ensures functions gracefully handle NULL inputs
- **Empty array operations**: Tests behavior with empty RADIUS attribute arrays
- **Boundary conditions**: Tests attributes at beginning, middle, and end of arrays
- **Duplicate handling**: Verifies correct behavior when duplicate attribute types exist
- **Data integrity**: Confirms attribute data is correctly stored and retrieved
- **Memory management**: Tests allocation, deallocation, and large block handling

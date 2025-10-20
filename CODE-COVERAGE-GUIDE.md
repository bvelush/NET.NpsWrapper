# Code Coverage Guide for Omni2FA

Complete guide for generating and viewing code coverage reports for both C++ and .NET projects.

---

## Quick Start

### All Tests with Coverage (Recommended)
```cmd
run-all-tests.cmd
```
This runs both .NET and C++ tests with coverage and opens both HTML reports.

**Note:** C++ projects require **MSBuild** (Visual Studio), while .NET projects use the dotnet CLI.

---

## C++ Code Coverage (Omni2FA.NPS.Plugin)

### Prerequisites

**1. Visual Studio Build Tools**
C++ projects require MSBuild (part of Visual Studio):
- Visual Studio 2022 (Community, Professional, or Enterprise)
- Or Visual Studio Build Tools 2022

**2. OpenCppCoverage** (for coverage reports):

```cmd
# Option 1: Using Chocolatey (recommended)
choco install opencppcoverage

# Option 2: Manual Download
# Visit: https://github.com/OpenCppCoverage/OpenCppCoverage/releases
# Download and run the installer
```

### Generate C++ Coverage

**Automated (Recommended):**
```cmd
generate-cpp-coverage.cmd
```

This script will:
1. ? Build the C++ test project using **MSBuild**
2. ? Run all 22 C++ tests
3. ? Collect coverage data with OpenCppCoverage
4. ? Generate HTML and XML reports
5. ? Open the report in your browser

**Manual:**
```cmd
# Step 1: Build tests using MSBuild (not dotnet build!)
msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64

# Step 2: Run with coverage
OpenCppCoverage ^
    --sources "Omni2FA.NPS.Plugin\*" ^
    --excluded_sources "Omni2FA.NPS.Plugin.Tests\*" ^
    --modules "Omni2FA.NPS.Plugin" ^
    --export_type html:CppCoverageReport ^
    -- x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
```

**Important:** C++ projects **cannot** be built with `dotnet build` or `dotnet test`. They require MSBuild.

### C++ Coverage Output

- **HTML Report**: `CppCoverageReport\index.html` - Interactive, color-coded view
- **XML Report**: `CppCoverageReport\coverage.xml` - Cobertura format for CI/CD

### What You'll See

```
Coverage Report
===============
Lines Covered:    45/50  (90.0%)
Functions Covered: 4/5   (80.0%)
Branches Covered: 12/15  (80.0%)

Source Files:
?? radutil.cpp         95% coverage
?? other files...
```

Click on any file to see:
- ? **Green lines**: Code that was executed
- ? **Red lines**: Code that was NOT executed
- ? **Gray lines**: Non-executable (comments, declarations)

---

## .NET Code Coverage (Omni2FA.AuthClient)

### Prerequisites

- .NET SDK (comes with Visual Studio or standalone)
- ReportGenerator (auto-installed by script)

### Generate .NET Coverage

**Automated (Recommended):**
```cmd
generate-coverage-report.cmd
```

This script will:
1. ? Build the .NET test project using **dotnet CLI**
2. ? Run all .NET tests
3. ? Collect coverage data with Coverlet
4. ? Generate HTML report with ReportGenerator
5. ? Open the report in your browser

**Manual:**
```cmd
# Clean previous results
rmdir /s /q TestResults CoverageReport

# Run tests with coverage (only .NET projects)
dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj --collect:"XPlat Code Coverage" --results-directory:./TestResults

# Generate HTML report
reportgenerator ^
    -reports:"./TestResults/**/coverage.cobertura.xml" ^
    -targetdir:"./CoverageReport" ^
    -reporttypes:Html
```

### .NET Coverage Output

- **HTML Report**: `CoverageReport\index.html` - Detailed, drill-down view
- **XML Report**: `TestResults\*\coverage.cobertura.xml` - Cobertura format

### What You'll See

```
Summary
=======
Line Coverage:    85.5%
Branch Coverage:  78.3%
Method Coverage:  90.2%

Assemblies:
?? Omni2FA.AuthClient    87% coverage
?  ?? AuthenticatorClient
?  ?? Other classes...
?? Omni2FA.Adapter       82% coverage
```

---

## Understanding Build Systems

### Why Two Different Build Systems?

| Project Type | Build Tool | Test Tool | Coverage Tool |
|-------------|------------|-----------|---------------|
| **C++** | MSBuild | Google Test | OpenCppCoverage |
| **.NET** | dotnet CLI | MSTest | Coverlet |

**Key Point:** You **cannot** use `dotnet build` or `dotnet test` on C++ projects. They require MSBuild.

### Common Errors

? **Error:** "The imported file '$(VCTargetsPath)\Microsoft.Cpp.Default.props' does not exist"
- **Cause:** Trying to build C++ project with `dotnet build`
- **Solution:** Use `msbuild` instead, or use the provided scripts

? **Correct approach:**
```cmd
# For C++ - use MSBuild
msbuild Omni2FA.NPS.Plugin.Tests\... /p:Configuration=Debug /p:Platform=x64

# For .NET - use dotnet CLI  
dotnet test Omni2FA.AuthClient.Tests\...
```

---

## Visual Studio Enterprise (Alternative)

If you have **VS Enterprise**, you can use built-in coverage:

### For C++ and .NET:
1. **Test ? Analyze Code Coverage ? All Tests**
2. View results in **Code Coverage Results** window
3. See line-by-line highlighting in the editor

?? **Note:** This feature requires **Enterprise** edition (not Community/Professional)

---

## Comparing Coverage Tools

| Feature | OpenCppCoverage (C++) | Coverlet (.NET) | VS Enterprise |
|---------|----------------------|-----------------|---------------|
| **Cost** | Free | Free | Paid (Enterprise) |
| **Languages** | C/C++ | .NET | C++, .NET, more |
| **HTML Reports** | ? Yes | ? Yes | ? Yes |
| **XML Export** | ? Cobertura | ? Cobertura | ? Multiple formats |
| **IDE Integration** | ? No | ?? Limited | ? Full integration |
| **CI/CD Friendly** | ? Yes | ? Yes | ? Yes |
| **Branch Coverage** | ? Yes | ? Yes | ? Yes |
| **Build Tool** | MSBuild | dotnet CLI | Both |

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Tests with Coverage

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v2
    
    # Install OpenCppCoverage
    - name: Install OpenCppCoverage
      run: choco install opencppcoverage
    
    # Restore NuGet packages
    - name: Restore packages
      run: nuget restore
    
    # Build C++ project with MSBuild
    - name: Build C++ Tests
      run: msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64
    
    # Build .NET project with dotnet
    - name: Build .NET Tests
      run: dotnet build Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj
    
    # Run .NET tests with coverage
    - name: .NET Tests
      run: dotnet test Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj --collect:"XPlat Code Coverage" --results-directory:./TestResults --no-build
    
    # Run C++ tests with coverage
    - name: C++ Tests
      run: |
        OpenCppCoverage --sources "Omni2FA.NPS.Plugin\*" --modules "Omni2FA.NPS.Plugin" --export_type cobertura:cpp-coverage.xml -- x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
    
    # Upload coverage reports
    - name: Upload coverage to Codecov
      uses: codecov/codecov-action@v3
      with:
        files: ./TestResults/**/coverage.cobertura.xml,./cpp-coverage.xml
```

### Azure DevOps Example

```yaml
steps:
- task: NuGetCommand@2
  inputs:
    command: 'restore'

# Build C++ with MSBuild
- task: VSBuild@1
  inputs:
    solution: 'Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj'
    platform: 'x64'
    configuration: 'Debug'

# Build .NET with dotnet
- task: DotNetCoreCLI@2
  inputs:
    command: 'build'
    projects: 'Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj'

- script: |
    choco install opencppcoverage
  displayName: 'Install OpenCppCoverage'

- script: |
    generate-cpp-coverage.cmd
  displayName: 'C++ Tests with Coverage'

- script: |
    generate-coverage-report.cmd
  displayName: '.NET Tests with Coverage'

- task: PublishCodeCoverageResults@1
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: '**/coverage.cobertura.xml,**/coverage.xml'
```

---

## Understanding Coverage Metrics

### Line Coverage
**What it means:** Percentage of code lines that were executed during tests.

```cpp
void MyFunction() {
    int x = 5;        // ? Covered (executed)
    if (x > 10) {     // ? Covered
        DoSomething(); // ? Not covered (condition never true)
    }
    return;           // ? Covered
}
```
**Coverage:** 75% (3/4 lines)

### Branch Coverage
**What it means:** Percentage of decision branches (if/else, switch) that were tested.

```cpp
if (condition) {      // Branch point
    // Path A         // ? Tested
} else {
    // Path B         // ? Not tested
}
```
**Branch Coverage:** 50% (1/2 branches)

### Function Coverage
**What it means:** Percentage of functions that were called during tests.

```cpp
void FunctionA() { } // ? Called in tests
void FunctionB() { } // ? Never called
```
**Function Coverage:** 50% (1/2 functions)

---

## Best Practices

### Coverage Goals
- **Aim for 80%+ line coverage** - Good balance of effort vs. value
- **Target 70%+ branch coverage** - Ensures edge cases are tested
- **100% coverage is not always necessary** - Focus on critical paths

### What to Prioritize
1. ? **Business logic** - Core functionality
2. ? **Error handling** - Exception paths
3. ? **Edge cases** - Boundary conditions
4. ?? **Simple getters/setters** - Lower priority
5. ? **Generated code** - Usually excluded

### Improving Coverage

**1. Find uncovered code:**
   - Open HTML report
   - Look for red (uncovered) lines
   - Identify why they're not covered

**2. Add targeted tests:**
```cpp
TEST(MyTest, EdgeCase) {
    // Test the specific uncovered scenario
}
```

**3. Remove dead code:**
   - If code can't be reached, remove it
   - Or add comments explaining why

---

## Troubleshooting

### "The imported file '$(VCTargetsPath)\Microsoft.Cpp.Default.props' does not exist"
**Cause:** Trying to build C++ project with `dotnet build` instead of MSBuild.

**Solution:**
```cmd
# ? Wrong - don't use dotnet for C++
dotnet build

# ? Correct - use MSBuild for C++
msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64

# ? Or use the provided script
generate-cpp-coverage.cmd
```

### "OpenCppCoverage not found"
```cmd
# Check installation
where OpenCppCoverage

# If not found, install:
choco install opencppcoverage

# Add to PATH if needed
set PATH=%PATH%;C:\Program Files\OpenCppCoverage
```

### "No coverage data generated"
- Ensure tests are actually running: `run-cpp-tests.cmd`
- Check that executable path is correct
- Verify `--sources` and `--modules` filters match your code

### "Coverage too low"
- Add more test cases for uncovered scenarios
- Check if there's unreachable/dead code
- Review error handling paths

### "Build failed"
- **For C++:** Ensure Visual Studio C++ build tools are installed
- **For .NET:** Ensure .NET SDK is installed
- Check that all NuGet packages are restored
- Verify project builds successfully first

---

## Additional Resources

### Tools
- **OpenCppCoverage**: https://github.com/OpenCppCoverage/OpenCppCoverage
- **ReportGenerator**: https://github.com/danielpalme/ReportGenerator
- **Coverlet**: https://github.com/coverlet-coverage/coverlet
- **MSBuild**: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild

### Documentation
- **Google Test**: https://google.github.io/googletest/
- **MSTest**: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest
- **Code Coverage Best Practices**: https://martinfowler.com/bliki/TestCoverage.html
- **C++ in Visual Studio**: https://learn.microsoft.com/en-us/cpp/

---

## Summary

| Task | Command | Build Tool | Output |
|------|---------|------------|--------|
| **Run all tests with coverage** | `run-all-tests.cmd` | MSBuild + dotnet | Both HTML reports |
| **C++ coverage only** | `generate-cpp-coverage.cmd` | MSBuild | `CppCoverageReport\index.html` |
| **.NET coverage only** | `generate-coverage-report.cmd` | dotnet CLI | `CoverageReport\index.html` |
| **C++ tests only** | `run-cpp-tests.cmd` | MSBuild | Console output |
| **.NET tests only** | `dotnet test Omni2FA.AuthClient.Tests\...` | dotnet CLI | Console output |

**Key Takeaway:** C++ projects need MSBuild, .NET projects use dotnet CLI. The scripts handle this automatically!

---

**Coverage is a tool, not a goal. Focus on meaningful tests, not just numbers!** ???

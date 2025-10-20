# Omni2FA.NPS.Plugin Tests

This directory contains comprehensive unit tests for the Omni2FA NPS Plugin C++ components.

## Overview

The test suite uses **Google Test** framework to provide thorough coverage of the native C++ code, specifically focusing on the RADIUS utility functions.

## Test Coverage

### RadUtil Functions
The test suite covers all functions in `radutil.cpp`:

- **RadiusAlloc / RadiusFree**: Memory allocation and deallocation tests
  - Normal allocation
  - Zero-byte allocation
  - Large block allocation
  - Null pointer handling

- **RadiusFindFirstIndex**: Attribute search by index
  - Null array handling
  - Empty array handling
  - Finding attributes at various positions
  - Non-existent attribute handling
  - Duplicate attribute handling

- **RadiusFindFirstAttribute**: Attribute search by pointer
  - Null and empty array handling
  - Finding attributes with data
  - Verifying attribute data integrity

- **RadiusReplaceFirstAttribute**: Attribute replacement/addition
  - Null parameter validation
  - Adding new attributes
  - Replacing existing attributes
  - Handling duplicates
  - Appending to array

## Project Structure

```
Omni2FA.NPS.Plugin.Tests/
??? Omni2FA.NPS.Plugin.Tests.vcxproj   # Visual Studio C++ test project
??? packages.config                     # NuGet package configuration (Google Test)
??? RadUtilTests.cpp                    # Comprehensive tests for radutil functions
??? README.md                           # This file
```

## Prerequisites

- Visual Studio 2019 or later with C++ development tools
- MSBuild (comes with Visual Studio)
- NuGet CLI (for package restoration)
- Google Test 1.15.2 (automatically installed via NuGet)

## Building the Tests

### Using MSBuild (Command Line)
```cmd
msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Debug /p:Platform=x64
```

### Using Visual Studio
1. Open the solution in Visual Studio
2. Build the `Omni2FA.NPS.Plugin.Tests` project
3. Tests will be compiled to `Omni2FA.NPS.Plugin.Tests\x64\Debug\Omni2FA.NPS.Plugin.Tests.exe`

## Running the Tests

### Option 1: Automated Script (Recommended)
```cmd
run-cpp-tests.cmd
```
This script will:
- Restore NuGet packages if needed
- Build the test project
- Run all tests with colored output
- Display results

### Option 2: Run All Tests (C++ and .NET)
```cmd
run-all-tests.cmd
```
This master script runs both C++ and .NET test suites and provides a comprehensive summary.

### Option 3: Manual Execution
```cmd
Omni2FA.NPS.Plugin.Tests\x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
```

### Option 4: Visual Studio Test Explorer
The tests should appear in Visual Studio's Test Explorer and can be run from there.

## Test Output

Tests use Google Test's standard output format:
```
[==========] Running 25 tests from 5 test suites.
[----------] Global test environment set-up.
[----------] 4 tests from RadiusAllocTest
[ RUN      ] RadiusAllocTest.AllocatesMemorySuccessfully
[       OK ] RadiusAllocTest.AllocatesMemorySuccessfully (0 ms)
...
[==========] 25 tests from 5 test suites ran. (15 ms total)
[  PASSED  ] 25 tests.
```

## Google Test Features Used

- **Test Fixtures**: `RadUtilTest` class provides setup/teardown for RADIUS array tests
- **Mock Objects**: `MockRadiusAttributeArray` simulates RADIUS attribute arrays
- **Assertions**: ASSERT_* and EXPECT_* macros for validation
- **Parameterized Tests**: Ready to extend with TEST_P if needed

## Extending the Tests

### Adding New Test Cases
Add new tests in `RadUtilTests.cpp` using Google Test macros:

```cpp
TEST_F(RadUtilTest, YourNewTestName) {
    // Arrange
    AddAttribute(1, nullptr, 0);
    
    // Act
    DWORD result = RadiusFindFirstIndex(radiusArray, 1);
    
    // Assert
    EXPECT_EQ(result, 0);
}
```

### Adding Tests for New Functions
1. Add the source file to the `<ClCompile>` section in the `.vcxproj`
2. Create corresponding test cases in a new `.cpp` file
3. Include necessary headers

## Troubleshooting

### Build Errors
- Ensure Visual Studio C++ build tools are installed
- Check that Google Test package is restored: `nuget restore`
- Verify C++14 standard is supported by your toolset

### NuGet Package Issues
If Google Test doesn't restore automatically:
```cmd
nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory .
```

### Test Failures
- Review the test output for specific assertion failures
- Check that the source code in `Omni2FA.NPS.Plugin` matches expectations
- Enable verbose output: `Omni2FA.NPS.Plugin.Tests.exe --gtest_verbose`

## Continuous Integration

To integrate these tests into CI/CD:

```yaml
# Example for CI pipeline
- name: Restore NuGet packages
  run: nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory .

- name: Build C++ tests
  run: msbuild Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj /p:Configuration=Release /p:Platform=x64

- name: Run C++ tests
  run: Omni2FA.NPS.Plugin.Tests\x64\Release\Omni2FA.NPS.Plugin.Tests.exe --gtest_output=xml:cpp-test-results.xml
```

## License

GNU General Public License version 2.1 (GPLv2.1)

## Contributing

When adding new C++ code to the plugin, please:
1. Write corresponding unit tests
2. Ensure all existing tests pass
3. Aim for >80% code coverage
4. Document complex test scenarios

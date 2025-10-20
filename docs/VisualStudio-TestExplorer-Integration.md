# Integrating C++ Tests with Visual Studio Test Explorer

## Prerequisites

Install the **Test Adapter for Google Test** extension:

1. In Visual Studio, go to **Extensions ? Manage Extensions**
2. Search for "Test Adapter for Google Test"
3. Install the extension by **Google** (or the community version)
4. Restart Visual Studio

Alternatively, install via command line:
```cmd
# Visual Studio 2019
vsixinstaller.exe /q GoogleTestAdapter.VS2019.vsix

# Visual Studio 2022
vsixinstaller.exe /q GoogleTestAdapter.VS2022.vsix
```

## Configuration

After installation, your C++ Google Test tests will automatically appear in Visual Studio's Test Explorer alongside your .NET tests!

## Usage

1. **Build the test project** (Omni2FA.NPS.Plugin.Tests)
2. Open **Test ? Test Explorer** (or press Ctrl+E, T)
3. Your C++ tests will appear in the test list
4. Run tests individually or all at once
5. View results, stack traces, and failure details

## Benefits

? Unified test experience (C++ and .NET in same window)  
? Run/debug tests from Test Explorer  
? Filter and group tests  
? See test output and assertions  
? CI/CD integration via `vstest.console.exe`  

## Alternative: Using vstest.console.exe

If you prefer command-line or CI/CD without the extension:

```cmd
vstest.console.exe Omni2FA.NPS.Plugin.Tests\x64\Debug\Omni2FA.NPS.Plugin.Tests.exe
```

This works with the Google Test adapter and generates standard test result files.

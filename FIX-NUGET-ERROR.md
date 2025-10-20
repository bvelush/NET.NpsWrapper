# Fix for NuGet Package Reference Error

## Problem
You're seeing this error:
```
This project references NuGet package(s) that are missing on this computer. 
The missing file is ..\packages\googletest.1.15.2\build\native\googletest.targets.
```

## Root Cause
The test project was initially created with a non-existent Google Test package version (1.15.2). 
It needs to be updated to use the correct Microsoft Google Test package.

## Solutions

### **Solution 1: Automated Fix (Easiest)**

1. **Close Visual Studio completely**

2. **Run the PowerShell fix script:**
```powershell
powershell -ExecutionPolicy Bypass -File fix-test-project.ps1
```

3. **Restore NuGet packages:**
```cmd
nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory .
```
   
   **OR** if you don't have nuget.exe in PATH, open Visual Studio and:
   - Right-click on Solution ? **Restore NuGet Packages**

4. **Open Visual Studio and build the solution**

---

### **Solution 2: Manual Fix in Visual Studio**

1. **In Visual Studio Solution Explorer:**
   - Right-click on `Omni2FA.NPS.Plugin.Tests` project
   - Select **"Unload Project"**

2. **Edit the project file:**
   - Right-click on the unloaded project
   - Select **"Edit Project File"**

3. **Use Find & Replace (Ctrl+H):**

   **Replace #1 - Update Package Name:**
   - Find: `googletest.1.15.2`
   - Replace: `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.1.8.1.7`
   - Click **"Replace All"**

   **Replace #2 - Update Target File:**
   - Find: `googletest.targets`
   - Replace: `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets`
   - Click **"Replace All"**

   **Replace #3 - Update to Visual Studio 2022:**
   - Find: `<PlatformToolset>v142</PlatformToolset>`
   - Replace: `<PlatformToolset>v143</PlatformToolset>`
   - Click **"Replace All"**

   **Replace #4 - Remove Old Include Path:**
   - Find and **DELETE** all occurrences of:
   ```
   $(SolutionDir)packages\googletest.1.15.2\build\native\include;
   ```

4. **Save the file** (Ctrl+S)

5. **Reload the project:**
   - Right-click on the unloaded project
   - Select **"Reload Project"**

6. **Restore NuGet packages:**
   - Right-click on the Solution
   - Select **"Restore NuGet Packages"**

7. **Build the solution**

---

## Verification

After applying the fix, you should see:

? No more errors about missing googletest.1.15.2  
? Package `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.1.8.1.7` in `packages\` folder  
? Project builds successfully  
? Tests appear in Visual Studio Test Explorer  

## Updated Files

The fix updates these references in `Omni2FA.NPS.Plugin.Tests.vcxproj`:

| What | Old Value | New Value |
|------|-----------|-----------|
| Package Name | googletest.1.15.2 | Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.1.8.1.7 |
| Target File | googletest.targets | Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets |
| Platform Toolset | v142 (VS2019) | v143 (VS2022) |
| Include Directory | (removed old path) | Uses package-provided paths |

## Why This Happened

The test project was initially generated with a package reference to `googletest` version `1.15.2`, 
which doesn't exist in the official NuGet repository. The correct package for C++ Google Test on 
Windows is `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn`.

Additionally, the project was configured for Visual Studio 2019 (v142 toolset) but needs VS2022 (v143).

## Need Help?

If the automated or manual fixes don't work:

1. Check that you have Visual Studio 2022 installed with C++ development tools
2. Ensure NuGet Package Manager is working (check Tools ? NuGet Package Manager)
3. Try cleaning the solution and rebuilding
4. Delete the `packages\` folder and restore again

For more details, see: `TESTING.md`

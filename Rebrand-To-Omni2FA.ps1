# Omni2FA Rebranding Script
# This script renames the NET.NpsWrapper solution to Omni2FA
# 
# IMPORTANT: Run this script from the solution root directory
# BACKUP: Make sure you have committed all changes to Git before running

param(
    [Parameter(Mandatory=$false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Omni2FA Rebranding Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "RUNNING IN WHATIF MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Check if we're in the right directory
if (!(Test-Path "NET.NpsWrapper.sln")) {
    Write-Error "This script must be run from the solution root directory (where NET.NpsWrapper.sln is located)"
    exit 1
}

# Check if Visual Studio is running
$vsProcesses = Get-Process devenv -ErrorAction SilentlyContinue
if ($vsProcesses) {
    Write-Warning "Visual Studio is currently running. Please close Visual Studio before continuing."
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne 'y' -and $continue -ne 'Y') {
        exit 0
    }
}

Write-Host "Step 1: Backing up solution file..." -ForegroundColor Yellow
if (-not $WhatIf) {
    Copy-Item "NET.NpsWrapper.sln" "NET.NpsWrapper.sln.backup"
}
Write-Host "  ? Backup created" -ForegroundColor Green

# Define rebranding mappings
$rebrandMap = @{
    # Project names
    "NpsWrapper" = "Omni2FA.NPS.Plugin"
    "NpsWrapperNET" = "Omni2FA.Adapter"
    "AsyncAuthHandler" = "Omni2FA.Auth"
    "AsyncAuthHandler.Tests" = "Omni2FA.Auth.Tests"
    
    # Namespaces
    "namespace NpsWrapperNET" = "namespace Omni2FA.Adapter"
    "namespace AsyncAuthHandler" = "namespace Omni2FA.Auth"
    
    # Using statements
    "using NpsWrapperNET" = "using Omni2FA.Adapter"
    "using AsyncAuthHandler" = "using Omni2FA.Auth"
    
    # Event log sources
    "NPS-Wrapper" = "Omni2FA-NPS-Plugin"
    "NPS-Wrapper.NET" = "Omni2FA-Adapter"
    "NPS-AsyncAuthHandler" = "Omni2FA-Auth"
    
    # App names
    "APP_NAME = `"NPS-Wrapper.NET`"" = "APP_NAME = `"Omni2FA-Adapter`""
    "APP_NAME = `"NPS-AsyncAuthHandler`"" = "APP_NAME = `"Omni2FA-Auth`""
    
    # Registry paths  
    "SOFTWARE\\NpsWrapperNET" = "SOFTWARE\\Omni2FA"
}

Write-Host ""
Write-Host "Step 2: Updating solution file..." -ForegroundColor Yellow
if (-not $WhatIf) {
    $solutionContent = Get-Content "NET.NpsWrapper.sln" -Raw
    $solutionContent = $solutionContent -replace 'NpsWrapper\\NpsWrapper', 'Omni2FA.NPS.Plugin\Omni2FA.NPS.Plugin'
    $solutionContent = $solutionContent -replace 'NpsWrapperNET\\NpsWrapperNET', 'Omni2FA.Adapter\Omni2FA.Adapter'
    $solutionContent = $solutionContent -replace 'AsyncAuthHandler\\AsyncAuthHandler', 'Omni2FA.Auth\Omni2FA.Auth'
    $solutionContent = $solutionContent -replace 'AsyncAuthHandler\.Tests\\AsyncAuthHandler\.Tests', 'Omni2FA.Auth.Tests\Omni2FA.Auth.Tests'
    $solutionContent = $solutionContent -replace '"NpsWrapper"', '"Omni2FA.NPS.Plugin"'
    $solutionContent = $solutionContent -replace '"NpsWrapperNET"', '"Omni2FA.Adapter"'
    $solutionContent = $solutionContent -replace '"AsyncAuthHandler"', '"Omni2FA.Auth"'
    $solutionContent = $solutionContent -replace '"AsyncAuthHandler\.Tests"', '"Omni2FA.Auth.Tests"'
    Set-Content "Omni2FA.sln" -Value $solutionContent -NoNewline
}
Write-Host "  ? Solution file updated and saved as Omni2FA.sln" -ForegroundColor Green

Write-Host ""
Write-Host "Step 3: Renaming project directories..." -ForegroundColor Yellow

# Rename directories
$directoryRenames = @(
    @{Old="NpsWrapper"; New="Omni2FA.NPS.Plugin"},
    @{Old="NpsWrapperNET"; New="Omni2FA.Adapter"},
    @{Old="AsyncAuthHandler"; New="Omni2FA.Auth"},
    @{Old="AsyncAuthHandler.Tests"; New="Omni2FA.Auth.Tests"}
)

foreach ($dir in $directoryRenames) {
    if (Test-Path $dir.Old) {
        Write-Host "  Renaming $($dir.Old) ? $($dir.New)" -ForegroundColor Cyan
        if (-not $WhatIf) {
            Rename-Item $dir.Old $dir.New
        }
    }
}
Write-Host "  ? Directories renamed" -ForegroundColor Green

Write-Host ""
Write-Host "Step 4: Renaming project files..." -ForegroundColor Yellow

# Rename C++ project files
if (Test-Path "Omni2FA.NPS.Plugin\NpsWrapper.vcxproj") {
    Write-Host "  Renaming NpsWrapper.vcxproj ? Omni2FA.NPS.Plugin.vcxproj" -ForegroundColor Cyan
    if (-not $WhatIf) {
        Rename-Item "Omni2FA.NPS.Plugin\NpsWrapper.vcxproj" "Omni2FA.NPS.Plugin.vcxproj"
        Rename-Item "Omni2FA.NPS.Plugin\NpsWrapper.vcxproj.filters" "Omni2FA.NPS.Plugin.vcxproj.filters"
        if (Test-Path "Omni2FA.NPS.Plugin\NpsWrapper.vcxproj.user") {
            Rename-Item "Omni2FA.NPS.Plugin\NpsWrapper.vcxproj.user" "Omni2FA.NPS.Plugin.vcxproj.user"
        }
    }
}

# Rename C# project files
$csharpProjects = @(
    @{Dir="Omni2FA.Adapter"; Old="NpsWrapperNET.csproj"; New="Omni2FA.Adapter.csproj"},
    @{Dir="Omni2FA.Auth"; Old="AsyncAuthHandler.csproj"; New="Omni2FA.Auth.csproj"},
    @{Dir="Omni2FA.Auth.Tests"; Old="AsyncAuthHandler.Tests.csproj"; New="Omni2FA.Auth.Tests.csproj"}
)

foreach ($proj in $csharpProjects) {
    $oldPath = "$($proj.Dir)\$($proj.Old)"
    if (Test-Path $oldPath) {
        Write-Host "  Renaming $oldPath ? $($proj.Dir)\$($proj.New)" -ForegroundColor Cyan
        if (-not $WhatIf) {
            Rename-Item $oldPath $proj.New
        }
    }
}
Write-Host "  ? Project files renamed" -ForegroundColor Green

Write-Host ""
Write-Host "Step 5: Updating code files..." -ForegroundColor Yellow

# Update C++ files
$cppFiles = Get-ChildItem "Omni2FA.NPS.Plugin\*.cpp", "Omni2FA.NPS.Plugin\*.h" -ErrorAction SilentlyContinue
foreach ($file in $cppFiles) {
    Write-Host "  Processing $($file.Name)" -ForegroundColor Cyan
    if (-not $WhatIf) {
        $content = Get-Content $file.FullName -Raw
        foreach ($key in $rebrandMap.Keys) {
            $content = $content -replace [regex]::Escape($key), $rebrandMap[$key]
        }
        Set-Content $file.FullName -Value $content -NoNewline
    }
}

# Update C# files  
$csFiles = Get-ChildItem "Omni2FA.Adapter\*.cs", "Omni2FA.Auth\*.cs", "Omni2FA.Auth.Tests\*.cs" -Recurse -ErrorAction SilentlyContinue
foreach ($file in $csFiles) {
    Write-Host "  Processing $($file.FullName.Replace($PWD.Path + '\', ''))" -ForegroundColor Cyan
    if (-not $WhatIf) {
        $content = Get-Content $file.FullName -Raw
        foreach ($key in $rebrandMap.Keys) {
            $content = $content -replace [regex]::Escape($key), $rebrandMap[$key]
        }
        Set-Content $file.FullName -Value $content -NoNewline
    }
}
Write-Host "  ? Code files updated" -ForegroundColor Green

Write-Host ""
Write-Host "Step 6: Updating project files..." -ForegroundColor Yellow

# Update vcxproj
if (Test-Path "Omni2FA.NPS.Plugin\Omni2FA.NPS.Plugin.vcxproj") {
    if (-not $WhatIf) {
        $vcxproj = Get-Content "Omni2FA.NPS.Plugin\Omni2FA.NPS.Plugin.vcxproj" -Raw
        $vcxproj = $vcxproj -replace 'NpsWrapper', 'Omni2FA.NPS.Plugin'
        $vcxproj = $vcxproj -replace 'NpsWrapperNET', 'Omni2FA.Adapter'
        Set-Content "Omni2FA.NPS.Plugin\Omni2FA.NPS.Plugin.vcxproj" -Value $vcxproj -NoNewline
    }
}

# Update csproj files
$csprojFiles = Get-ChildItem "Omni2FA.*\*.csproj" -ErrorAction SilentlyContinue
foreach ($file in $csprojFiles) {
    Write-Host "  Processing $($file.Name)" -ForegroundColor Cyan
    if (-not $WhatIf) {
        $content = Get-Content $file.FullName -Raw
        $content = $content -replace 'NpsWrapperNET', 'Omni2FA.Adapter'
        $content = $content -replace 'AsyncAuthHandler', 'Omni2FA.Auth'
        $content = $content -replace 'NpsWrapper', 'Omni2FA.NPS.Plugin'
        Set-Content $file.FullName -Value $content -NoNewline
    }
}
Write-Host "  ? Project files updated" -ForegroundColor Green

Write-Host ""
Write-Host "Step 7: Updating deployment script..." -ForegroundColor Yellow
if (Test-Path "Deploy-NpsPlugin.ps1") {
    if (-not $WhatIf) {
        $deployScript = Get-Content "Deploy-NpsPlugin.ps1" -Raw
        $deployScript = $deployScript -replace 'NET\.NpsWrapper', 'Omni2FA'
        $deployScript = $deployScript -replace 'NpsWrapper\.dll', 'Omni2FA.NPS.Plugin.dll'
        $deployScript = $deployScript -replace 'NpsWrapperNET\.dll', 'Omni2FA.Adapter.dll'
        $deployScript = $deployScript -replace 'NPS-Wrapper', 'Omni2FA-NPS-Plugin'
        $deployScript = $deployScript -replace 'NPS-Wrapper\.NET', 'Omni2FA-Adapter'
        $deployScript = $deployScript -replace 'NPS-AsyncAuthHandler', 'Omni2FA-Auth'
        $deployScript = $deployScript -replace 'SOFTWARE\\NpsWrapperNET', 'SOFTWARE\Omni2FA'
        $deployScript = $deployScript -replace 'NpsWrapperNET registry', 'Omni2FA registry'
        Set-Content "Deploy-Omni2FA.ps1" -Value $deployScript -NoNewline
    }
    Write-Host "  ? Deployment script updated and saved as Deploy-Omni2FA.ps1" -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 8: Updating README..." -ForegroundColor Yellow
if (Test-Path "README.md") {
    if (-not $WhatIf) {
        $readme = Get-Content "README.md" -Raw
        $readme = $readme -replace 'NET\.NpsWrapper', 'Omni2FA'
        $readme = $readme -replace 'NpsWrapper', 'Omni2FA.NPS.Plugin'
        $readme = $readme -replace 'NpsWrapperNET', 'Omni2FA.Adapter'
        $readme = $readme -replace 'AsyncAuthHandler', 'Omni2FA.Auth'
        $readme = $readme -replace 'NPS-Wrapper', 'Omni2FA-NPS-Plugin'
        $readme = $readme -replace 'SOFTWARE\\\\NpsWrapperNET', 'SOFTWARE\\\\Omni2FA'
        Set-Content "README.md" -Value $readme -NoNewline
    }
    Write-Host "  ? README updated" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Rebranding Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Review the changes" -ForegroundColor White
Write-Host "2. Open Omni2FA.sln in Visual Studio" -ForegroundColor White
Write-Host "3. Clean and rebuild the solution" -ForegroundColor White
Write-Host "4. Test the deployment script: Deploy-Omni2FA.ps1" -ForegroundColor White
Write-Host "5. Commit the changes: git add -A && git commit -m 'Rebrand to Omni2FA'" -ForegroundColor White
Write-Host ""

if ($WhatIf) {
    Write-Host "NOTE: This was a dry run. No actual changes were made." -ForegroundColor Yellow
    Write-Host "Run the script without -WhatIf to apply changes." -ForegroundColor Yellow
}

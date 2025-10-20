# PowerShell script to fix the Omni2FA.NPS.Plugin.Tests project file
# This script updates package references and Visual Studio version

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fixing Omni2FA.NPS.Plugin.Tests Project" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$projectFile = "Omni2FA.NPS.Plugin.Tests\Omni2FA.NPS.Plugin.Tests.vcxproj"

# Check if file exists
if (-not (Test-Path $projectFile)) {
    Write-Host "ERROR: Project file not found at $projectFile" -ForegroundColor Red
    Write-Host "Please run this script from the solution root directory." -ForegroundColor Yellow
    pause
    exit 1
}

# Check if file is locked (Visual Studio has it open)
try {
    $fileStream = [System.IO.File]::Open($projectFile, 'Open', 'ReadWrite', 'None')
    $fileStream.Close()
} catch {
    Write-Host "ERROR: Cannot modify the project file." -ForegroundColor Red
    Write-Host "The file appears to be locked by Visual Studio." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please close Visual Studio and run this script again." -ForegroundColor Yellow
    Write-Host "Or follow the manual steps in TESTING.md" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "Reading project file..." -ForegroundColor Green
$content = Get-Content $projectFile -Raw

Write-Host "Backing up original file..." -ForegroundColor Green
Copy-Item $projectFile "$projectFile.backup" -Force

Write-Host "Updating package references..." -ForegroundColor Green
# Replace googletest package references
$content = $content -replace 'googletest\.1\.15\.2', 'Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.1.8.1.7'
$content = $content -replace 'googletest\.targets', 'Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets'

# Remove old include directory references
$content = $content -replace '\$\(SolutionDir\)packages\\googletest\.1\.15\.2\\build\\native\\include;', ''

Write-Host "Updating Visual Studio version to 2022 (v143)..." -ForegroundColor Green
# Update platform toolset from v142 (VS2019) to v143 (VS2022)
$content = $content -replace '<PlatformToolset>v142</PlatformToolset>', '<PlatformToolset>v143</PlatformToolset>'

Write-Host "Saving updated project file..." -ForegroundColor Green
Set-Content $projectFile $content -NoNewline

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Project file updated successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Backup saved to: $projectFile.backup" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Restore NuGet packages:" -ForegroundColor White
Write-Host "   nuget restore Omni2FA.NPS.Plugin.Tests\packages.config -SolutionDirectory ." -ForegroundColor Gray
Write-Host "2. Open solution in Visual Studio 2022" -ForegroundColor White
Write-Host "3. Build the test project" -ForegroundColor White
Write-Host ""

pause

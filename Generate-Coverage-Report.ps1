# Generate Code Coverage Report for Omni2FA Projects
# This script runs tests with code coverage collection and generates HTML reports

param(
    [switch]$OpenReport = $true
)

Write-Host "=== Omni2FA Code Coverage Report Generator ===" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$coverageDir = Join-Path $rootDir "coverage-results"
$reportDir = Join-Path $rootDir "coverage-report"

# Clean up previous results
if (Test-Path $coverageDir) {
    Write-Host "Cleaning up previous coverage results..." -ForegroundColor Yellow
    Remove-Item -Path $coverageDir -Recurse -Force
}
if (Test-Path $reportDir) {
    Write-Host "Cleaning up previous coverage reports..." -ForegroundColor Yellow
    Remove-Item -Path $reportDir -Recurse -Force
}

# Create directories
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

Write-Host ""
Write-Host "Step 1: Running tests with code coverage for AuthClient..." -ForegroundColor Green

# Run tests with Coverlet for AuthClient
$authClientTestProject = Join-Path $rootDir "Omni2FA.AuthClient.Tests\Omni2FA.AuthClient.Tests.csproj"
$authClientCoverageFile = Join-Path $coverageDir "authclient-coverage.cobertura.xml"

try {
    dotnet test $authClientTestProject `
        --configuration Debug `
        --logger "console;verbosity=minimal" `
        /p:CollectCoverage=true `
        /p:CoverletOutputFormat=cobertura `
        /p:CoverletOutput="$authClientCoverageFile" `
        /p:ExcludeByFile="**/*AssemblyInfo.cs%2c**/*.g.cs" `
        /p:Exclude="[*.Tests]*%2c[AsyncAuthHandler]*%2c[Omni2FA.Auth]*" `
        /p:Include="[Omni2FA.AuthClient]*%2c[Omni2FA.Adapter]*"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Warning: Some tests failed, but coverage was collected." -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error running tests: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Generating HTML coverage report..." -ForegroundColor Green

# Check if coverage file was created
$coverageFiles = Get-ChildItem -Path $coverageDir -Filter "*.cobertura.xml" -Recurse

if ($coverageFiles.Count -eq 0) {
    Write-Host "Error: No coverage files found!" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($coverageFiles.Count) coverage file(s):" -ForegroundColor Cyan
foreach ($file in $coverageFiles) {
    Write-Host "  - $($file.Name)" -ForegroundColor Gray
}

# Generate report using ReportGenerator
$coverageFilePaths = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"

try {
    reportgenerator `
        "-reports:$coverageFilePaths" `
        "-targetdir:$reportDir" `
        "-reporttypes:Html;HtmlSummary;Badges;TextSummary" `
        "-title:Omni2FA Code Coverage Report" `
        "-verbosity:Info" `
        "-assemblyfilters:-AsyncAuthHandler;-Omni2FA.Auth"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error generating report!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error running ReportGenerator: $_" -ForegroundColor Red
    Write-Host "Make sure ReportGenerator is installed: dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "=== Code Coverage Report Generated Successfully! ===" -ForegroundColor Green
Write-Host ""

# Display summary
$summaryFile = Join-Path $reportDir "Summary.txt"
if (Test-Path $summaryFile) {
    Write-Host "Coverage Summary:" -ForegroundColor Cyan
    Write-Host "=================" -ForegroundColor Cyan
    Get-Content $summaryFile | Write-Host
    Write-Host ""
}

# Report locations
$indexFile = Join-Path $reportDir "index.html"
Write-Host "Reports generated at:" -ForegroundColor Cyan
Write-Host "  HTML Report: $indexFile" -ForegroundColor White
Write-Host "  Coverage Data: $coverageDir" -ForegroundColor White
Write-Host ""

# Open report in browser
if ($OpenReport -and (Test-Path $indexFile)) {
    Write-Host "Opening coverage report in browser..." -ForegroundColor Green
    Start-Process $indexFile
} else {
    Write-Host "To view the report, open: $indexFile" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Note: Old assemblies (AsyncAuthHandler, Omni2FA.Auth) are excluded from coverage." -ForegroundColor Yellow
Write-Host "Done! ?" -ForegroundColor Green

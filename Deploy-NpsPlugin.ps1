# NPS Plugin Deployment Script
# Compatible with Windows Server 2019 PowerShell
# Author: Based on NET.NpsWrapper project
# License: GPLv2.1

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$BuildType = "Debug"
)

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Please run PowerShell as Administrator and try again."
    exit 1
}

Write-Host "NPS Plugin Deployment Script" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green
Write-Host "Build Type: $BuildType" -ForegroundColor Yellow
Write-Host ""

# Function to create registry key if it doesn't exist
function Ensure-RegistryKey {
    param([string]$Path)
    if (!(Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
        Write-Host "Created registry key: $Path" -ForegroundColor Green
    }
}

# Function to set registry value
function Set-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [object]$Value,
        [string]$Type = "String"
    )
    Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type
    Write-Host "Set $Path\$Name = $Value" -ForegroundColor Cyan
}

# Collect user input for required settings
Write-Host "Please provide the following configuration settings:" -ForegroundColor Yellow
Write-Host ""

$basicAuthUserName = Read-Host "Enter BasicAuth Username"
$basicAuthPassword = Read-Host "Enter BasicAuth Password" -AsSecureString
$basicAuthPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($basicAuthPassword))

$serviceUrl = Read-Host "Enter Service URL (e.g., https://auth.example.com:8443)"
$mfaEnabledPolicy = Read-Host "Enter MFA-enabled NPS Policy Name"

Write-Host ""
Write-Host "Configuration Summary:" -ForegroundColor Yellow
Write-Host "  Username: $basicAuthUserName"
Write-Host "  Password: $('*' * $basicAuthPasswordPlain.Length)"
Write-Host "  Service URL: $serviceUrl"
Write-Host "  MFA Policy: $mfaEnabledPolicy"
Write-Host ""

$confirm = Read-Host "Continue with deployment? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Deployment cancelled." -ForegroundColor Red
    exit 0
}

try {
    # Step 1: Stop IAS (Internet Authentication Service/NPS) service
    Write-Host "Stopping IAS service..." -ForegroundColor Yellow
    Stop-Service -Name "IAS" -Force -ErrorAction Stop
    Write-Host "IAS service stopped successfully." -ForegroundColor Green

    # Step 2: Copy DLL files
    $sourcePath = "C:\dd\NET.NpsWrapper\x64\$BuildType"
    $destinationPath = "C:\Windows\System32"

    Write-Host "Copying DLL files from $sourcePath to $destinationPath..." -ForegroundColor Yellow
    
    if (!(Test-Path $sourcePath)) {
        throw "Source path not found: $sourcePath. Please ensure the project is built."
    }

    # Copy all files from source to destination
    Copy-Item -Path "$sourcePath\*.*" -Destination $destinationPath -Force
    Write-Host "DLL files copied successfully." -ForegroundColor Green

    # Step 3: Create Event Log sources
    Write-Host "Creating Event Log sources..." -ForegroundColor Yellow
    
    $eventSources = @("NPS-Wrapper", "NPS-Wrapper.NET", "NPS-AsyncAuthHandler")
    foreach ($source in $eventSources) {
        try {
            if (![System.Diagnostics.EventLog]::SourceExists($source)) {
                New-EventLog -LogName Application -Source $source
                Write-Host "Created Event Log source: $source" -ForegroundColor Green
            } else {
                Write-Host "Event Log source already exists: $source" -ForegroundColor Cyan
            }
        } catch {
            Write-Warning "Failed to create Event Log source: $source - $($_.Exception.Message)"
        }
    }

    # Step 4: Configure .NET for TLS 1.2
    Write-Host "Configuring .NET for TLS 1.2..." -ForegroundColor Yellow
    
    $tlsSettings = @(
        @{Path = "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319"; Name = "SchUseStrongCrypto"; Value = 1},
        @{Path = "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319"; Name = "SchUseStrongCrypto"; Value = 1},
        @{Path = "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319"; Name = "SystemDefaultTlsVersions"; Value = 1},
        @{Path = "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319"; Name = "SystemDefaultTlsVersions"; Value = 1}
    )

    foreach ($setting in $tlsSettings) {
        Ensure-RegistryKey -Path $setting.Path
        Set-RegistryValue -Path $setting.Path -Name $setting.Name -Value $setting.Value -Type "DWord"
    }

    # Step 5: Configure NpsWrapperNET registry settings
    Write-Host "Configuring NpsWrapperNET registry settings..." -ForegroundColor Yellow
    
    $npsWrapperPath = "HKLM:\SOFTWARE\NpsWrapperNET"
    Ensure-RegistryKey -Path $npsWrapperPath

    # Set all configuration values
    $regSettings = @{
        "AuthTimeout" = 60
        "BasicAuthPassword" = $basicAuthPasswordPlain
        "BasicAuthUserName" = $basicAuthUserName
        "EnableTraceLogging" = 1
        "IgnoreSslErrors" = 1
        "MfaEnabledNPSPolicy" = $mfaEnabledPolicy
        "NoMfaGroups" = "SMK\tsg-direct;SMK\TSG NO MFA"
        "PollInterval" = 1
        "PollMaxSeconds" = 90
        "ServiceUrl" = $serviceUrl
        "WaitBeforePoll" = 10
    }

    foreach ($setting in $regSettings.GetEnumerator()) {
        if ($setting.Name -in @("AuthTimeout", "EnableTraceLogging", "IgnoreSslErrors", "PollInterval", "PollMaxSeconds", "WaitBeforePoll")) {
            Set-RegistryValue -Path $npsWrapperPath -Name $setting.Name -Value $setting.Value -Type "DWord"
        } else {
            Set-RegistryValue -Path $npsWrapperPath -Name $setting.Name -Value $setting.Value -Type "String"
        }
    }

    # Step 6: Register NPS Extension DLLs
    Write-Host "Registering NPS Extension DLLs..." -ForegroundColor Yellow
    
    $npsWrapperDllPath = "$destinationPath\NpsWrapper.dll"
    if (!(Test-Path $npsWrapperDllPath)) {
        throw "NpsWrapper.dll not found at $npsWrapperDllPath"
    }

    # Register Extension DLLs
    $extensionDllsPath = "HKLM:\System\CurrentControlSet\Services\AuthSrv\Parameters"
    Ensure-RegistryKey -Path $extensionDllsPath

    # Get current ExtensionDLLs value
    $currentExtensionDLLs = @()
    try {
        $currentExtensionDLLs = Get-ItemProperty -Path $extensionDllsPath -Name "ExtensionDLLs" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty ExtensionDLLs
        if ($currentExtensionDLLs -eq $null) { $currentExtensionDLLs = @() }
    } catch {
        $currentExtensionDLLs = @()
    }

    # Add NpsWrapper.dll if not already present
    if ($npsWrapperDllPath -notin $currentExtensionDLLs) {
        $newExtensionDLLs = $currentExtensionDLLs + $npsWrapperDllPath
        Set-ItemProperty -Path $extensionDllsPath -Name "ExtensionDLLs" -Value $newExtensionDLLs -Type MultiString
        Write-Host "Added to ExtensionDLLs: $npsWrapperDllPath" -ForegroundColor Green
    } else {
        Write-Host "ExtensionDLLs already contains: $npsWrapperDllPath" -ForegroundColor Cyan
    }

    # Get current AuthorizationDLLs value
    $currentAuthorizationDLLs = @()
    try {
        $currentAuthorizationDLLs = Get-ItemProperty -Path $extensionDllsPath -Name "AuthorizationDLLs" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty AuthorizationDLLs
        if ($currentAuthorizationDLLs -eq $null) { $currentAuthorizationDLLs = @() }
    } catch {
        $currentAuthorizationDLLs = @()
    }

    # Add NpsWrapper.dll if not already present
    if ($npsWrapperDllPath -notin $currentAuthorizationDLLs) {
        $newAuthorizationDLLs = $currentAuthorizationDLLs + $npsWrapperDllPath
        Set-ItemProperty -Path $extensionDllsPath -Name "AuthorizationDLLs" -Value $newAuthorizationDLLs -Type MultiString
        Write-Host "Added to AuthorizationDLLs: $npsWrapperDllPath" -ForegroundColor Green
    } else {
        Write-Host "AuthorizationDLLs already contains: $npsWrapperDllPath" -ForegroundColor Cyan
    }

    # Step 7: Start IAS service
    Write-Host "Starting IAS service..." -ForegroundColor Yellow
    Start-Service -Name "IAS" -ErrorAction Stop
    Write-Host "IAS service started successfully." -ForegroundColor Green

    # Step 8: Verify service status
    Write-Host "Verifying IAS service status..." -ForegroundColor Yellow
    $serviceStatus = Get-Service -Name "IAS"
    Write-Host "IAS Service Status: $($serviceStatus.Status)" -ForegroundColor $(if ($serviceStatus.Status -eq 'Running') { 'Green' } else { 'Red' })

    Write-Host ""
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host "=========================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Configuration Summary:" -ForegroundColor Yellow
    Write-Host "  Build Type: $BuildType"
    Write-Host "  Service URL: $serviceUrl"
    Write-Host "  MFA Policy: $mfaEnabledPolicy"
    Write-Host "  Username: $basicAuthUserName"
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Test the NPS authentication with a client"
    Write-Host "2. Check Windows Event Logs (Application) for any errors"
    Write-Host "3. Verify MFA functionality with the configured policy"
    Write-Host ""

} catch {
    Write-Error "Deployment failed: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Attempting to restart IAS service..." -ForegroundColor Yellow
    try {
        Start-Service -Name "IAS" -ErrorAction SilentlyContinue
    } catch {
        Write-Warning "Failed to restart IAS service. Please start it manually."
    }
    exit 1
} finally {
    # Clear sensitive data from memory
    if ($basicAuthPasswordPlain) {
        $basicAuthPasswordPlain = $null
    }
    if ($basicAuthPassword) {
        $basicAuthPassword.Dispose()
    }
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
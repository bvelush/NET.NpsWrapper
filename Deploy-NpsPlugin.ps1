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
    # Mask password values in logging with fixed length
    $displayValue = if ($Name -eq "BasicAuthPassword") { "*" * 25 } else { $Value }
    Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type
    Write-Host "Set $Path\$Name = $displayValue" -ForegroundColor Cyan
}

# Function to safely manage NPS DLL registry entries
function Set-NpsDllRegistry {
    param(
        [string]$Path,
        [string]$ValueName,
        [string]$DllPath
    )
    
    Write-Host "Managing $ValueName registry entry..." -ForegroundColor Yellow
    
    # Get current values, handle both string and multistring types
    $currentValues = @()
    try {
        $regProperty = Get-ItemProperty -Path $Path -Name $ValueName -ErrorAction SilentlyContinue
        if ($regProperty) {
            $currentValue = $regProperty.$ValueName
            if ($currentValue -is [string[]]) {
                $currentValues = $currentValue
            } elseif ($currentValue -is [string] -and ![string]::IsNullOrWhiteSpace($currentValue)) {
                $currentValues = @($currentValue)
            }
        }
    } catch {
        Write-Host "No existing $ValueName found or error reading it." -ForegroundColor Cyan
    }
    
    Write-Host "Current $ValueName entries: $($currentValues.Count)" -ForegroundColor Cyan
    foreach ($entry in $currentValues) {
        Write-Host "  - $entry" -ForegroundColor Gray
    }
    
    # Remove any existing entries that contain NpsWrapper.dll (cleanup old installations)
    $cleanedValues = $currentValues | Where-Object { $_ -notlike "*NpsWrapper.dll*" }
    
    # Add our DLL path
    $newValues = $cleanedValues + $DllPath
    
    # Remove empty entries and ensure we have a proper array
    $finalValues = @($newValues | Where-Object { ![string]::IsNullOrWhiteSpace($_) })
    
    Write-Host "Setting $ValueName to $($finalValues.Count) entries:" -ForegroundColor Green
    foreach ($entry in $finalValues) {
        Write-Host "  + $entry" -ForegroundColor Green
    }
    
    # Set the registry value as MultiString
    Set-ItemProperty -Path $Path -Name $ValueName -Value $finalValues -Type MultiString
    Write-Host "Successfully updated $ValueName" -ForegroundColor Green
}

# Function to securely read password without showing any characters
function Read-SecurePassword {
    param([string]$Prompt)
    
    Write-Host $Prompt -NoNewline
    $password = ""
    $securePassword = New-Object System.Security.SecureString
    
    do {
        $key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        
        if ($key.VirtualKeyCode -eq 13) { # Enter key
            break
        }
        elseif ($key.VirtualKeyCode -eq 8) { # Backspace
            if ($password.Length -gt 0) {
                $password = $password.Substring(0, $password.Length - 1)
                $securePassword.RemoveAt($securePassword.Length - 1)
            }
        }
        elseif ($key.VirtualKeyCode -ge 32) { # Printable characters
            $password += $key.Character
            $securePassword.AppendChar($key.Character)
        }
    } while ($true)
    
    Write-Host "" # New line after password input
    return $securePassword
}

# Collect user input for required settings
Write-Host "Please provide the following configuration settings:" -ForegroundColor Yellow
Write-Host ""

$basicAuthUserName = Read-Host "Enter BasicAuth Username"
$basicAuthPassword = Read-SecurePassword "Enter BasicAuth Password: "
$basicAuthPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($basicAuthPassword))

$serviceUrl = Read-Host "Enter Service URL (e.g., https://auth.example.com:8443)"
$mfaEnabledPolicy = Read-Host "Enter MFA-enabled NPS Policy Name"

Write-Host ""
Write-Host "Configuration Summary:" -ForegroundColor Yellow
Write-Host "  Username: $basicAuthUserName"
Write-Host "  Password: $("*" * 25)" -ForegroundColor Yellow
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
    
    # Wait for service to fully release file locks
    Write-Host "Waiting for service to release file locks..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    # Verify service is fully stopped
    $maxWaitTime = 30
    $waitTime = 0
    do {
        $serviceStatus = Get-Service -Name "IAS"
        if ($serviceStatus.Status -eq 'Stopped') {
            Write-Host "Service fully stopped." -ForegroundColor Green
            break
        }
        Start-Sleep -Seconds 1
        $waitTime++
        if ($waitTime -ge $maxWaitTime) {
            throw "Service did not stop within $maxWaitTime seconds"
        }
    } while ($true)

    # Step 2: Copy DLL files using CMD copy (proven to work)
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $sourcePath = Join-Path $scriptDirectory "x64\$BuildType"
    $destinationPath = "C:\Windows\System32"

    Write-Host "Copying DLL files from $sourcePath to $destinationPath..." -ForegroundColor Yellow
    
    if (!(Test-Path $sourcePath)) {
        throw "Source path not found: $sourcePath. Please ensure the project is built with BuildType '$BuildType'."
    }

    # Get file listing for reporting
    $sourceFiles = Get-ChildItem -Path $sourcePath -File
    Write-Host "Found $($sourceFiles.Count) files to deploy:" -ForegroundColor Cyan
    foreach ($file in $sourceFiles) {
        Write-Host "  - $($file.Name)" -ForegroundColor Gray
    }

    # Use CMD copy (same as original deploy.cmd)
    $cmdCommand = "copy /Y `"$sourcePath\*.*`" `"$destinationPath`""
    Write-Host "Executing file copy..." -ForegroundColor Yellow
    
    try {
        $cmdOutput = & cmd /c $cmdCommand 2>&1
        $cmdExitCode = $LASTEXITCODE
        
        if ($cmdExitCode -ne 0) {
            Write-Host "CMD copy output:" -ForegroundColor Red
            $cmdOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
            throw "File copy failed with exit code: $cmdExitCode"
        }
        
        # Show copy results
        $copiedFiles = ($cmdOutput | Where-Object { $_ -match "^\s*\d+\s+file\(s\)\s+copied" })
        if ($copiedFiles) {
            Write-Host "$copiedFiles" -ForegroundColor Green
        }
    } catch {
        Write-Error "File copy failed: $($_.Exception.Message)"
        throw
    }

    # Verify critical files exist after copy
    $criticalFiles = @("NpsWrapper.dll", "NpsWrapperNET.dll")
    Write-Host "Verifying deployment..." -ForegroundColor Yellow
    foreach ($fileName in $criticalFiles) {
        $filePath = Join-Path $destinationPath $fileName
        if (Test-Path $filePath) {
            $fileInfo = Get-Item $filePath
            Write-Host "  ? $fileName deployed successfully ($($fileInfo.Length) bytes, $($fileInfo.LastWriteTime))" -ForegroundColor Green
        } else {
            throw "Critical file missing after deployment: $fileName"
        }
    }
    
    Write-Host "File deployment completed successfully." -ForegroundColor Green

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

    # Step 6: Register NPS Extension DLLs (FIXED - proper cleanup and registration)
    Write-Host "Registering NPS Extension DLLs..." -ForegroundColor Yellow
    
    $npsWrapperDllPath = "$destinationPath\NpsWrapper.dll"
    if (!(Test-Path $npsWrapperDllPath)) {
        throw "NpsWrapper.dll not found at $npsWrapperDllPath"
    }

    # Register Extension DLLs
    $extensionDllsPath = "HKLM:\System\CurrentControlSet\Services\AuthSrv\Parameters"
    Ensure-RegistryKey -Path $extensionDllsPath

    # Use the new safe function to manage registry entries
    Set-NpsDllRegistry -Path $extensionDllsPath -ValueName "ExtensionDLLs" -DllPath $npsWrapperDllPath
    Set-NpsDllRegistry -Path $extensionDllsPath -ValueName "AuthorizationDLLs" -DllPath $npsWrapperDllPath

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
    Write-Host "  Password: $("*" * 25)" -ForegroundColor Yellow
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
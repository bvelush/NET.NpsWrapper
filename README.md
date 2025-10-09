# Creating Source in Windows Applications log
```PowerShell
New-EventLog -LogName Application -Source "NPS-Wrapper"
New-EventLog -LogName Application -Source "NPS-Wrapper.NET"
New-EventLog -LogName Application -Source "NPS-AsyncAuthHandler"
```

Modifying .NET for TLS v1.2:
```PowerShell
# Enable TLS 1.2 for .NET 4.7.2
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319" -Name "SchUseStrongCrypto" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319" -Name "SchUseStrongCrypto" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319" -Name "SystemDefaultTlsVersions" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319" -Name "SystemDefaultTlsVersions" -Value 1 -Type DWord
```

Registry settings:
```reg
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\NpsWrapperNET]
"AuthTimeout"=dword:0000003c
"BasicAuthPassword"="<password>"
"BasicAuthUserName"="<username>"
"EnableTraceLogging"=dword:00000001
"IgnoreSslErrors"=dword:00000001
"MfaEnabledNPSPolicy"="Name of NPS policy that needs MFA"
"NoMfaGroups"="SMK\\tsg-direct;SMK\\TSG NO MFA"
"PollInterval"=dword:00000001
"PollMaxSeconds"=dword:0000005a
"ServiceUrl"="https://auth.smk:8443"
"WaitBeforePoll"=dword:0000000a
```

# Deploy

run deploy.cmd

# NET.NpsWrapper

NET.NpsWrapper aims to enrich Microsoft Network Policy Server (NPS)
through a set of custom DLLs fitting some use cases you might have.
Thanks to `OpenCymd.Nps` project provides a .NET wrapper around
NPS API for easier customization.

# Contents

## NpsWrapper

NPS Extension DLL exporting callback functions and bridge to .NET NpsWrapperNET dll.

## NpsWrapperNET

.NET implementation of NPS Extension API functions.

## Auth_WatchGuard

Adds MFA support to NPS based on WatchGuard AuthPoint.

# Installation

To be recognized by NPS two Registry values of type `REG_MULTI_SZ`
need to be created or complemented:

 * `HKLM\System\CurrentControlSet\Services\AuthSrv\Parameters\ExtensionDLLs`
 * `HKLM\System\CurrentControlSet\Services\AuthSrv\Parameters\AuthorizationDLLs`

Both values need to contain appropriate full path to `NpsWrapper.dll`.

# Compatibility

Tested on Windows Server 2016 - 2019.

# License & Copyright

[Copyright lestoilfante 2023](https://github.com/lestoilfante)

GNU General Public License version 2.1 (GPLv2.1) 
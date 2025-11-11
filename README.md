# Creating Source in Windows Applications log
```PowerShell
New-EventLog -LogName Application -Source "SMK2FA.NPS.Plugin"
New-EventLog -LogName Application -Source "Omni2FA.Adapter"
New-EventLog -LogName Application -Source "Omni2FA.AuthClient"
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

[HKEY_LOCAL_MACHINE\SOFTWARE\Omni2FA.NPS]
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


# Installation

To be recognized by NPS two Registry values of type `REG_MULTI_SZ`
need to be created or complemented:

 * `HKLM\System\CurrentControlSet\Services\AuthSrv\Parameters\ExtensionDLLs`
 * `HKLM\System\CurrentControlSet\Services\AuthSrv\Parameters\AuthorizationDLLs`

Both values need to contain appropriate full path to `Omni2FA.NPS.Plugin.dll`.

# Compatibility

Tested on Windows Server 2012 - 2019.


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
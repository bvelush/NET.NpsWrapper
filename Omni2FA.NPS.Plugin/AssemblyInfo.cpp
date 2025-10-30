#include "pch.h"
#include "../Generated/version.h"

using namespace System;
using namespace System::Reflection;
using namespace System::Runtime::CompilerServices;
using namespace System::Runtime::InteropServices;
using namespace System::Security::Permissions;

[assembly:AssemblyTitleAttribute(L"Omni2FA.NPS.Plugin")];
[assembly:AssemblyDescriptionAttribute(L"")];
[assembly:AssemblyConfigurationAttribute(L"")];
[assembly:AssemblyCompanyAttribute(L"")];
[assembly:AssemblyProductAttribute(L"Omni2FA.NPS.Plugin")];
[assembly:AssemblyCopyrightAttribute(L"Copyright (c) 2023-2025")];
[assembly:AssemblyTrademarkAttribute(L"")];
[assembly:AssemblyCultureAttribute(L"")];

// Version is now defined in Generated/version.h
// VERSION_STRING is already a string literal, just need to convert to wide string
#define WIDE2(x) L##x
#define WIDE(x) WIDE2(x)
[assembly:AssemblyVersionAttribute(WIDE(VERSION_STRING))];
[assembly:AssemblyInformationalVersionAttribute(WIDE(VERSION_SHORT_STRING) L"+" WIDE(GIT_COMMIT_HASH))];

[assembly:ComVisible(false)];
[assembly:CLSCompliantAttribute(true)];

# Event Code Implementation Summary

## Overview
Successfully implemented standardized event codes across all Omni2FA projects to enable better filtering and monitoring in Windows Event Viewer and other logging tools.

## Changes Made

### 1. Core Logging Infrastructure (Omni2FA.Net.Utils\Log.cs)
- **Modified**: Added `eventCode` parameter to both `Event()` method overloads
- **Change**: Updated `EventInstance` to use the provided event code instead of hardcoded `0`
- **Impact**: All logging calls now require an event code parameter

### 2. Request Logging (Omni2FA.Net.Utils\Log.cs - logRequest method)
- **Event Code 120**: RadiusExtensionProcess2 called with params (trace)
- **Event Code 121**: Authorization request details (trace)
- **Event Code 122**: Request components (trace)
- **Event Code 123**: Response components (trace)

### 3. Adapter (Omni2FA.Adapter\Omni2FA.Adapter.cs)
Updated all Log.Event calls with appropriate event codes:

**Initialization (100-109)**
- **Event Code 102**: Initializing Omni2FA.Adapter with version info
- **Event Code 10**: RadiusExtensionInit called (trace)
- **Event Code 12**: Hostname detected (trace)

**Informational (200-299)**
- **Event Code 201**: MFA-enabled NPS policy set to specific policy
- **Event Code 202**: MfaEnabledNPSPolicy registry value is empty or missing
- **Event Code 203**: Policy does NOT match MFA-enabled policy, skipping MFA
- **Event Code 204**: (Not used in Adapter - reserved for AuthClient)

**User/Group Resolution (140-149)**
- **Event Code 140**: NoMFA group added (local or domain)
- **Event Code 141**: User is in NoMFA group, skipping MFA

**Request Processing (120-129)**
- **Event Code 124**: Processing authorized AccessRequest for MFA (trace)
- **Event Code 125**: Policy matches MFA-enabled policy (trace)
- **Event Code 126**: No MFA-enabled policy configured (trace)

**MFA Events (130-139)**
- **Event Code 130**: MFA succeeded for user
- **Event Code 131**: MFA failed for user
- **Event Code 132**: MFA skipped for user

**Warnings (300-399)**
- **Event Code 302**: Error resolving NoMFA group
- **Event Code 303**: NoMFA group not found
- **Event Code 304**: NoMfaGroups registry value is empty or missing
- **Event Code 305**: Error checking NoMFA group membership for user

**Termination (110-119)**
- **Event Code 11**: RadiusExtensionTerm called (trace)

### 4. AuthClient (Omni2FA.AuthClient\Authenticator.cs)
Updated all Log.Event calls with appropriate event codes:

**Initialization (100-109)**
- **Event Code 103**: Initializing Omni2FA.AuthClient with version info
- **Event Code 29**: Using injected HttpClient (trace)

**Informational (200-299)**
- **Event Code 204**: Omni2FA.Auth initialized with service URL
- **Event Code 205**: SSL certificate validation disabled
- **Event Code 206**: Basic authentication configured for user

**Warnings (300-399)**
- **Event Code 301**: SSL certificate validation bypassed
- **Event Code 310**: AuthResult responded with non-success status code

**Trace Events (0-99)**
- **Event Code 20**: Sending authentication request (trace)
- **Event Code 21**: Received authentication response (trace)
- **Event Code 22**: Deserialized authentication response (trace)
- **Event Code 23**: Authentication failed without polling (trace)
- **Event Code 24**: Authentication succeeded without polling (trace)
- **Event Code 25**: Polling AuthResult (trace)
- **Event Code 26**: Polled AuthResult response (trace)
- **Event Code 27**: Authentication succeeded after polling (trace)
- **Event Code 28**: Authentication failed after polling (trace)

**Errors (400-499)**
- **Event Code 410**: Service responded with non-success status code
- **Event Code 411**: Invalid response from service
- **Event Code 412**: Invalid AuthResult response
- **Event Code 413**: Authentication result not received in time
- **Event Code 414**: Timeout reached while authenticating
- **Event Code 415**: MFA Service is unreachable while authenticating
- **Event Code 416**: Error authenticating user
- **Event Code 417**: Timeout reached while polling AuthResult
- **Event Code 418**: MFA Service is unreachable while polling AuthResult
- **Event Code 419**: Error polling AuthResult

### 5. NPS Plugin C++ (Omni2FA.NPS.Plugin\Omni2FA.NPS.Plugin.cpp)
Updated all LogEvent calls with appropriate event codes:

**Trace Events (0-99)**
- **Event Code 1**: RadiusExtensionInit called (trace)
- **Event Code 2**: RadiusExtensionTerm called (trace)
- **Event Code 3**: RadiusExtensionProcess2 called (trace)
- **Event Code 4**: RadiusExtensionInit completed with result (trace)
- **Event Code 5**: RadiusExtensionTerm completed (trace)
- **Event Code 6**: RadiusExtensionProcess2 completed with result (trace)
- **Event Code 7**: LocalAssemblyResolver called (trace)

**Initialization (100-109)**
- **Event Code 100**: Initializing Omni2FA.NPS.Plugin with version info
- **Event Code 101**: Omni2FA.NPS.Plugin initialized

**Cleanup/Termination (110-119)**
- **Event Code 110**: Cleaning up Omni2FA.NPS.Plugin
- **Event Code 111**: Omni2FA.NPS.Plugin cleaned up

**Informational (200-299)**
- **Event Code 200**: Assembly resolve requested / Loading assembly from path

**Warnings (300-399)**
- **Event Code 300**: Assembly not found

**Errors (400-499)**
- **Event Code 400**: Error in LocalAssemblyResolver
- **Event Code 401**: Error during Initialize
- **Event Code 402**: Error during Cleanup
- **Event Code 403**: Error in RadiusExtensionInit
- **Event Code 404**: Error in RadiusExtensionTerm
- **Event Code 405**: Error in RadiusExtensionProcess2

### 6. Registry Helper (Omni2FA.Net.Utils\Registry.cs)
- **Event Code 304**: Registry base key not found (reused from warnings)
- **Event Code 305**: Error reading settings from registry (reused from warnings)

### 7. Documentation (EventCodes.md)
Created comprehensive documentation including:
- Event code range definitions
- Detailed table of all event codes with descriptions
- Event Viewer XML query examples for filtering
- Usage guidance for administrators

## Event Code Organization

The event codes are organized into logical ranges:

| Range | Purpose |
|-------|---------|
| 0-99 | Trace/Debug events (only when EnableTraceLogging is enabled) |
| 100-109 | Initialization events |
| 110-119 | Cleanup/Termination events |
| 120-129 | Request Processing details |
| 130-139 | MFA-specific events |
| 140-149 | User/Group resolution events |
| 200-299 | Informational events |
| 300-399 | Warning events |
| 400-499 | Error events |

## Benefits

1. **Improved Filtering**: Administrators can now filter events by code range or specific codes
2. **Better Monitoring**: Automated monitoring tools can trigger on specific event codes
3. **Consistent Categorization**: Similar events across different components have related codes
4. **Source Flexibility**: Same event codes can be used across different sources (Omni2FA.Adapter, Omni2FA.NPS.Plugin, Omni2FA.AuthClient) since filtering can combine Source and Event ID

## Testing Recommendations

1. Verify trace events (0-99) only appear when `EnableTraceLogging` registry setting is enabled
2. Test Event Viewer filtering using the provided XML queries in EventCodes.md
3. Confirm that event codes appear correctly in Windows Event Log
4. Validate that existing functionality is not affected by the changes

## Build Status

? All projects build successfully with the new event code implementation

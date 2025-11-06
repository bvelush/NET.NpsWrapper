# Event Codes Reference

This document describes the event codes used across Omni2FA projects for Windows Event Log filtering.

## Event Code Ranges

| Range | Category | Description |
|-------|----------|-------------|
| 0-99 | Trace/Debug | Detailed trace and debug information (only logged when EnableTraceLogging is enabled) |
| 100-109 | Initialization | Component initialization and startup events |
| 110-119 | Cleanup/Termination | Component cleanup and shutdown events |
| 120-129 | Request Processing | RADIUS request and response processing details |
| 130-139 | MFA Events | MFA authentication specific events |
| 140-149 | User/Group Resolution | Active Directory user and group resolution events |
| 200-299 | Informational | General informational events |
| 300-399 | Warnings | Warning events that don't prevent operation |
| 400-499 | Errors | Error events |

## Detailed Event Codes

### Trace/Debug Events (0-99)

| Code | Source | Description |
|------|--------|-------------|
| 1 | Omni2FA.NPS.Plugin | RadiusExtensionInit called (trace) |
| 2 | Omni2FA.NPS.Plugin | RadiusExtensionTerm called (trace) |
| 3 | Omni2FA.NPS.Plugin | RadiusExtensionProcess2 called (trace) |
| 4 | Omni2FA.NPS.Plugin | RadiusExtensionInit completed with result (trace) |
| 5 | Omni2FA.NPS.Plugin | RadiusExtensionTerm completed (trace) |
| 6 | Omni2FA.NPS.Plugin | RadiusExtensionProcess2 completed with result (trace) |
| 7 | Omni2FA.NPS.Plugin | LocalAssemblyResolver called (trace) |
| 10 | Omni2FA.Adapter | RadiusExtensionInit called (trace) |
| 11 | Omni2FA.Adapter | RadiusExtensionTerm called (trace) |
| 12 | Omni2FA.Adapter | Hostname detected (trace) |
| 20 | Omni2FA.AuthClient | Sending authentication request (trace) |
| 21 | Omni2FA.AuthClient | Received authentication response (trace) |
| 22 | Omni2FA.AuthClient | Deserialized authentication response (trace) |
| 23 | Omni2FA.AuthClient | Authentication failed without polling (trace) |
| 24 | Omni2FA.AuthClient | Authentication succeeded without polling (trace) |
| 25 | Omni2FA.AuthClient | Polling AuthResult (trace) |
| 26 | Omni2FA.AuthClient | Polled AuthResult response (trace) |
| 27 | Omni2FA.AuthClient | Authentication succeeded after polling (trace) |
| 28 | Omni2FA.AuthClient | Authentication failed after polling (trace) |
| 29 | Omni2FA.AuthClient | Using injected HttpClient (trace) |

### Initialization Events (100-109)

| Code | Source | Description |
|------|--------|-------------|
| 100 | Omni2FA.NPS.Plugin | Initializing Omni2FA.NPS.Plugin with version info |
| 101 | Omni2FA.NPS.Plugin | Omni2FA.NPS.Plugin initialized |
| 102 | Omni2FA.Adapter | Initializing Omni2FA.Adapter with version info |
| 103 | Omni2FA.AuthClient | Initializing Omni2FA.AuthClient with version info |

### Cleanup/Termination Events (110-119)

| Code | Source | Description |
|------|--------|-------------|
| 110 | Omni2FA.NPS.Plugin | Cleaning up Omni2FA.NPS.Plugin |
| 111 | Omni2FA.NPS.Plugin | Omni2FA.NPS.Plugin cleaned up |

### Request Processing Events (120-129)

| Code | Source | Description |
|------|--------|-------------|
| 120 | Omni2FA.Net.Utils | RadiusExtensionProcess2 called with params (trace) |
| 121 | Omni2FA.Net.Utils | Authorization request details (trace) |
| 122 | Omni2FA.Net.Utils | Request components (trace) |
| 123 | Omni2FA.Net.Utils | Response components (trace) |
| 124 | Omni2FA.Adapter | Processing authorized AccessRequest for MFA (trace) |
| 125 | Omni2FA.Adapter | Policy matches MFA-enabled policy (trace) |
| 126 | Omni2FA.Adapter | No MFA-enabled policy configured (trace) |

### MFA Events (130-139)

| Code | Source | Description |
|------|--------|-------------|
| 130 | Omni2FA.Adapter | MFA succeeded for user |
| 131 | Omni2FA.Adapter | MFA failed for user |
| 132 | Omni2FA.Adapter | MFA skipped for user |

### User/Group Resolution Events (140-149)

| Code | Source | Description |
|------|--------|-------------|
| 140 | Omni2FA.Adapter | NoMFA group added (local or domain) |
| 141 | Omni2FA.Adapter | User is in NoMFA group, skipping MFA |

### Informational Events (200-299)

| Code | Source | Description |
|------|--------|-------------|
| 200 | Omni2FA.NPS.Plugin | Loading assembly from path |
| 201 | Omni2FA.Adapter | MFA-enabled NPS policy set to specific policy |
| 202 | Omni2FA.Adapter | MfaEnabledNPSPolicy registry value is empty or missing |
| 203 | Omni2FA.Adapter | Policy does NOT match MFA-enabled policy, skipping MFA |
| 204 | Omni2FA.AuthClient | Omni2FA.Auth initialized with service URL |
| 205 | Omni2FA.AuthClient | SSL certificate validation disabled |
| 206 | Omni2FA.AuthClient | Basic authentication configured for user |

### Warning Events (300-399)

| Code | Source | Description |
|------|--------|-------------|
| 300 | Omni2FA.NPS.Plugin | Assembly not found |
| 301 | Omni2FA.NPS.Plugin | SSL certificate validation bypassed |
| 302 | Omni2FA.Adapter | Error resolving NoMFA group |
| 303 | Omni2FA.Adapter | NoMFA group not found |
| 304 | Omni2FA.Adapter | NoMfaGroups registry value is empty or missing |
| 305 | Omni2FA.Adapter | Error checking NoMFA group membership for user |
| 310 | Omni2FA.AuthClient | AuthResult responded with non-success status code |

### Error Events (400-499)

| Code | Source | Description |
|------|--------|-------------|
| 400 | Omni2FA.NPS.Plugin | Error in LocalAssemblyResolver |
| 401 | Omni2FA.NPS.Plugin | Error during Initialize |
| 402 | Omni2FA.NPS.Plugin | Error during Cleanup |
| 403 | Omni2FA.NPS.Plugin | Error in RadiusExtensionInit |
| 404 | Omni2FA.NPS.Plugin | Error in RadiusExtensionTerm |
| 405 | Omni2FA.NPS.Plugin | Error in RadiusExtensionProcess2 |
| 410 | Omni2FA.AuthClient | Service responded with non-success status code |
| 411 | Omni2FA.AuthClient | Invalid response from service |
| 412 | Omni2FA.AuthClient | Invalid AuthResult response |
| 413 | Omni2FA.AuthClient | Authentication result not received in time |
| 414 | Omni2FA.AuthClient | Timeout reached while authenticating |
| 415 | Omni2FA.AuthClient | MFA Service is unreachable while authenticating |
| 416 | Omni2FA.AuthClient | Error authenticating user |
| 417 | Omni2FA.AuthClient | Timeout reached while polling AuthResult |
| 418 | Omni2FA.AuthClient | MFA Service is unreachable while polling AuthResult |
| 419 | Omni2FA.AuthClient | Error polling AuthResult |

## Usage Examples

### Filtering by Event Range

To filter initialization events in Event Viewer:
```xml
<QueryList>
  <Query Id="0" Path="Application">
    <Select Path="Application">
      *[System[Provider[@Name='Omni2FA.Adapter' or @Name='Omni2FA.NPS.Plugin'] 
      and (EventID &gt;= 100 and EventID &lt;= 109)]]
    </Select>
  </Query>
</QueryList>
```

### Filtering MFA Success/Failure

To filter MFA authentication results:
```xml
<QueryList>
  <Query Id="0" Path="Application">
    <Select Path="Application">
      *[System[Provider[@Name='Omni2FA.Adapter'] 
      and (EventID = 130 or EventID = 131 or EventID = 132)]]
    </Select>
  </Query>
</QueryList>
```

### Filtering Errors Only

To filter all error events:
```xml
<QueryList>
  <Query Id="0" Path="Application">
    <Select Path="Application">
      *[System[Provider[@Name='Omni2FA.Adapter' or @Name='Omni2FA.NPS.Plugin' or @Name='Omni2FA.AuthClient'] 
      and (EventID &gt;= 400 and EventID &lt;= 499)]]
    </Select>
  </Query>
</QueryList>
```

## Notes

- Event codes may be reused across different source components (Omni2FA.Adapter, Omni2FA.NPS.Plugin, Omni2FA.AuthClient) as filtering can be done by both Source and Event ID
- Trace events (0-99) are only logged when `EnableTraceLogging` registry setting is enabled
- All events are written to the Windows Application event log

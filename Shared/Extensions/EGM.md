# Extensions

## Overview

Extension methods for shared collections, entities, logging, streams, strings, session reasons, and tasks.

## Interface

- DeviceExtensions.ToDto converts a Device to DeviceClientDto through JSON serialization [Shared/Extensions/DeviceExtensions.cs:ToDto].
- IEnumerableExtensions.IndexWhere returns the first matching index or -1 [Shared/Extensions/IEnumerableExtensions.cs:IndexWhere].
- ILoggerExtensions.Enter and LogResult add scoped logging helpers [Shared/Extensions/ILoggerExtensions.cs:Enter/LogResult].
- SessionSwitchReasonExExtensions.IsTerminalSessionSwitch returns true only for SessionLogoff [Shared/Extensions/SessionSwitchReasonExExtensions.cs:IsTerminalSessionSwitch].
- StreamExtensions.CopyToAsync copies data and reports cumulative bytes read [Shared/Extensions/StreamExtensions.cs:CopyToAsync].
- StringExtensions provides filename and filesystem-path character sanitizers [Shared/Extensions/StringExtensions.cs:SanitizeFileName/SanitizeFileSystemPath].
- TaskExtensions.Forget observes task completion and optionally invokes an exception callback [Shared/Extensions/TaskExtensions.cs:Forget].

## Invariants

- IsTerminalSessionSwitch treats lock/unlock and connect/disconnect reasons as nonterminal; only SessionLogoff ends the session [Shared/Extensions/SessionSwitchReasonExExtensions.cs:IsTerminalSessionSwitch].
- TaskExtensions.Forget is async void and suppresses task errors if no exception handler is supplied. Callers that need failure reporting must supply a handler [Shared/Extensions/TaskExtensions.cs:Forget].
- DeviceExtensions.ToDto throws SerializationException if deserialization does not produce a DTO [Shared/Extensions/DeviceExtensions.cs:ToDto].

## Side Effects

- ILoggerExtensions writes through the supplied ILogger [Shared/Extensions/ILoggerExtensions.cs:Enter/LogResult].
- StreamExtensions.CopyToAsync reads the source and writes the destination, invoking the byte-count callback after each write [Shared/Extensions/StreamExtensions.cs:CopyToAsync].
- TaskExtensions.Forget starts observation of the supplied task; the optional exception handler may itself perform additional work [Shared/Extensions/TaskExtensions.cs:Forget].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


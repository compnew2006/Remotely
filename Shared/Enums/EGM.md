# Enums

## Overview

Shared enum values used by persistence models, DTOs, and client/server contracts.

## Interface

- RemoteAccessMode defines ViewOnly = 1 and FullControl = 2 [Shared/Enums/RemoteAccessMode.cs:RemoteAccessMode].
- Platform, DbProvider, EventType, RepeatInterval, ScriptingShell, and ScriptInputType define shared platform, persistence, and scripting values [Shared/Enums/Platform.cs:Platform; Shared/Enums/DbProvider.cs:DbProvider; Shared/Enums/EventType.cs:EventType; Shared/Enums/RepeatInterval.cs:RepeatInterval; Shared/Enums/ScriptingShell.cs:ScriptingShell; Shared/Enums/ScriptInputType.cs:ScriptInputType].
- Theme, PromptForAccessResult, ChatHistoryItemOrigin, CompletionIntent, SessionEndReasonsEx, and SessionSwitchReasonEx define shared UI and session values [Shared/Enums/Theme.cs:Theme; Shared/Enums/PromptForAccessResult.cs:PromptForAccessResult; Shared/Enums/ChatHistoryItemOrigin.cs:ChatHistoryItemOrigin; Shared/Enums/CompletionIntent.cs:CompletionIntent; Shared/Enums/SessionEndReasonsEx.cs:SessionEndReasonsEx; Shared/Enums/SessionSwitchReasonEx.cs:SessionSwitchReasonEx].

## Invariants

- RemoteAccessMode's XML documentation describes server-owned authority; the enum alone does not enforce an authorization boundary [Shared/Enums/RemoteAccessMode.cs:RemoteAccessMode].
- SessionSwitchReasonExExtensions.IsTerminalSessionSwitch treats SessionLogoff as terminal [Shared/Extensions/SessionSwitchReasonExExtensions.cs:IsTerminalSessionSwitch].

## Side Effects

- None. Enum declarations contain values and no operations [Shared/Enums/RemoteAccessMode.cs:RemoteAccessMode; Shared/Enums/Platform.cs:Platform].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


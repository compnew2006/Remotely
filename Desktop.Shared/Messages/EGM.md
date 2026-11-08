# Desktop Messenger Messages

## Overview

These message types carry desktop app-state, display, and Windows session changes between services [Desktop.Shared/Messages/AppStateHostChangedMessage.cs:AppStateHostChangedMessage; Desktop.Shared/Messages/WindowsSessionSwitchedMessage.cs:WindowsSessionSwitchedMessage].

## Interface

- `AppStateHostChangedMessage` carries `NewHost`; `DisplaySettingsChangedMessage` is a marker message [Desktop.Shared/Messages/AppStateHostChangedMessage.cs:AppStateHostChangedMessage; Desktop.Shared/Messages/DisplaySettingsChangedMessage.cs:DisplaySettingsChangedMessage].
- `WindowsSessionEndingMessage` carries a `SessionEndReasonsEx`; `WindowsSessionSwitchedMessage` carries a `SessionSwitchReasonEx` and session ID [Desktop.Shared/Messages/WindowsSessionEndingMessage.cs:WindowsSessionEndingMessage; Desktop.Shared/Messages/WindowsSessionSwitchedMessage.cs:WindowsSessionSwitchedMessage].

## Invariants

- Message payloads are immutable after construction; the classes expose getter-only values and the display message has no payload [Desktop.Shared/Messages/AppStateHostChangedMessage.cs:AppStateHostChangedMessage.NewHost; Desktop.Shared/Messages/WindowsSessionEndingMessage.cs:WindowsSessionEndingMessage.Reason; Desktop.Shared/Messages/WindowsSessionSwitchedMessage.cs:WindowsSessionSwitchedMessage.Reason/SessionId].

## Side Effects

- None; these types only carry values for message dispatch [Desktop.Shared/Messages/DisplaySettingsChangedMessage.cs:DisplaySettingsChangedMessage].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

# Device Components

## Overview
Blazor components for device cards, device collections, chat, and the remote PowerShell terminal.

## Interface
- DevicesFrame filters and refreshes the device collection; DeviceCard renders an individual device and its actions. [DevicesFrame.razor.cs:Refresh] [DeviceCard.razor.cs:OnInitializedAsync]
- ChatFrame and ChatCard present chat sessions and messages. [ChatFrame.razor.cs:HandleChatMessageReceived] [ChatCard.razor.cs:HandleChatMessageReceived]
- Terminal presents remote command input, completion, and script selection. [Terminal.razor.cs:EvaluateInputKeypress] [Terminal.razor.cs:HandlePowerShellCompletionsMessage]

## Invariants
- Device and chat views update from typed messenger notifications and component lifecycle handlers. [DevicesFrame.razor.cs:HandleDeviceStateChangedMessage] [ChatFrame.razor.cs:HandleChatSessionsChanged]
- The card preview loop is started and stopped with the card state and cancellation token. [DeviceCard.razor.cs:StartPreviewLoop] [DeviceCard.razor.cs:StopPreviewLoop]

## Side Effects
- Terminal actions send commands and completion requests through the circuit connection. [Terminal.razor.cs:EvaluateKeyDown] [Terminal.razor.cs:ShowQuickScripts]
- Components subscribe to and dispose messenger handlers as their lifecycle changes. [DeviceCard.razor.cs:Dispose] [ChatCard.razor.cs:OnInitializedAsync]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
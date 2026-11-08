# Services

## Overview

Provides Avalonia-backed UI services for dispatching, dialogs, clipboard access, chat, attended-access prompts, session indicators, and view-model creation.

## Interface

- `IUiDispatcher` exposes UI-thread invocation/posting, window display, clipboard and application access, shutdown, and classic/headless startup [Desktop.UI/Services/UiDispatcher.cs:IUiDispatcher].
- `IViewModelFactory` creates chat, file-transfer, host-name, and access-prompt view models [Desktop.UI/Services/ViewModelFactory.cs:IViewModelFactory].
- `IDialogProvider.Show` returns a `MessageBoxResult` for a message, caption, and `MessageBoxType` [Desktop.UI/Services/DialogProvider.cs:IDialogProvider].
- `ChatUiService`, `ClipboardService`, `RemoteControlAccessService`, and `SessionIndicator` implement the corresponding shared UI service interfaces [Desktop.UI/Services/ChatUiService.cs:ChatUiService; Desktop.UI/Services/ClipboardService.cs:ClipboardService; Desktop.UI/Services/RemoteControlAccessService.cs:RemoteControlAccessService; Desktop.UI/Services/SessionIndicator.cs:SessionIndicator].

## Invariants

- `UiDispatcher.Show` returns whether the window closed before its timeout and closes it when the timeout expires; `StartHeadless` reports failure if initialization does not complete within ten seconds [Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.Show; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartHeadless].
- `ClipboardService.BeginWatching` does not start a second watcher while its task is running, and the watcher uses the application-exiting token [Desktop.UI/Services/ClipboardService.cs:ClipboardService.BeginWatching; Desktop.UI/Services/ClipboardService.cs:ClipboardService.WatchClipboard].
- `RemoteControlAccessService.PromptForAccess` reports `TimedOut` when its one-minute prompt does not close, maps the view-model choice to `Accepted` or `Denied`, maps exceptions to `Error`, and decrements the open-prompt counter in `finally` [Desktop.UI/Services/RemoteControlAccessService.cs:RemoteControlAccessService.PromptForAccess]. This describes the UI prompt result flow.

## Side Effects

- `UiDispatcher` starts Avalonia application lifetimes, dispatches UI work, displays windows, cancels its application token at shutdown, and may call `Environment.Exit(0)` when classic shutdown cannot be completed [Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartClassicDesktop; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartHeadless; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.ShowWindow; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.Shutdown].
- `ClipboardService` polls the local clipboard every 500 ms, raises `ClipboardTextChanged` for changed nonempty text, and writes or clears clipboard text through Avalonia [Desktop.UI/Services/ClipboardService.cs:ClipboardService.WatchClipboard; Desktop.UI/Services/ClipboardService.cs:ClipboardService.SetText].
- `ChatUiService` displays the chat window and, for a disconnected chat message, shows a dialog then exits the process [Desktop.UI/Services/ChatUiService.cs:ChatUiService.ShowChatWindow; Desktop.UI/Services/ChatUiService.cs:ChatUiService.ReceiveChat].
- `RemoteControlAccessService` displays an attended-access prompt; `SessionIndicator.Show` displays the session indicator window [Desktop.UI/Services/RemoteControlAccessService.cs:RemoteControlAccessService.PromptForAccess; Desktop.UI/Services/SessionIndicator.cs:SessionIndicator.Show].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

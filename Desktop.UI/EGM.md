# Desktop UI

## Overview

`Desktop.UI` provides the Avalonia application, views, view models, and UI services used by the desktop clients. It supplies the dispatcher and consent-prompt implementations that the shared runtime consumes [Desktop.UI/App.axaml.cs:App; Desktop.UI/Startup/IServiceCollectionExtensions.cs:AddRemoteControlUi].

## Interface

- `IServiceCollectionExtensions.AddRemoteControlUi` registers `IUiDispatcher`, chat and clipboard services, session indicators, remote-control access prompts, view-model factories, and dialog services [Desktop.UI/Startup/IServiceCollectionExtensions.cs:AddRemoteControlUi].
- `IUiDispatcher` marshals work to Avalonia's UI thread, opens and closes windows, exposes the application clipboard, and starts classic or headless application lifetimes [Desktop.UI/Services/UiDispatcher.cs:IUiDispatcher].
- `App.OnFrameworkInitializationCompleted` selects `MainWindow` for the classic desktop lifetime or `MainView` for a single-view lifetime [Desktop.UI/App.axaml.cs:App.OnFrameworkInitializationCompleted].
- The views and view models cover the main remote-control UI, chat, file transfer, host-name prompts, attended-access prompts, and session indicators [Desktop.UI/Views/MainWindow.axaml.cs:MainWindow; Desktop.UI/Views/MainView.axaml.cs:MainView; Desktop.UI/Views/ChatWindow.axaml.cs:ChatWindow; Desktop.UI/Views/FileTransferWindow.axaml.cs:FileTransferWindow; Desktop.UI/ViewModels/PromptForAccessWindowViewModel.cs:PromptForAccessWindowViewModel; Desktop.UI/ViewModels/SessionIndicatorWindowViewModel.cs:SessionIndicatorWindowViewModel].

### Child contracts

- [Controls](./Controls/EGM.md) groups the dialog controls.
- [Services](./Services/EGM.md) provides dispatcher, clipboard, chat, prompt, and indicator services.
- [Startup](./Startup/EGM.md) registers the UI services and view models.
- [ViewModels](./ViewModels/EGM.md) holds observable UI state and commands; its [fake implementations](./ViewModels/Fakes/EGM.md) are documented separately.
- [Views](./Views/EGM.md) defines the main shell and desktop windows.

## Invariants

- `UiDispatcher.StartClassicDesktop` starts Avalonia with the classic desktop lifetime; `StartHeadless` starts the headless lifetime and returns a failed result if initialization does not complete within ten seconds [Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartClassicDesktop; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartHeadless].
- `RemoteControlAccessService.PromptForAccess` waits up to one minute for the attended-access prompt and returns Accepted, Denied, TimedOut, or Error [Desktop.UI/Services/RemoteControlAccessService.cs:RemoteControlAccessService.PromptForAccess].

## Side Effects

- `UiDispatcher` starts Avalonia lifetimes, dispatches work onto the UI thread, and creates or displays application windows [Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartClassicDesktop; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.StartHeadless; Desktop.UI/Services/UiDispatcher.cs:UiDispatcher.ShowWindow].
- `RemoteControlAccessService.PromptForAccess` displays an attended-access window and tracks open prompts [Desktop.UI/Services/RemoteControlAccessService.cs:RemoteControlAccessService.PromptForAccess].
- `ClipboardService` reads local clipboard updates while watching and writes text supplied by the remote-control client [Desktop.UI/Services/ClipboardService.cs:ClipboardService.BeginWatching; Desktop.UI/Services/ClipboardService.cs:ClipboardService.SetText].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

# ViewModels

## Overview

Contains observable UI state and commands for the desktop main view, branded windows, chat, file transfer, host-name entry, message boxes, attended-access prompts, and session indicators.

## Interface

- `IBrandedViewModelBase` and `BrandedViewModelBase` expose product and icon branding shared by branded windows [Desktop.UI/ViewModels/BrandedViewModelBase.cs:IBrandedViewModelBase; Desktop.UI/ViewModels/BrandedViewModelBase.cs:BrandedViewModelBase].
- The view-model contracts cover `IMainViewViewModel`, `IMainWindowViewModel`, `IChatWindowViewModel`, `IFileTransferWindowViewModel`, `IHostNamePromptViewModel`, `IMessageBoxViewModel`, `IPromptForAccessWindowViewModel`, and `ISessionIndicatorWindowViewModel` [Desktop.UI/ViewModels/MainViewViewModel.cs:IMainViewViewModel; Desktop.UI/ViewModels/MainWindowViewModel.cs:IMainWindowViewModel; Desktop.UI/ViewModels/ChatWindowViewModel.cs:IChatWindowViewModel; Desktop.UI/ViewModels/FileTransferWindowViewModel.cs:IFileTransferWindowViewModel; Desktop.UI/ViewModels/HostNamePromptViewModel.cs:IHostNamePromptViewModel; Desktop.UI/ViewModels/MessageBoxViewModel.cs:IMessageBoxViewModel; Desktop.UI/ViewModels/PromptForAccessWindowViewModel.cs:IPromptForAccessWindowViewModel; Desktop.UI/ViewModels/SessionIndicatorWindowViewModel.cs:ISessionIndicatorWindowViewModel].
- [Fakes](./Fakes/EGM.md) contains fake implementations of these UI contracts.

## Invariants

- `BrandedViewModelBase` caches `CurrentBranding` in a static field and applies its product name and icon while dispatching to the UI thread [Desktop.UI/ViewModels/BrandedViewModelBase.cs:BrandedViewModelBase.ApplyBrandingImpl].
- `MainViewViewModel.PromptForHostName` accepts only an absolute `http` or `https` URI before assigning `Host`; `Init` sets the app state host and attended mode after a host is available [Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.PromptForHostName; Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.Init].
- `ChatWindowViewModel.SendChatMessage` does nothing for blank input or a missing writer; otherwise it serializes one `ChatMessage`, writes and flushes it, then adds the local message to the collection [Desktop.UI/ViewModels/ChatWindowViewModel.cs:ChatWindowViewModel.SendChatMessage].
- `FileTransferWindowViewModel.RemoveFileUpload` removes the selected upload and cancels that upload's token; `UploadFile` adds its upload model on the UI dispatcher before calling the file-transfer service [Desktop.UI/ViewModels/FileTransferWindowViewModel.cs:FileTransferWindowViewModel.RemoveFileUpload; Desktop.UI/ViewModels/FileTransferWindowViewModel.cs:FileTransferWindowViewModel.UploadFile].
- `MainViewViewModel.ScreenCastRequested` displays a Yes/No dialog, starts screen casting for `Yes`, and sends a denial through the hub otherwise; this is the view-model's UI request flow [Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.ScreenCastRequested].

## Side Effects

- `MainViewViewModel.Init` may connect to the hub and then retrieves a session ID and sends attended-session information; on Linux it runs a `sudo bash -c` command that invokes `apt-get` to install UI dependencies [Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.Init; Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.GetSessionID; Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.InstallDependencies].
- `MainViewViewModel.CopyLink` writes the host and current session ID URL to the local clipboard [Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.CopyLink].
- `ChatWindowViewModel.SendChatMessage` writes serialized chat messages to its supplied `StreamWriter`; `FileTransferWindowViewModel.UploadFile` calls the injected file-transfer service [Desktop.UI/ViewModels/ChatWindowViewModel.cs:ChatWindowViewModel.SendChatMessage; Desktop.UI/ViewModels/FileTransferWindowViewModel.cs:FileTransferWindowViewModel.UploadFile].
- `SessionIndicatorWindowViewModel.PromptForExit` shows a confirmation dialog and invokes `IShutdownService.Shutdown` only when the dialog result is `Yes` [Desktop.UI/ViewModels/SessionIndicatorWindowViewModel.cs:SessionIndicatorWindowViewModel.PromptForExit].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

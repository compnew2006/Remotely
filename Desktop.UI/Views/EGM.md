# Views

## Overview

Defines the classic desktop window and the single-view shell, along with chat, file-transfer, host-name, access-prompt, and session-indicator windows.

## Interface

- `MainView` obtains `IMainViewViewModel` outside design mode, copies list selection into `SelectedViewers`, and calls `Init` when loaded [Desktop.UI/Views/MainView.axaml.cs:MainView].
- `MainWindow` resolves `IMainWindowViewModel` outside design mode and asks the dispatcher to shut down when closed [Desktop.UI/Views/MainWindow.axaml.cs:MainWindow].
- `ChatWindow`, `FileTransferWindow`, `HostNamePrompt`, `PromptForAccessWindow`, and `SessionIndicatorWindow` are Avalonia windows with corresponding `.axaml` layouts [Desktop.UI/Views/ChatWindow.axaml.cs:ChatWindow; Desktop.UI/Views/FileTransferWindow.axaml.cs:FileTransferWindow; Desktop.UI/Views/HostNamePrompt.axaml.cs:HostNamePrompt; Desktop.UI/Views/PromptForAccessWindow.axaml.cs:PromptForAccessWindow; Desktop.UI/Views/SessionIndicatorWindow.axaml.cs:SessionIndicatorWindow].

## Invariants

- The main view resolves and invokes its view model only when its data context has the expected type; its constructor skips service lookup in design mode [Desktop.UI/Views/MainView.axaml.cs:MainView.MainView; Desktop.UI/Views/MainView.axaml.cs:MainView.MainView_Loaded].
- Closing `SessionIndicatorWindow` is cancelled outside design mode when it has the expected view model, which is then asked to handle the exit prompt [Desktop.UI/Views/SessionIndicatorWindow.axaml.cs:SessionIndicatorWindow.SessionIndicatorWindow_Closing].

## Side Effects

- Closing `ChatWindow` calls `Environment.Exit(0)`; closing `MainWindow` requests dispatcher shutdown [Desktop.UI/Views/ChatWindow.axaml.cs:ChatWindow.ChatWindow_Closed; Desktop.UI/Views/MainWindow.axaml.cs:MainWindow.MainWindow_Closed].
- Loading `MainView` invokes `MainViewViewModel.Init`; that method's hub and dependency-install effects are described in [../ViewModels/EGM.md](../ViewModels/EGM.md) [Desktop.UI/Views/MainView.axaml.cs:MainView.MainView_Loaded; Desktop.UI/ViewModels/MainViewViewModel.cs:MainViewViewModel.Init].
- Opening the file-transfer or session-indicator windows positions them at the bottom-right of the primary working area when a primary screen is available [Desktop.UI/Views/FileTransferWindow.axaml.cs:FileTransferWindow.FileTransferWindow_Opened; Desktop.UI/Views/SessionIndicatorWindow.axaml.cs:SessionIndicatorWindow.SessionIndicatorWindow_Opened].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

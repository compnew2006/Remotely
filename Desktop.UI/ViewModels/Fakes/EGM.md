# Fakes

## Overview

Provides fake implementations of the desktop UI view-model interfaces with sample values and inert commands or methods.

## Interface

- `FakeBrandedViewModelBase` implements `IBrandedViewModelBase`; the chat, file-transfer, host-name, main-view, main-window, message-box, access-prompt, and session-indicator fakes implement their matching view-model contracts [Desktop.UI/ViewModels/Fakes/FakeBrandedViewModelBase.cs:FakeBrandedViewModelBase; Desktop.UI/ViewModels/Fakes/FakeChatWindowViewModel.cs:FakeChatWindowViewModel; Desktop.UI/ViewModels/Fakes/FakeFileTransferViewModel.cs:FakeFileTransferViewModel; Desktop.UI/ViewModels/Fakes/FakeHostNamePromptViewModel.cs:FakeHostNamePromptViewModel; Desktop.UI/ViewModels/Fakes/FakeMainViewViewModel.cs:FakeMainViewViewModel; Desktop.UI/ViewModels/Fakes/FakeMainWindowViewModel.cs:FakeMainWindowViewModel; Desktop.UI/ViewModels/Fakes/FakeMessageBoxViewModel.cs:FakeMessageBoxViewModel; Desktop.UI/ViewModels/Fakes/FakePromptForAccessViewModel.cs:FakePromptForAccessViewModel; Desktop.UI/ViewModels/Fakes/FakeSessionIndicatorWindowViewModel.cs:FakeSessionIndicatorWindowViewModel].
- `FakeChatWindowViewModel` and `FakeMainViewViewModel` supply example chat/viewer data; other fakes expose representative prompt, message-box, branding, or empty file-transfer state [Desktop.UI/ViewModels/Fakes/FakeChatWindowViewModel.cs:FakeChatWindowViewModel; Desktop.UI/ViewModels/Fakes/FakeMainViewViewModel.cs:FakeMainViewViewModel; Desktop.UI/ViewModels/Fakes/FakeMessageBoxViewModel.cs:FakeMessageBoxViewModel; Desktop.UI/ViewModels/Fakes/FakePromptForAccessViewModel.cs:FakePromptForAccessViewModel; Desktop.UI/ViewModels/Fakes/FakeFileTransferViewModel.cs:FakeFileTransferViewModel].

## Invariants

- Most fake commands are inert and asynchronous methods return completed tasks; `FakeMainViewViewModel.ChangeServer` is an exception and throws `NotImplementedException` [Desktop.UI/ViewModels/Fakes/FakeChatWindowViewModel.cs:FakeChatWindowViewModel.SendChatMessage; Desktop.UI/ViewModels/Fakes/FakeFileTransferViewModel.cs:FakeFileTransferViewModel.UploadFile; Desktop.UI/ViewModels/Fakes/FakeMainViewViewModel.cs:FakeMainViewViewModel.Init; Desktop.UI/ViewModels/Fakes/FakeMainViewViewModel.cs:FakeMainViewViewModel.ChangeServer; Desktop.UI/ViewModels/Fakes/FakeSessionIndicatorWindowViewModel.cs:FakeSessionIndicatorWindowViewModel.PromptForExit].
- Fake property setters that are intentionally inert do not update the sample values, while the fake main-view and message-box models expose preselected demonstration state [Desktop.UI/ViewModels/Fakes/FakeChatWindowViewModel.cs:FakeChatWindowViewModel; Desktop.UI/ViewModels/Fakes/FakeMessageBoxViewModel.cs:FakeMessageBoxViewModel; Desktop.UI/ViewModels/Fakes/FakeMainViewViewModel.cs:FakeMainViewViewModel].

## Side Effects

- `FakeBrandedViewModelBase` reads the embedded default icon resource into a bitmap; if that fails, it writes the exception message to `Debug` and returns `null` [Desktop.UI/ViewModels/Fakes/FakeBrandedViewModelBase.cs:FakeBrandedViewModelBase.GetBitmapImageIcon].
- The fake classes do not call production network, clipboard, file-transfer, or shutdown services; `FakeMainViewViewModel.ChangeServer` throws `NotImplementedException` [Desktop.UI/ViewModels/Fakes/FakeChatWindowViewModel.cs:FakeChatWindowViewModel; Desktop.UI/ViewModels/Fakes/FakeFileTransferViewModel.cs:FakeFileTransferViewModel; Desktop.UI/ViewModels/Fakes/FakeMainViewViewModel.cs:FakeMainViewViewModel.ChangeServer; Desktop.UI/ViewModels/Fakes/FakeSessionIndicatorWindowViewModel.cs:FakeSessionIndicatorWindowViewModel].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

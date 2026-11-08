# Dialogs

## Overview

Contains the Avalonia message-box dialog and its result and button-type enums.

## Interface

- `MessageBox.Show(message, caption, type)` resolves the dispatcher and transient message-box view model, displays the dialog, and returns the view model result [Desktop.UI/Controls/Dialogs/MessageBox.axaml.cs:MessageBox.Show].
- `MessageBoxType` selects `OK` or `YesNo`; `MessageBoxResult` represents `Cancel`, `OK`, `Yes`, or `No` [Desktop.UI/Controls/Dialogs/MessageBoxType.cs:MessageBoxType; Desktop.UI/Controls/Dialogs/MessageBoxResult.cs:MessageBoxResult].

## Invariants

- `Show` sets the caption and message before opening the modal dialog, and exposes the button visibility that matches the requested type [Desktop.UI/Controls/Dialogs/MessageBox.axaml.cs:MessageBox.Show].
- The result starts as `Cancel`; the message-box view-model commands set the selected result before closing the window [Desktop.UI/ViewModels/MessageBoxViewModel.cs:MessageBoxViewModel.Result; Desktop.UI/ViewModels/MessageBoxViewModel.cs:MessageBoxViewModel.YesCommand; Desktop.UI/ViewModels/MessageBoxViewModel.cs:MessageBoxViewModel.NoCommand; Desktop.UI/ViewModels/MessageBoxViewModel.cs:MessageBoxViewModel.OKCommand].

## Side Effects

- `Show` accesses `StaticServiceProvider`, resolves services, creates a window, and displays it through `IUiDispatcher.ShowDialog` [Desktop.UI/Controls/Dialogs/MessageBox.axaml.cs:MessageBox.Show; Desktop.UI/Services/UiDispatcher.cs:IUiDispatcher.ShowDialog].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

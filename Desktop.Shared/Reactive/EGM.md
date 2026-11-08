# Desktop Reactive Helpers

## Overview

This directory provides `INotifyPropertyChanged` support and `ICommand` wrappers for desktop view models [Desktop.Shared/Reactive/ObservableObject.cs:ObservableObject; Desktop.Shared/Reactive/RelayCommand.cs:RelayCommand; Desktop.Shared/Reactive/AsyncRelayCommand.cs:AsyncRelayCommand].

## Interface

- `ObservableObject` supplies property storage, getters, setters, and `PropertyChanged` notifications [Desktop.Shared/Reactive/ObservableObject.cs:ObservableObject.Get/Set/NotifyPropertyChanged].
- `RelayCommand` and `AsyncRelayCommand` wrap synchronous or asynchronous delegates as `ICommand`, with optional `CanExecute` predicates; generic variants pass a typed parameter [Desktop.Shared/Reactive/RelayCommand.cs:RelayCommand; Desktop.Shared/Reactive/AsyncRelayCommand.cs:AsyncRelayCommand].

## Invariants

- `ObservableObject.Set` stores the new value and raises `PropertyChanged` on each call; it does not compare the old and new values [Desktop.Shared/Reactive/ObservableObject.cs:ObservableObject.Set].
- Generic command wrappers accept null as the default parameter and throw `InvalidOperationException` for a non-null parameter of the wrong type [Desktop.Shared/Reactive/RelayCommand.cs:RelayCommand<T>.Execute; Desktop.Shared/Reactive/AsyncRelayCommand.cs:AsyncRelayCommand<T>.Execute].
- `AsyncRelayCommand.Execute` is `async void` to satisfy `ICommand.Execute` [Desktop.Shared/Reactive/AsyncRelayCommand.cs:AsyncRelayCommand.Execute].

## Side Effects

- Setters and command methods raise events or invoke the supplied delegates [Desktop.Shared/Reactive/ObservableObject.cs:ObservableObject.Set; Desktop.Shared/Reactive/RelayCommand.cs:RelayCommand.Execute; Desktop.Shared/Reactive/AsyncRelayCommand.cs:AsyncRelayCommand.Execute].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

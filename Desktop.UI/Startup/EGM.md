# Startup

## Overview

Registers the Avalonia desktop UI services and view models with the application's dependency-injection container.

## Interface

- `IServiceCollectionExtensions.AddRemoteControlUi` adds UI dispatcher, chat, clipboard, session-indicator, access-prompt, view-model-factory, main-window/main-view, session-indicator view-model, message-box view-model, and dialog-provider registrations [Desktop.UI/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlUi].

## Invariants

- The registrations are singletons except `IMessageBoxViewModel`, which is registered as transient [Desktop.UI/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlUi].

## Side Effects

- Calling `AddRemoteControlUi` mutates the supplied `IServiceCollection` by adding these service registrations [Desktop.UI/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlUi].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Desktop.UI/Desktop.UI.csproj`

# Linux Desktop Client

## Overview

The Linux executable composes the shared remote-control client with Linux X11 services and Avalonia UI services.

## Interface

- Program.Main loads embedded server data, resolves command-line organization settings, and starts the remote-control client [Desktop.Linux/Program.cs:Program.Main].
- Child packages: [Services](./Services/EGM.md), [Startup](./Startup/EGM.md).

## Invariants

- Program.Main applies the embedded server URL and organization first, then allows the org-id argument to override the organization [Desktop.Linux/Program.cs:Program.Main].
- AppStartup starts screen casting only for Unattended mode and starts the chat host only for Chat mode [Desktop.Linux/Services/AppStartup.cs:AppStartup.Run].

## Side Effects

- Program.Main configures file logging, builds a service provider, and starts the Avalonia client [Desktop.Linux/Program.cs:Program.Main].
- AppStartup connects the desktop hub and reports unattended session details to the server [Desktop.Linux/Services/AppStartup.cs:AppStartup.StartScreenCasting].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln

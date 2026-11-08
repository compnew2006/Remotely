# Agent

## Overview

The agent is the background client that registers a device with the server, maintains a hub connection, and delegates scripting and desktop operations to platform-specific services.

## Interface

- Program.Main builds and runs the host; RegisterServices wires shared and OS-specific implementations [Agent/Program.cs:Program.RegisterServices].
- Agent hub operations and command handling are indexed in [Services](./Services/EGM.md); platform launch and update boundaries are indexed under its Linux, macOS, and Windows child contracts.
- Child packages: [Extensions](./Extensions/EGM.md), [Interfaces](./Interfaces/EGM.md), [Models](./Models/EGM.md), [Services](./Services/EGM.md).

## Invariants

- Program.Init connects the agent hub before beginning update checks so registration and trusted-key retrieval happen first [Agent/Program.cs:Program.Init].
- RegisterServices selects Windows, Linux, or macOS implementations and throws for other operating systems [Agent/Program.cs:Program.RegisterServices].

## Side Effects

- Main starts the generic host and waits for shutdown [Agent/Program.cs:Program.Main].
- Init changes the current working directory to the executable directory and, on Windows, may set the Secure Attention Sequence registry policy when elevated [Agent/Program.cs:Program.SetWorkingDirectory].
- Logging is configured with console, debug, and file providers [Agent/Program.cs:Program.RegisterServices].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln

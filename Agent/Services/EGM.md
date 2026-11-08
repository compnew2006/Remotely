# Agent Services

## Overview

Agent services connect to the server, handle remote commands, manage shells and logs, and delegate platform operations.

## Interface

- AgentHubConnection implements IAgentHubConnection and registers incoming server handlers [Agent/Services/AgentHubConnection.cs:AgentHubConnection.RegisterMessageHandlers].
- ScriptExecutor runs terminal, API, and saved-script commands and returns results [Agent/Services/ScriptExecutor.cs:ScriptExecutor].
- ScriptingShellFactory caches shell instances by session and expires them after inactivity [Agent/Services/ScriptingShellFactory.cs:ScriptingShellFactory.GetEntryOptions].
- Child packages: [Linux](./Linux/EGM.md), [macOS](./MacOS/EGM.md), [Windows](./Windows/EGM.md).

## Invariants

- AgentHubConnection handlers that execute remote commands, launch remote control, or transfer files require server verification first [Agent/Services/AgentHubConnection.cs:AgentHubConnection.VerifyServer].
- Connect registers the device and verifies the server before fetching update keys and starting pending work [Agent/Services/AgentHubConnection.cs:AgentHubConnection.Connect].
- ScriptingShellFactory reuses a shell only while it is not disposed [Agent/Services/ScriptingShellFactory.cs:ScriptingShellFactory.GetOrCreatePsCoreShell].

## Side Effects

- AgentHubConnection opens the SignalR service hub, sends heartbeats, and writes the received trusted update keys beside the executable [Agent/Services/AgentHubConnection.cs:AgentHubConnection.FetchUpdatePublicKey].
- ScriptExecutor executes shell commands and posts script results to the configured server [Agent/Services/ScriptExecutor.cs:ScriptExecutor.SendResultsToApi].
- FileLogsManager reads and deletes the agent log directory under the shared FileLogger path [Agent/Services/FileLogsManager.cs:FileLogsManager.DeleteLogs].
- AgentHubConnection downloads shared files into C:\RemotelySharedFiles on Windows or a temporary RemotelySharedFiles directory elsewhere [Agent/Services/AgentHubConnection.cs:AgentHubConnection.TransferFileFromBrowserToAgent].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln

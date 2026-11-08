# Viewer Client Models

## Overview
TypeScript model objects and interfaces used for viewer state, coordinates, connection metadata, and user options.

## Interface
- StreamingState and HubConnection represent client-side streaming and SignalR state. [StreamingState.ts:StreamingState] [HubConnection.ts:HubConnection]
- RemoteControlTarget, WindowsSession, Point, CursorInfo, and Result represent target and UI data. [RemoteControlTarget.ts:RemoteControlTarget] [WindowsSession.ts:WindowsSession] [Point.ts:Point]
- UserOptions carries browser-side user preferences. [UserOptions.ts:UserOptions]

## Invariants
- Result<T> carries a success flag and optional result or error fields. [Result.ts:Result]
- WindowsSession and RemoteControlTarget retain the IDs required by viewer session selection. [WindowsSession.ts:WindowsSession] [RemoteControlTarget.ts:RemoteControlTarget]

## Side Effects
- These model definitions hold browser state; network and DOM effects are handled by the client services. [StreamingState.ts:StreamingState]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
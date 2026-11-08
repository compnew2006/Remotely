# Agent Models

## Overview

Small state objects passed between agent services.

## Interface

- ChatSession carries a process ID and optional named-pipe client stream [Agent/Models/ChatSession.cs:ChatSession].
- DesktopProcessLaunchInfo carries process ID, target session, role, executable path, arguments, and optional start time [Agent/Models/DesktopProcessLaunchInfo.cs:DesktopProcessLaunchInfo].

## Invariants

- DesktopProcessLaunchInfo defaults role, path, and arguments to empty strings; ProcessStartTime may be absent [Agent/Models/DesktopProcessLaunchInfo.cs:DesktopProcessLaunchInfo].
- ChatSession.PipeStream is nullable, so consumers must handle a missing stream [Agent/Models/ChatSession.cs:ChatSession.PipeStream].

## Side Effects

- These model types only hold state; process and pipe operations are performed by services [Agent/Models/DesktopProcessLaunchInfo.cs:DesktopProcessLaunchInfo].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln

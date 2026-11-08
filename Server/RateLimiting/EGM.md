# Server Rate Limiting

## Overview
Named policies used by ASP.NET Core rate-limiter registration.

## Interface
- PolicyNames.AgentUpdateDownloads identifies the concurrent agent package-download policy. [PolicyNames.cs:AgentUpdateDownloads]

## Invariants
- The policy identifier is a stable string derived from the constant name. [PolicyNames.cs:AgentUpdateDownloads]

## Side Effects
- The named policy is applied by Program startup to agent update downloads. [PolicyNames.cs:AgentUpdateDownloads] [../Program.cs:AgentUpdateDownloads]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
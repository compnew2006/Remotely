# Server Options

## Overview
Typed server configuration used by startup and provider selection.

## Interface
- ApplicationOptions exposes the ApplicationOptions section key, DbProvider, AllowDbAutoMigration, and DockerGatewayIp. [ApplicationOptions.cs:ApplicationOptions]

## Invariants
- DbProvider defaults to SQLite and AllowDbAutoMigration defaults to true. [ApplicationOptions.cs:DbProvider] [ApplicationOptions.cs:AllowDbAutoMigration]
- When automatic migration is disabled, startup checks for pending migrations rather than silently applying them. [ApplicationOptions.cs:AllowDbAutoMigration]

## Side Effects
- These options control database provider selection, startup migration behavior, and the configured Docker gateway proxy. [ApplicationOptions.cs:DbProvider] [ApplicationOptions.cs:DockerGatewayIp]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
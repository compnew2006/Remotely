# Server Extensions

## Overview
Extension methods for database settings, application pipeline registration, request headers, and query composition.

## Interface
- UseRemoteControlServer configures the remote-control server middleware and routes. [IApplicationBuilderExtensions.cs:UseRemoteControlServer]
- GetAppSettings retrieves persisted application settings. [AppDbExtensions.cs:GetAppSettings]
- TryGetOrganizationId reads an organization identifier from request headers. [IHeaderDictionaryExtensions.cs:TryGetOrganizationId]
- Apply conditionally applies a query-builder action to an IQueryable. [IQueryableExtensions.cs:Apply]

## Invariants
- TryGetOrganizationId reports whether a non-empty organization ID was found and returns a nullable output. [IHeaderDictionaryExtensions.cs:TryGetOrganizationId]
- Apply preserves the original query when no query-builder action is supplied. [IQueryableExtensions.cs:Apply]

## Side Effects
- UseRemoteControlServer adds middleware and endpoint configuration to the application builder. [IApplicationBuilderExtensions.cs:UseRemoteControlServer]
- GetAppSettings reads and may initialize persisted settings. [AppDbExtensions.cs:GetAppSettings]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
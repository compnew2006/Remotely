# Viewer Filters

## Overview
MVC authorization for the browser remote-control viewer page.

## Interface
- ViewerAuthorizationFilter checks the server’s remote-control authentication setting, the authenticated principal, and an OTP query parameter. [ViewerAuthorizationFilter.cs:IsAuthorized]

## Invariants
- When remote-control authentication is required, access is allowed only for an authenticated user or an existing OTP. [ViewerAuthorizationFilter.cs:IsAuthorized]
- An authorization failure redirects to the account login route. [ViewerAuthorizationFilter.cs:OnAuthorizationAsync]

## Side Effects
- Filter evaluation reads server settings and may set an MVC redirect result. [ViewerAuthorizationFilter.cs:OnAuthorizationAsync]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
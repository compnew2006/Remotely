# Viewer Pages

## Overview
Razor Pages used to authorize and render the remote-control viewer shell.

## Interface
- ViewerModel supplies viewer page metadata, theme, logo URL, and display name. [Viewer.cshtml.cs:ViewerModel] [Viewer.cshtml.cs:OnGet]

## Invariants
- The viewer page is protected by ViewerAuthorizationFilter. [Viewer.cshtml.cs:ViewerAuthorizationFilter]

## Side Effects
- OnGet loads display information used by the viewer page response. [Viewer.cshtml.cs:OnGet]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
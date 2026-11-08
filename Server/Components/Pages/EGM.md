# Application Pages

## Overview
Blazor pages for device operations, organization and server administration, downloads, user settings, and informational routes.

## Interface
- DeviceDetails, ManageOrganization, ServerConfig, and ServerLogs provide device, organization, server configuration, and log views. [DeviceDetails.razor.cs:OnInitializedAsync] [ManageOrganization.razor.cs:RefreshData] [ServerConfig.razor.cs:HandleValidSubmit] [ServerLogs.razor:RefreshLogs]
- ApiKeys, Branding, Deploy, Downloads, ScriptsPage, and UserOptions provide management workflows. [ApiKeys.razor:CreateNewKey] [Branding.razor:HandleValidSubmit] [Deploy.razor:SetScriptContent] [ScriptsPage.razor.cs:RefreshScripts]
- Informational and routing pages include About, Credits, Error, GetSupport, Index, and Invite. [About.razor:About] [Error.razor:OnInitialized] [Invite.razor:Confirm]

## Invariants
- Administrative forms submit through their page handlers and validate the associated EditContext. [ServerConfig.razor.cs:HandleValidSubmit] [Branding.razor:HandleValidSubmit]
- Device and organization pages use application services for data access. [DeviceDetails.razor.cs:OnInitializedAsync] [ManageOrganization.razor.cs:OnInitializedAsync]

## Side Effects
- Pages create, rename, or delete organization, API key, and script records through services. [ManageOrganization.razor.cs:CreateNewDeviceGroup] [ApiKeys.razor:DeleteKey] [ScriptsPage.razor.cs:RefreshScripts]
- Deploy and Downloads pages produce installer links or copy scripts to the clipboard. [Deploy.razor:CopyScriptToClipboard] [Downloads.razor:CopyScript]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
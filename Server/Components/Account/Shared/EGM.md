# Account Shared Components

## Overview
Shared Blazor layout and status components used by Identity account pages.

## Interface
- AccountLayout and ManageLayout provide the outer account and management shells. [AccountLayout.razor:OnParametersSet] [ManageLayout.razor:ManageNavMenu]
- ManageNavMenu, ExternalLoginPicker, RedirectToLogin, ShowRecoveryCodes, and StatusMessage are reusable account UI pieces. [ManageNavMenu.razor:OnInitializedAsync] [ExternalLoginPicker.razor:GetExternalAuthenticationSchemesAsync] [ShowRecoveryCodes.razor:RecoveryCodes]

## Invariants
- RedirectToLogin navigates through the account login route when its component is rendered. [RedirectToLogin.razor:NavigateTo]
- StatusMessage renders a supplied message and status class without persisting account state. [StatusMessage.razor:StatusMessage]

## Side Effects
- ExternalLoginPicker invokes the selected external provider flow. [ExternalLoginPicker.razor:Account/PerformExternalLogin]
- Shared layouts render child content and navigation links for the account route family. [AccountLayout.razor:Body] [ManageLayout.razor:Body]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
# Account Management Pages

## Overview
Blazor Identity pages for managing a signed-in user’s credentials, personal data, external logins, and two-factor settings.

## Interface
- Password and email workflows are implemented by ChangePassword, SetPassword, and Email. [ChangePassword.razor:ChangePassword] [SetPassword.razor:SetPassword] [Email.razor:Email]
- Two-factor setup and recovery workflows are implemented by EnableAuthenticator, Disable2fa, ResetAuthenticator, TwoFactorAuthentication, and GenerateRecoveryCodes. [EnableAuthenticator.razor:EnableAuthenticator] [TwoFactorAuthentication.razor:TwoFactorAuthentication]
- Profile and account data pages include Index, PersonalData, DeletePersonalData, and ExternalLogins. [Index.razor:OnValidSubmitAsync] [PersonalData.razor:PersonalData]

## Invariants
- Account changes run through ASP.NET Core Identity page handlers and their bound forms. [ChangePassword.razor:OnValidSubmitAsync] [EnableAuthenticator.razor:OnValidSubmitAsync]

## Side Effects
- Handlers update Identity credentials, authenticator state, recovery codes, or user data through the injected Identity services. [ChangePassword.razor:OnValidSubmitAsync] [DeletePersonalData.razor:OnValidSubmitAsync]
- External login pages add or remove provider logins through Identity. [ExternalLogins.razor:OnGetLinkLoginCallbackAsync]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
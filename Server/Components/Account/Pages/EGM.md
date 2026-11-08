# Account Pages

## Overview
Blazor Identity routes for registration, sign-in, confirmation, password reset, lockout, and two-factor challenges.

## Interface
- Sign-in and registration pages include Login, Register, LoginWith2fa, and LoginWithRecoveryCode. [Login.razor:Login] [Register.razor:Register] [LoginWith2fa.razor:LoginWith2fa]
- Email confirmation and reset flows include ConfirmEmail, ForgotPassword, ResetPassword, and their confirmation pages. [ConfirmEmail.razor:ConfirmEmail] [ForgotPassword.razor:ForgotPassword] [ResetPassword.razor:ResetPassword]
- Account administration pages are documented in [Manage](./Manage/EGM.md). [Manage/EGM.md:Account Management Pages]

## Invariants
- Login, registration, and recovery actions submit through their component form handlers and Identity services. [Login.razor:OnValidSubmit] [Register.razor:OnValidSubmit]

## Side Effects
- Successful authentication and registration actions change Identity state and may redirect to a return URL. [Login.razor:LoginUser] [Register.razor:RegisterUser]
- Confirmation and reset pages consume Identity tokens from route or form input. [ConfirmEmail.razor:OnInitializedAsync] [ResetPassword.razor:OnValidSubmitAsync]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
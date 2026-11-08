# Account Components

## Overview
ASP.NET Core Identity support types and Blazor account pages used for authentication and profile management.

## Interface
- IdentityComponentsEndpointRouteBuilderExtensions maps the additional Identity endpoints. [IdentityComponentsEndpointRouteBuilderExtensions.cs:MapAdditionalIdentityEndpoints]
- IdentityUserAccessor, IdentityRedirectManager, IdentityRevalidatingAuthenticationStateProvider, and IdentityNoOpEmailSender adapt Identity services to the component layer. [IdentityUserAccessor.cs:IdentityUserAccessor] [IdentityRedirectManager.cs:IdentityRedirectManager]
- Sign-in and registration screens are listed in [Pages](./Pages/EGM.md); shared shells are in [Shared](./Shared/EGM.md). [Pages/EGM.md:Account Pages] [Shared/EGM.md:Account Shared Components]

## Invariants
- The component authentication state provider periodically revalidates the signed-in Identity principal. [IdentityRevalidatingAuthenticationStateProvider.cs:ValidateAuthenticationStateAsync]
- IdentityNoOpEmailSender deliberately completes email sends without delivery. [IdentityNoOpEmailSender.cs:SendEmailAsync]

## Side Effects
- Endpoint mapping adds Identity account routes to the application. [IdentityComponentsEndpointRouteBuilderExtensions.cs:MapAdditionalIdentityEndpoints]
- IdentityRedirectManager redirects account flows to local destinations. [IdentityRedirectManager.cs:RedirectTo]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
# Authentication and Authorization

## Overview
MVC filters and authorization requirements gate API calls, local-only routes, expiring-token routes, and administrator operations.

## Interface
- ApiAuthorizationFilter authorizes API calls through an administrator identity or API key. [ApiAuthorizationFilter.cs:IsAuthorizedAsync]
- ExpiringTokenFilter checks authenticated users, API keys, and expiring tokens. [ExpiringTokenFilter.cs:Authorize]
- LocalOnlyFilter restricts a request to loopback addresses. [LocalOnlyFilter.cs:OnAuthorization]
- OrganizationAdminRequirement, ServerAdminRequirement, and TwoFactorRequiredRequirement define authorization requirements and their handlers. [OrganizationAdminRequirement.cs:OrganizationAdminRequirement] [ServerAdminRequirementHandler.cs:HandleRequirementAsync] [TwoFactorRequiredHandler.cs:HandleRequirementAsync]

## Invariants
- API-key authorization fails closed on malformed credentials or authorization exceptions. [ApiAuthorizationFilter.cs:IsAuthorizedAsync]
- Expiring-token authorization rejects expired or unrecognized tokens. [ExpiringTokenFilter.cs:Authorize]
- Loopback access is required by LocalOnlyFilter. [LocalOnlyFilter.cs:OnAuthorization]

## Side Effects
- Successful API authorization writes the resolved organization ID to the request header; denial sets an unauthorized result. [ApiAuthorizationFilter.cs:OnAuthorizationAsync]
- ExpiringTokenFilter writes the organization header for authenticated users or API keys and assigns an unauthorized result on failure. [ExpiringTokenFilter.cs:Authorize]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
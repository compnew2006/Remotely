# Layout Components

## Overview
Application shell and navigation menu for the Blazor server UI.

## Interface
- MainLayout hosts page content and shared layout state. [MainLayout.razor:AuthorizeView]
- NavMenu renders primary navigation entries. [NavMenu.razor:NavMenu]

## Invariants
- Navigation visibility follows the component’s authorization checks. [MainLayout.razor:AuthorizeView]

## Side Effects
- Navigation links change the current Blazor route. [NavMenu.razor:NavLink]
- MainLayout renders the shared alert, toast, and modal hosts. [MainLayout.razor:ModalHarness]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
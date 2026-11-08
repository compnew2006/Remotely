# Tab Control Components

## Overview
Small reusable tab, tab-header, and tab-content components for composing tabbed Blazor views.

## Interface
- TabControl coordinates tab selection; TabHeader and TabContent render a header and selected content. [TabControl.razor:SetActiveTab] [TabHeader.razor:TabHeader] [TabContent.razor:TabContent]

## Invariants
- The parent tab control owns selected-tab state used by its headers and content. [TabControl.razor:ActiveTab]

## Side Effects
- Selecting a header updates the active tab state in the component tree. [TabHeader.razor:SetActiveTab]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
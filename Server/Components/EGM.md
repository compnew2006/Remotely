# Server Components

## Overview
Blazor application components, shared UI hosts, account workflows, device controls, administration pages, and reusable widgets.

## Interface
- App and Routes define the interactive application entry and route table. [App.razor:OnInitializedAsync] [Routes.razor:Router]
- AuthComponentBase and MessengerSubscriber provide authentication and message subscription support. [AuthComponentBase.cs:AuthComponentBase] [MessengerSubscriber.cs:MessengerSubscriber]
- Account, device, layout, modal, page, script, tab, and tree components are documented in [Account](./Account/EGM.md), [Devices](./Devices/EGM.md), [Layout](./Layout/EGM.md), [ModalContents](./ModalContents/EGM.md), [Pages](./Pages/EGM.md), [Scripts](./Scripts/EGM.md), [TabControl](./TabControl/EGM.md), and [TreeView](./TreeView/EGM.md). [App.razor:OnInitializedAsync]

## Invariants
- Routes are resolved by Router and RouteView, which uses MainLayout as the default layout. [Routes.razor:Router] [Routes.razor:RouteView] [Routes.razor:MainLayout]
- Modal, toast, alert, and loading harnesses host messages for the active circuit. [ModalHarness.razor:ModalHarness] [ToastHarness.razor:OnAfterRenderAsync]

## Side Effects
- MessengerSubscriber registers and unregisters typed messenger handlers. [MessengerSubscriber.cs:Subscribe] [MessengerSubscriber.cs:Dispose]
- Components invoke server services, circuit actions, or browser interop in their event handlers. [ColorPicker.razor:ColorChanged] [FileInputButton.razor:OnChange]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
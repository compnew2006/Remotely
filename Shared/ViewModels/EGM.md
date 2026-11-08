# ViewModels

## Overview

Small view state models for chat, terminal output, invitations, organization users, and property-change binding.

## Interface

- ChatSession holds chat history in a ConcurrentQueue and tracks device and expansion state [Shared/ViewModels/ChatSession.cs:ChatSession].
- ChatHistoryItem maps ChatHistoryItemOrigin to a CSS class name [Shared/ViewModels/ChatHistoryItem.cs:ClassName].
- ViewModelBase implements INotifyPropertyChanged and IInvokePropertyChanged with protected Get/Set helpers [Shared/ViewModels/ViewModelBase.cs:ViewModelBase.Get/Set].
- InviteViewModel, OrganizationUser, and TerminalLineItem carry UI-facing data [Shared/ViewModels/InviteViewModel.cs:InviteViewModel; Shared/ViewModels/OrganizationUser.cs:OrganizationUser; Shared/ViewModels/TerminalLineItem.cs:TerminalLineItem].

## Invariants

- ViewModelBase.Set returns false and raises no notification when the value equals the stored value; otherwise it stores the value and raises PropertyChanged [Shared/ViewModels/ViewModelBase.cs:Set/OnPropertyChanged].
- ViewModelBase.Get and Set require a nonblank property name [Shared/ViewModels/ViewModelBase.cs:Get/Set].
- ChatSession.SessionId is generated once per instance [Shared/ViewModels/ChatSession.cs:SessionId].

## Side Effects

- ViewModelBase.InvokePropertyChanged and Set raise PropertyChanged events [Shared/ViewModels/ViewModelBase.cs:InvokePropertyChanged/OnPropertyChanged].
- ChatSession.ChatHistory is an in-memory concurrent queue [Shared/ViewModels/ChatSession.cs:ChatHistory].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


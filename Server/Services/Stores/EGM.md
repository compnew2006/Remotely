# Circuit Stores

## Overview
Circuit-scoped in-memory stores for chat sessions, selected device cards, and terminal history.

## Interface
- IChatSessionStore manages per-device ChatSession entries. [ChatSessionStore.cs:IChatSessionStore]
- ISelectedCardsStore tracks selected device IDs and raises selection changes. [SelectedCardsStore.cs:ISelectedCardsStore]
- ITerminalStore stores terminal lines and command history. [TerminalStore.cs:ITerminalStore]

## Invariants
- Store collections are in-memory and scoped according to the DI registrations that consume them. [ChatSessionStore.cs:ChatSessionStore] [SelectedCardsStore.cs:SelectedCardsStore]
- Terminal history direction is passed explicitly when read. [TerminalStore.cs:GetTerminalHistory]

## Side Effects
- Selection changes and terminal line changes raise events for subscribed components. [SelectedCardsStore.cs:InvokeSelectionsChanged] [TerminalStore.cs:InvokeLinesChanged]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj
# Stores

## Overview

In-process state stores for Blazor circuit lifecycles. Each store keeps volatile, per-circuit data that never reaches the database: chat sessions, grid card selections, and terminal history. Backed by thread-safe collections (`ConcurrentDictionary`, `ConcurrentQueue`, lock-protected `HashSet`).

## Interface

- `IChatSessionStore` / `ChatSessionStore` — keyed by device ID; `GetOrAdd`, `TryGetSession`, `ContainsKey`, `GetAllSessions`, `TryRemove`, `AddOrUpdate`.
- `ISelectedCardsStore` / `SelectedCardsStore` — tracks selected device IDs; `Add`, `Remove`, `Contains`, `Clear`, `SelectedDevices`, `InvokeSelectionsChanged` (raises `SelectedDevicesChanged`).
- `ITerminalStore` / `TerminalStore` — bounded terminal history (500 entries) with forward/back navigation (`GetTerminalHistory`); `AddTerminalLine`, `AddTerminalHistory`, `TerminalLines` queue, `TerminalLinesChanged`.

## Invariants

- Storage is per-process memory only; sessions/stores are discarded when the process restarts.
- `TerminalStore` history and lines are capped at 500 entries each.
- `SelectedCardsStore` reads return snapshots under the store lock; no instance state is shared between circuits.

## Side Effects

- None persisted. Raises `SelectedDevicesChanged` and `TerminalLinesChanged` events in-memory.
- No disk, network, or database access.

## Verification

- Run: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

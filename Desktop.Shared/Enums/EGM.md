# Desktop Runtime Enums

## Overview

This directory holds small enums shared by desktop startup and UI code [Desktop.Shared/Enums/AppMode.cs:AppMode; Desktop.Shared/Enums/ButtonAction.cs:ButtonAction].

## Interface

- `AppMode` defines `Unattended`, `Attended`, and `Chat` [Desktop.Shared/Enums/AppMode.cs:AppMode].
- `ButtonAction` defines `Down` and `Up` [Desktop.Shared/Enums/ButtonAction.cs:ButtonAction].

## Invariants

- `AppMode` and `ButtonAction` are value declarations; they do not perform startup or input actions themselves [Desktop.Shared/Enums/AppMode.cs:AppMode; Desktop.Shared/Enums/ButtonAction.cs:ButtonAction].

## Side Effects

- None; these files declare enum values [Desktop.Shared/Enums/AppMode.cs:AppMode; Desktop.Shared/Enums/ButtonAction.cs:ButtonAction].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

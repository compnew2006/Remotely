# Desktop Shared Assembly Properties

## Overview

This directory contains assembly-level metadata [Desktop.Shared/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Interface

- `AssemblyInfo.cs` grants internals visibility to the `Remotely_Desktop` assembly [Desktop.Shared/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Invariants

- The friend assembly name is `Remotely_Desktop` [Desktop.Shared/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Side Effects

- None at runtime; the attribute affects compilation visibility [Desktop.Shared/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

# Windows Desktop Assembly Properties

## Overview

This directory contains assembly-level metadata [Desktop.Win/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Interface

- `AssemblyInfo.cs` grants internals visibility to the `Remotely.Desktop.Win.Tests` assembly [Desktop.Win/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Invariants

- The friend assembly name is `Remotely.Desktop.Win.Tests` [Desktop.Win/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Side Effects

- None at runtime; the attribute affects compilation visibility [Desktop.Win/Properties/AssemblyInfo.cs:InternalsVisibleTo].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

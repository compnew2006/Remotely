# Attributes

## Overview

This folder contains metadata attributes shared by model and UI code.

## Interface

- SortableAttribute may be applied to properties. It is a marker; consumers provide the sorting behavior [Shared/Attributes/SortableAttribute.cs:SortableAttribute].

## Invariants

- SortableAttribute is restricted to AttributeTargets.Property [Shared/Attributes/SortableAttribute.cs:AttributeUsage].

## Side Effects

- None. SortableAttribute only supplies metadata [Shared/Attributes/SortableAttribute.cs:SortableAttribute].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


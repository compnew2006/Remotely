# Desktop Shared Extensions

## Overview

This directory contains extensions for shared desktop types [Desktop.Shared/Extensions/SKBitmapExtensions.cs:SKBitmapExtensions].

## Interface

- `SKBitmapExtensions.ToRectangle` creates an `SKRect` from a bitmap's dimensions [Desktop.Shared/Extensions/SKBitmapExtensions.cs:SKBitmapExtensions.ToRectangle].

## Invariants

- `ToRectangle` returns a rectangle whose origin is `(0, 0)` and whose width and height match the bitmap [Desktop.Shared/Extensions/SKBitmapExtensions.cs:SKBitmapExtensions.ToRectangle].

## Side Effects

- None; `ToRectangle` creates and returns a value [Desktop.Shared/Extensions/SKBitmapExtensions.cs:SKBitmapExtensions.ToRectangle].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

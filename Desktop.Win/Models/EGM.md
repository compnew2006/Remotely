# Windows Desktop Models

## Overview

This directory contains a disposable wrapper for a DirectX desktop output [Desktop.Win/Models/DirectXOutput.cs:DirectXOutput].

## Interface

- `DirectXOutput` groups the adapter, Direct3D device, DXGI output duplication, texture, rotation, and bounds needed by screen capture [Desktop.Win/Models/DirectXOutput.cs:DirectXOutput].

## Invariants

- Initial `Bounds` starts at `(0, 0)` and uses the texture width and height; `Rotation` is retained from the output description supplied to the constructor [Desktop.Win/Models/DirectXOutput.cs:DirectXOutput.DirectXOutput].

## Side Effects

- `Dispose` releases the current duplication frame, disposes the duplication, texture, adapter, and device, then suppresses finalization [Desktop.Win/Models/DirectXOutput.cs:DirectXOutput.Dispose].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

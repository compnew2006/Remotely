# Desktop Shared View Models

## Overview

This directory contains the file-transfer progress model shared with desktop UI code [Desktop.Shared/ViewModels/FileUpload.cs:FileUpload].

## Interface

- `FileUpload` exposes a file path, progress percentage, cancellation token source, and a display name derived from the path [Desktop.Shared/ViewModels/FileUpload.cs:FileUpload.FilePath/PercentProgress/CancellationTokenSource/DisplayName].

## Invariants

- `DisplayName` is computed with `Path.GetFileName(FilePath)`; the model uses `ObservableObject` property storage and notifications [Desktop.Shared/ViewModels/FileUpload.cs:FileUpload.DisplayName; Desktop.Shared/Reactive/ObservableObject.cs:ObservableObject.Set].

## Side Effects

- Setting `FilePath` or `PercentProgress` raises `PropertyChanged`; callers can use the exposed cancellation token source to cancel an upload [Desktop.Shared/ViewModels/FileUpload.cs:FileUpload.FilePath/PercentProgress/CancellationTokenSource].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

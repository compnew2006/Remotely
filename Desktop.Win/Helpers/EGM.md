# Windows Desktop Helpers

## Overview

This directory contains the Win32 monitor-enumeration helper [Desktop.Win/Helpers/DisplayEnumerationHelper.cs:DisplaysEnumerationHelper].

## Interface

- `DisplaysEnumerationHelper.GetDisplays` returns display bounds, work areas, primary-display state, monitor handles, and device names [Desktop.Win/Helpers/DisplayEnumerationHelper.cs:DisplaysEnumerationHelper.GetDisplays].

## Invariants

- Display geometry is derived from the monitor and work-area rectangles returned by `GetMonitorInfo` [Desktop.Win/Helpers/DisplayEnumerationHelper.cs:DisplaysEnumerationHelper.GetDisplays].

## Side Effects

- `GetDisplays` calls `EnumDisplayMonitors` and `GetMonitorInfo` from `user32.dll` [Desktop.Win/Helpers/DisplayEnumerationHelper.cs:DisplaysEnumerationHelper.GetDisplays].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`

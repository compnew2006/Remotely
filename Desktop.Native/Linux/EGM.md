# Linux Native Bindings

## Overview

P/Invoke declarations for libc and the X11, XRandR, and XTest libraries used by the Linux desktop client.

## Interface

- Libc exposes geteuid; LibX11 exposes display, image, key-symbol, and synchronization calls [Desktop.Native/Linux/Libc.cs:Libc.geteuid], [Desktop.Native/Linux/LibX11.cs:LibX11.XGetImage].
- LibXrandr exposes monitor enumeration; LibXtst exposes simulated keyboard and pointer events [Desktop.Native/Linux/libXrandr.cs:LibXrandr.XRRGetMonitors], [Desktop.Native/Linux/LibXtst.cs:LibXtst.XTestFakeKeyEvent].

## Invariants

- XRRMonitorInfo and XImage are sequential native-layout structures passed across the P/Invoke boundary [Desktop.Native/Linux/libXrandr.cs:LibXrandr.XRRMonitorInfo].
- Display handles returned by XOpenDisplay are closed through XCloseDisplay by their callers [Desktop.Native/Linux/LibX11.cs:LibX11.XOpenDisplay].

## Side Effects

- Calls can open or close X displays, capture X images, enumerate monitors, and inject XTest input [Desktop.Native/Linux/LibX11.cs:LibX11.XOpenDisplay], [Desktop.Native/Linux/LibXtst.cs:LibXtst.XTestFakeButtonEvent].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln

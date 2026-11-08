# Screen Recorder PoC

## Overview

Standalone x64/.NET 10 probe for loading ScreenRecorderLib 7.0.1 and recording a short primary-display MP4.

## Interface

- The top-level program creates a PocReport, constructs RecorderOptions with the main display source and audio disabled, then records and stops a clip [poc/ScreenRecorderPoc/Program.cs:RecorderOptions/Record/Stop].
- PocReport stores load, recording, file-size, completion, exception, and host facts [poc/ScreenRecorderPoc/Program.cs:PocReport].
- HostFacts records OS, .NET version, machine name, process bitness, and interactive state [poc/ScreenRecorderPoc/Program.cs:HostFacts].

## Invariants

- ScreenRecorderPoc.csproj targets net10.0-windows, win-x64, and x64 and references ScreenRecorderLib 7.0.1 [poc/ScreenRecorderPoc/ScreenRecorderPoc.csproj:TargetFramework/RuntimeIdentifier/Platform/PackageReference].
- RecorderOptions selects DisplayRecordingSource.MainMonitor and sets IsAudioEnabled to false [poc/ScreenRecorderPoc/Program.cs:RecorderOptions].
- Success requires LoadOk, RecordFinished, and a nonzero FileSizeBytes [poc/ScreenRecorderPoc/Program.cs:LoadOk/RecordFinished/FileSizeBytes].

## Side Effects

- Recorder.Record captures display content and Recorder.Stop ends capture; the output is a temporary MP4 under AppContext.BaseDirectory [poc/ScreenRecorderPoc/Program.cs:Record/mediaPath].
- The program deletes a produced MP4 after collecting its size and writes a JSON report under the path derived from AppContext.BaseDirectory and results [poc/ScreenRecorderPoc/Program.cs:MediaDeleted/reportPath/WriteAllTextAsync].
- The project depends on ScreenRecorderLib native and managed binaries at runtime [poc/ScreenRecorderPoc/ScreenRecorderPoc.csproj:PackageReference].

## Verification

Build only; this PoC was not run for this documentation task:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build poc/ScreenRecorderPoc/ScreenRecorderPoc.csproj -p:Platform=x64
```

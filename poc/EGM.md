# Proofs of Concept

## Overview

Standalone evaluation tools and their result artifacts. These projects are not part of Remotely.sln and do not ship as production components.

## Interface

- install-sdk10.ps1 downloads and invokes dotnet-install.ps1 for a machine-level SDK installation [poc/install-sdk10.ps1:Invoke-WebRequest/dotnet-install.ps1].
- [G7Identity](./G7Identity/EGM.md) reproduces and measures the existing winlogon-token launch chain [poc/G7Identity/Program.cs:Launch/Probe].
- [ScreenRecorderPoc](./ScreenRecorderPoc/EGM.md) probes ScreenRecorderLib loading and a short primary-display recording [poc/ScreenRecorderPoc/Program.cs:Recorder.CreateRecorder/Record/Stop].
- poc/results contains JSON reports rather than source-bearing subtrees [poc/ScreenRecorderPoc/Program.cs:reportPath; poc/G7Identity/Program.cs:Write].

## Invariants

- G7Identity targets net8.0-windows, enables RollForward Major, and is documented in its project file as a PoC outside Remotely.sln [poc/G7Identity/G7Identity.csproj:TargetFramework/RollForward].
- ScreenRecorderPoc targets net10.0-windows and explicitly sets RuntimeIdentifier, Platform, and PlatformTarget to win-x64/x64 [poc/ScreenRecorderPoc/ScreenRecorderPoc.csproj:RuntimeIdentifier/Platform/PlatformTarget].
- The G7Identity launch path duplicates the winlogon token as TokenPrimary and starts a child on the resolved Winlogon or Default desktop [poc/G7Identity/Program.cs:Launch/ResolveDesktopNameLikeProduction]. AD-15 selected a different employee-token design, while G7 remains FAIL in the gate register [DECISION-LOG.md:AD-15; GATE-REGISTER.md:G7].

## Side Effects

- install-sdk10.ps1 downloads a script from dot.net, executes it with InstallDir set to C:\Program Files\dotnet, then lists installed SDKs [poc/install-sdk10.ps1:Invoke-WebRequest/InstallDir/dotnet.exe --list-sdks].
- G7Identity may enable SeDebugPrivilege, opens process/token handles, starts a probe child under the duplicated token, runs whoami.exe, and writes parent and child JSON reports [poc/G7Identity/Program.cs:TryEnableDebugPrivilege/Launch/Probe/RunCaptured/Write].
- ScreenRecorderPoc creates a primary-display recording with audio disabled, records for four seconds, deletes a produced media file after measuring it, and writes a JSON report [poc/ScreenRecorderPoc/Program.cs:RecorderOptions/Record/Stop/MediaDeleted/reportPath].

## Verification

Build commands only; the PoCs were not run for this documentation task:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build poc/G7Identity/G7Identity.csproj
& "$env:USERPROFILE\.dotnet\dotnet.exe" build poc/ScreenRecorderPoc/ScreenRecorderPoc.csproj -p:Platform=x64
```

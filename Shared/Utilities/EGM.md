# Shared Utilities

## Overview

Shared formatting, time, serialization, stream, audio/video preset, and pause-window utilities.

## Interface

- AudioQualityPresets and CameraQualityPresets map quality values to encoder/sample settings and clamp recording durations [Shared/Utilities/AudioQualityPresets.cs:AudioQualityPresets; Shared/Utilities/CameraQualityPresets.cs:CameraQualityPresets].
- CameraSourceSelection converts camera/display and audio selections to and from strings [Shared/Utilities/CameraSourceSelection.cs:CameraSourceSelection].
- ScreenshotPause evaluates and serializes screenshot pause windows [Shared/Utilities/ScreenshotPause.cs:ScreenshotPause].
- AppVersionHelper reads file version metadata; EnvironmentHelper exposes platform and executable names [Shared/Utilities/AppVersionHelper.cs:GetAppVersion; Shared/Utilities/EnvironmentHelper.cs:Platform/AgentExecutableFileName/DesktopExecutableFileName].
- AppendableStream wraps a stream and appends supplied bytes at the end of reads [Shared/Utilities/AppendableStream.cs:AppendableStream.Read].
- ConsoleHelper, Debouncer, Disposer, EnumMapper, JsonSerializerHelper, MathHelper, Time, and TimeSpanJsonConverter provide common support methods [Shared/Utilities/ConsoleHelper.cs:ConsoleHelper; Shared/Utilities/Debouncer.cs:Debounce; Shared/Utilities/Disposer.cs:TryDisposeAll; Shared/Utilities/EnumMapper.cs:ToEnum; Shared/Utilities/JsonSerializerHelper.cs:JsonSerializerHelper; Shared/Utilities/MathHelper.cs:GetFormattedPercent; Shared/Utilities/Time.cs:Time; Shared/Utilities/TimeSpanJsonConverter.cs:TimeSpanJsonConverter].

## Invariants

- AudioQualityPresets clamps duration to 5 through 600 seconds and defaults unknown qualities to Medium [Shared/Utilities/AudioQualityPresets.cs:ClampDurationSeconds/ParseQuality].
- CameraQualityPresets clamps duration to 5 through 300 seconds and defaults unknown qualities to Medium [Shared/Utilities/CameraQualityPresets.cs:ClampDurationSeconds/ParseQuality].
- ScreenshotPause treats matching start/end hours as disabled, supports overnight windows, and ignores malformed serialized pause data [Shared/Utilities/ScreenshotPause.cs:IsPaused/Deserialize].
- Time and SystemTime are separate adjustable clocks; this folder's Time is static and shared within the process [Shared/Utilities/Time.cs:Time; Shared/Services/SystemTime.cs:SystemTime].

## Side Effects

- AppendableStream delegates operations and disposal to its underlying Stream [Shared/Utilities/AppendableStream.cs:Dispose/Read/Write].
- ConsoleHelper reads or writes the process console; Debouncer creates process-local timers [Shared/Utilities/ConsoleHelper.cs:ReadLine/WriteLine; Shared/Utilities/Debouncer.cs:Debounce].
- AppVersionHelper reads executable version metadata; ScreenshotPause serializes or parses JSON text [Shared/Utilities/AppVersionHelper.cs:GetAppVersion; Shared/Utilities/ScreenshotPause.cs:Serialize/Deserialize].
- Time.Set, Time.Adjust, and Time.Reset mutate static process-local clock state [Shared/Utilities/Time.cs:Set/Adjust/Reset].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


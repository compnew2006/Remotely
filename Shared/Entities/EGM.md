# Entities

## Overview

Shared persistence-shaped records for organizations, users, devices, scripts, alerts, and captured media metadata.

## Interface

- Organization groups tenant records and relationships, including devices, device groups, users, and script records [Shared/Entities/Organization.cs:Organization].
- RemotelyUser extends IdentityUser and adds organization, role, device-group, script, and user-option state [Shared/Entities/RemotelyUser.cs:RemotelyUser].
- Device contains device identity and telemetry plus per-device screenshot, camera, audio, and keyboard capture settings [Shared/Entities/Device.cs:Device].
- ScriptResult and ScriptRun represent completed and scheduled script execution data [Shared/Entities/ScriptResult.cs:ScriptResult; Shared/Entities/ScriptRun.cs:ScriptRun].
- Screenshot, CameraClip, AudioClip, and KeyboardLog describe stored capture records; their file names and device IDs are data fields, not path validation [Shared/Entities/Screenshot.cs:Screenshot; Shared/Entities/CameraClip.cs:CameraClip; Shared/Entities/AudioClip.cs:AudioClip; Shared/Entities/KeyboardLog.cs:KeyboardLog].
- Other public records include Alert, ApiToken, BrandingInfo, DeviceGroup, InviteLink, KeyValueRecord, SavedScript, ScriptSchedule, and SharedFile [Shared/Entities/Alert.cs:Alert; Shared/Entities/ApiToken.cs:ApiToken; Shared/Entities/BrandingInfo.cs:BrandingInfo; Shared/Entities/DeviceGroup.cs:DeviceGroup; Shared/Entities/InviteLink.cs:InviteLink; Shared/Entities/KeyValueRecord.cs:KeyValueRecord; Shared/Entities/SavedScript.cs:SavedScript; Shared/Entities/ScriptSchedule.cs:ScriptSchedule; Shared/Entities/SharedFile.cs:SharedFile].

## Invariants

- Entity keys and lengths are expressed with DataAnnotations on the relevant properties [Shared/Entities/Device.cs:Key/StringLength; Shared/Entities/Organization.cs:Key/Required].
- Device defaults screenshot, camera, and audio retention to 200 records and 24 hours; keyboard retention defaults to 500 records and 24 hours [Shared/Entities/Device.cs:ScreenshotRetentionCount/CameraRetentionCount/AudioRetentionCount/KeyboardRetentionCount].
- Device.KeyboardShowPasswords is false by default. KeyboardLog.IsPasswordField identifies password-field entries; this model does not itself capture or mask text [Shared/Entities/Device.cs:KeyboardShowPasswords; Shared/Entities/KeyboardLog.cs:IsPasswordField].
- JsonIgnore navigation properties are excluded from the corresponding JSON shape; examples are Device.Organization and ScriptResult.Device [Shared/Entities/Device.cs:Organization; Shared/Entities/ScriptResult.cs:Device].

## Side Effects

- None in these entity classes. Device and capture entities define properties and annotations without I/O calls [Shared/Entities/Device.cs:Device; Shared/Entities/AudioClip.cs:AudioClip].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


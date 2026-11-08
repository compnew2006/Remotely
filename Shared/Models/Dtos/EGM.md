# Model DTOs

## Overview

MessagePack and DataContract payloads for screen, input, clipboard, file, audio, and session events.

## Interface

- DtoType assigns wire values to supported payload kinds; DtoWrapper carries chunk bytes, type, sequence, request, and instance IDs [Shared/Models/Dtos/DtoType.cs:DtoType; Shared/Models/Dtos/DtoWrapper.cs:DtoWrapper].
- DtoTypeExtensions.ControlTypes and IsControlMessage classify device-modifying input payloads [Shared/Models/Dtos/DtoTypeExtensions.cs:ControlTypes/IsControlMessage].
- ScreenDataDto, ScreenSizeDto, SelectScreenDto, CursorChangeDto, and FrameReceivedDto describe display and frame state [Shared/Models/Dtos/ScreenDataDto.cs:ScreenDataDto; Shared/Models/Dtos/ScreenSizeDto.cs:ScreenSizeDto; Shared/Models/Dtos/SelectScreenDto.cs:SelectScreenDto; Shared/Models/Dtos/CursorChangeDto.cs:CursorChangeDto; Shared/Models/Dtos/FrameReceivedDto.cs:FrameReceivedDto].
- MouseMoveDto, MouseDownDto, MouseUpDto, MouseWheelDto, TapDto, KeyDownDto, KeyUpDto, KeyPressDto, and TextTransferDto describe input events [Shared/Models/Dtos/MouseMoveDto.cs:MouseMoveDto; Shared/Models/Dtos/MouseDownDto.cs:MouseDownDto; Shared/Models/Dtos/MouseUpDto.cs:MouseUpDto; Shared/Models/Dtos/MouseWheelDto.cs:MouseWheelDto; Shared/Models/Dtos/TapDto.cs:TapDto; Shared/Models/Dtos/KeyDownDto.cs:KeyDownDto; Shared/Models/Dtos/KeyUpDto.cs:KeyUpDto; Shared/Models/Dtos/KeyPressDto.cs:KeyPressDto; Shared/Models/Dtos/TextTransferDto.cs:TextTransferDto].
- FileDto, ClipboardTextDto, AudioSampleDto, ToggleAudioDto, ToggleBlockInputDto, WindowsSessionsDto, SessionMetricsDto, and EmptyDto provide the remaining payloads [Shared/Models/Dtos/FileDto.cs:FileDto; Shared/Models/Dtos/ClipboardTextDto.cs:ClipboardTextDto; Shared/Models/Dtos/AudioSampleDto.cs:AudioSampleDto; Shared/Models/Dtos/ToggleAudioDto.cs:ToggleAudioDto; Shared/Models/Dtos/ToggleBlockInputDto.cs:ToggleBlockInputDto; Shared/Models/Dtos/WindowsSessionsDto.cs:WindowsSessionsDto; Shared/Models/Dtos/SessionMetricsDto.cs:SessionMetricsDto; Shared/Models/Dtos/EmptyDto.cs:EmptyDto].

## Invariants

- DtoTypeExtensions.ControlTypes contains the input or device-modifying DTO types and IsControlMessage tests membership in that set [Shared/Models/Dtos/DtoTypeExtensions.cs:ControlTypes/IsControlMessage].
- DtoType numeric values are wire-contract values [Shared/Models/Dtos/DtoType.cs:DtoType].
- DTO declarations provide serialization shapes only; runtime authorization and transport behavior belong to their consumers [Shared/Models/Dtos/DtoWrapper.cs:DtoWrapper; Shared/Models/Dtos/FileDto.cs:FileDto].

## Side Effects

- None from these payload declarations or classification helpers; DtoTypeExtensions only checks a set [Shared/Models/Dtos/DtoType.cs:DtoType; Shared/Models/Dtos/DtoTypeExtensions.cs:IsControlMessage].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```


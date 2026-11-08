## Agent/EGM.md

No drift detected.

## Agent/Extensions/EGM.md

No drift detected.

## Agent/Interfaces/EGM.md

No drift detected.

## Agent/Models/EGM.md

No drift detected.

## Agent/Services/EGM.md

- The Interface inventory omits public service boundaries present in this directory: IChatClientService/ChatClientService, IConfigService/ConfigService, ICpuUtilizationSampler, DeviceInfoGeneratorBase, IExternalScriptingShell/ExternalScriptingShell, IFileLogsManager, IPsCoreShell/PsCoreShell, IScriptExecutor, IScriptingShellFactory, IUninstaller/Uninstaller, IUpdateDownloader/UpdateDownloader, and IWakeOnLanService/WakeOnLanService. The overview covers these areas broadly, but the contract does not identify these service entry points.

## Agent/Services/Linux/EGM.md

No drift detected.

## Agent/Services/MacOS/EGM.md

No drift detected.

## Agent/Services/Windows/EGM.md

No drift detected.

## Desktop.Linux/EGM.md

No drift detected.

## Desktop.Linux/Services/EGM.md

No drift detected.

## Desktop.Linux/Startup/EGM.md

No drift detected.

## Desktop.Native/EGM.md

No drift detected.

## Desktop.Native/Linux/EGM.md

- The P/Invoke interface omits the public XWindowAttributes structure declared in Desktop.Native/Linux/LibX11.cs and returned by LibX11.XGetWindowAttributes. Its native layout is part of the Linux interop boundary.

## Desktop.Native/Windows/EGM.md

- The interface cites representative Win32 declarations but leaves out many public ABI types from the ADVAPI32, SECUR32, User32, GDI32, Kernel32, and WTSAPI32 bindings. Examples include TOKEN_PRIVILEGES, LUID_AND_ATTRIBUTES, SECURITY_ATTRIBUTES, STARTUPINFO, MEMORYSTATUSEX, INPUT and its input unions, KEYBDINPUT, MOUSEINPUT, WTS_SESSION_INFO, and the related token, logon, input, cursor, monitor, and session enums. These types define the layouts and values consumed by the declared APIs.

## Desktop.Shared/Abstractions/EGM.md

No drift detected.

## Desktop.Shared/EGM.md

No drift detected.

## Desktop.Shared/Enums/EGM.md

No drift detected.

## Desktop.Shared/Extensions/EGM.md

No drift detected.

## Desktop.Shared/Messages/EGM.md

No drift detected.

## Desktop.Shared/Properties/EGM.md

No drift detected.

## Desktop.Shared/Reactive/EGM.md

No drift detected.

## Desktop.Shared/Services/EGM.md

- The Interface inventory omits public service boundaries declared in this directory: IBrandingProvider/BrandingProvider, IChatHostService/ChatHostService, IDesktopEnvironment, IIdleTimer/IdleTimer, and IImageHelper/ImageHelper. These are user-facing runtime services in addition to the hub and capture interfaces already listed.

## Desktop.Shared/Startup/EGM.md

No drift detected.

## Desktop.Shared/ViewModels/EGM.md

No drift detected.

## Desktop.UI/Controls/Dialogs/EGM.md

No drift detected.

## Desktop.UI/Controls/EGM.md

No drift detected.

## Desktop.UI/EGM.md

No drift detected.

## Desktop.UI/Services/EGM.md

No drift detected.

## Desktop.UI/Startup/EGM.md

No drift detected.

## Desktop.UI/ViewModels/EGM.md

No drift detected.

## Desktop.UI/ViewModels/Fakes/EGM.md

No drift detected.

## Desktop.UI/Views/EGM.md

No drift detected.

## Desktop.Win/EGM.md

No drift detected.

## Desktop.Win/Helpers/EGM.md

- The display-enumeration contract does not mention the public RECT structure declared in Desktop.Win/Helpers/DisplayEnumerationHelper.cs, even though display bounds and work areas are part of the returned interface.

## Desktop.Win/Models/EGM.md

No drift detected.

## Desktop.Win/Properties/EGM.md

No drift detected.

## Desktop.Win/Services/EGM.md

- The listed Windows service interfaces omit IMessageLoop, the message-pump contract implemented by MessageLoop and injected into AppStartup.

## Desktop.Win/Startup/EGM.md

No drift detected.

## EGM.md

- The AGENTS evidence locator is stale: this contract cites "There is no npm build step", while the current AGENTS.md wording is "There is no npm browser-client build step". The statement remains substantively aligned, but the exact documentation anchor needs updating.

## poc/EGM.md

No drift detected.

## poc/G7Identity/EGM.md

- The experiment contract omits its public native interop data types, including TOKEN_PRIVILEGES, LUID, LUID_AND_ATTRIBUTES, SECURITY_ATTRIBUTES, STARTUPINFO, PROCESS_INFORMATION, and PROCESSENTRY32. These structures define the Win32 boundary used by the identity experiment.

## poc/ScreenRecorderPoc/EGM.md

No drift detected.

## Server/API/EGM.md

- The controller inventory omits BrandingController, CustomBinariesController, HealthCheckController, LoginController, and ServerLogsController. Their branding, custom-binary, health, login, and server-log endpoints are not represented in the current API boundary summary.

## Server/Auth/EGM.md

- The authorization boundary summary omits OrganizationAdminRequirementHandler and PolicyNames, which implement organization-admin policy handling and provide the policy identifiers used by components.

## Server/Components/Account/EGM.md

No drift detected.

## Server/Components/Account/Pages/EGM.md

No drift detected.

## Server/Components/Account/Pages/Manage/EGM.md

No drift detected.

## Server/Components/Account/Shared/EGM.md

No drift detected.

## Server/Components/Devices/EGM.md

No drift detected.

## Server/Components/EGM.md

- The component contract omits the exported showModal JavaScript interop function in ModalHarness.razor.js, which ModalHarness invokes to open the browser modal.

## Server/Components/Layout/EGM.md

No drift detected.

## Server/Components/ModalContents/EGM.md

No drift detected.

## Server/Components/Pages/EGM.md

No drift detected.

## Server/Components/Scripts/EGM.md

No drift detected.

## Server/Components/TabControl/EGM.md

No drift detected.

## Server/Components/TreeView/EGM.md

No drift detected.

## Server/Converters/EGM.md

No drift detected.

## Server/Data/EGM.md

No drift detected.

## Server/EGM.md

No drift detected.

## Server/Enums/EGM.md

No drift detected.

## Server/Extensions/EGM.md

No drift detected.

## Server/Filters/EGM.md

No drift detected.

## Server/Hubs/EGM.md

- The hub boundary summary omits ICircuitConnection, the circuit-scoped interface used by Blazor components to relay device and viewer operations. It is declared in Server/Hubs/CircuitConnection.cs and is a major boundary alongside the three hubs.

## Server/Migrations/EGM.md

No drift detected.

## Server/Migrations/PostgreSql/EGM.md

No drift detected.

## Server/Migrations/Sqlite/EGM.md

No drift detected.

## Server/Migrations/SqlServer/EGM.md

No drift detected.

## Server/Models/EGM.md

No drift detected.

## Server/Models/Messages/EGM.md

- The message inventory mentions capture events but omits other public message payloads: AudioDevicesReceivedMessage, AudioLiveChunkMessage, CameraDevicesReceivedMessage, ChatSessionsChangedMessage, DeviceCardStateChangedMessage, DisplayNotificationMessage, DownloadFileMessage, DownloadFileProgressMessage, and PowerShellCompletionsMessage. These notifications are part of the component, hub, and browser-client message boundary.

## Server/Options/EGM.md

No drift detected.

## Server/Pages/EGM.md

No drift detected.

## Server/Properties/EGM.md

No drift detected.

## Server/RateLimiting/EGM.md

No drift detected.

## Server/Services/EGM.md

- The service contract omits several public boundaries and lifecycles present in this directory: ICameraDevicesStore/CameraDevicesStore, IKeyboardLogService, KeyboardCaptureStartupService, IScriptScheduleDispatcher, ISessionRecordingSink/SessionRecordingSink, RemoteControlSessionCleaner, IModalService/ModalService, ILoaderService/LoaderService, IToastService, IJsInterop, ILogsManager, IThemeProvider, IUpgradeService/UpgradeService, EmailSenderEx, EmailSenderFake, and PascalCasePolicy. In particular, the startup service that restores enabled keyboard capture and the session-recording sink are not covered by the current service summary.

## Server/Services/Stores/EGM.md

No drift detected.

## Server/wwwroot/EGM.md

No drift detected.

## Server/wwwroot/src/EGM.md

- The browser-client interface summary names App and ViewerHubConnection but omits exported helpers and service contracts present in this source directory, including ApplyInputHandlers, CreateGUID, FormatScriptOutput, FormatScriptOutputArray, ParseSearchString, Prompt, SetScreenSize, SetStatusMessage, ToggleConnectUI, UpdateCursor, UpdateDisplays, UpdateMetrics, UpdateWindowsSessions, ViewerApp, IStreamResult, and ISubscription. These exports form part of the browser client boundary.

## Server/wwwroot/src/Enums/EGM.md

No drift detected.

## Server/wwwroot/src/Interfaces/EGM.md

- The wire-shape inventory omits public DTOs including ClipboardTextDto, CtrlAltDelDto, CursorChangeDto, EmptyDto, FrameReceivedDto, KeyDownDto, KeyPressDto, KeyUpDto, MouseDownDto, MouseMoveDto, MouseUpDto, MouseWheelDto, RemoteControlViewerOptions, ScreenDataDto, ScreenSizeDto, SelectScreenDto, SessionMetricsDto, TapDto, TextTransferDto, ToggleAudioDto, ToggleBlockInputDto, and WindowsSessionsDto. The current summary therefore does not identify most of the client/server input and display DTO surface.

## Server/wwwroot/src/Models/EGM.md

No drift detected.

## Shared/Attributes/EGM.md

No drift detected.

## Shared/DtoEntityBases/EGM.md

No drift detected.

## Shared/Dtos/EGM.md

No drift detected.

## Shared/EGM.md

No drift detected.

## Shared/Entities/EGM.md

No drift detected.

## Shared/Enums/EGM.md

No drift detected.

## Shared/Extensions/EGM.md

No drift detected.

## Shared/Helpers/EGM.md

No drift detected.

## Shared/Interfaces/EGM.md

No drift detected.

## Shared/Models/Dtos/EGM.md

No drift detected.

## Shared/Models/EGM.md

- The public model inventory omits AudioDeviceInfo, AudioQuality, AudioQualityPreset, CameraAudioSource, CameraDeviceKind, CameraQuality, CameraQualityPreset, ChatMessage, ConnectionInfo, CursorInfo, DeviceSetupOptions, PwshCompletionResult, PwshCompletionResultType, and WindowsSessionType. These are shared media, device, chat, connection, cursor, PowerShell, and session contracts.

## Shared/Primitives/EGM.md

No drift detected.

## Shared/Services/EGM.md

- The update-verification API summary omits the public UpdateVerificationFailure and UpdateVerificationResult types returned by UpdatePackageVerifier.

## Shared/Utilities/EGM.md

No drift detected.

## Shared/ViewModels/EGM.md

No drift detected.

## Utilities/EGM.md

No drift detected.

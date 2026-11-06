# REVERSE ENGINEERING & SPEC KIT READINESS REPORT

**المشروع:** Remotely Capture Extension — توسيع لـ Remotely (‏GPL-3.0) بقدرة التقاط مجدول للموظفين بموافقة مُعلنة.
**نوع المهمة:** Reverse engineering + architectural discovery + Spec Kit foundation.
**المرحلة:** ما قبل المواصفة — لا تنفيذ، لا تغيير في كود الإنتاج، لا commit.

> كل ادعاء موسوم: `[VERIFIED]` = متحقَّق من كود مقروء · `[INFERRED]` = استنتاج هندسي · `[UNKNOWN]` = غير قابل للتحقق · `[PROPOSED]` = تصميم مقترح غير موجود.

---

## 1. Source Evidence

### 1.1 المستودعات المفحوصة

| المستودع | URL | الفرع | Commit SHA | التاريخ | الرخصة | الموقع المحلي |
|---|---|---|---|---|---|---|
| Remotely | `https://github.com/immense/Remotely` | `master` | `ec0d45d8f83ee512e60306ce4ddefed6509bd903` | 2024-12-17 | GPL-3.0 | `…\new-chat\work\Captura` |
| ScreenRecorderLib | `https://github.com/sskodje/ScreenRecorderLib` | `master` | `7e8703c69dafc3e7c2e38b9de774c72a8e06ffca` | 2026-10-27 | MIT | `C:\Users\Administrator\Documents\smacc_TEST\ScreenRecorderLib` |

`[VERIFIED]` ScreenRecorderLib: آخر tag = `v7.0.1`؛ الـmaster فيه إصلاح بعد v7.0.1: *"Fix for bug where fragmented MP4 recordings play shorter than recorded (#375)"*.
`[VERIFIED]` ScreenRecorderLib toolchain: `ScreenRecorderLib.vcxproj` (C++/CLI) + `ScreenRecorderLibNative.vcxproj` (C++ native) + `Nuget/ScreenRecorderLib.nuspec` + `Nuget/ScreenRecorderLib.targets`.

### 1.2 الأدوات المستخدمة في الفحص

| الأداة | الحالة | ما استخدمته له |
|---|---|---|
| `codebase-memory-mcp` | مفهرس باسم `Remotely` — 9,956 عقدة / 33,556 حافة / 7,713 ملف | `get_architecture` (توزيع اللغات: C# 560 ملف، TS 31، JS 53، CSS 50)، `trace_path` على مسار `RemoteControl` (15 callee)، `query_graph` على `Shared/Entities` (14 كيان) |
| `Serena` | مفعّل (project = `Captura`، C# language server) — 6 memories | `find_symbol` (تحقق من مواقع الرموز)، `find_referencing_symbols` على `RemoteControlSession/ViewOnly` → أثبت أن المرجع فقط في موضعين |
| `graphify` | graph مبني: 8,318 عقدة / 18,106 حافة / 439 مجتمع | `graphify query` على مسار "جلسة المستخدم ↔ نقل الملفات"، ومCROSS-community edges، وImport Cycles |
| Native Read/Grep | — | قراءة مباشرة لكل الملفات المُشار إليها بسطر/سطر |

`[VERIFIED]` فجوة تحليل معروفة: 47 ملف `.razor` تُحلَّل جزئياً في codebase-memory و52 ملف غير قابل للتحليل → **أرقام الأسطر في هذه الملفات غير موثوقة**؛ تم الرجوع للقراءة المباشرة لها.

### 1.3 الملفات المفحوصة فعلياً (نموذج ممثّل)

**السيرفر:** `Server/Program.cs` · `Server/Hubs/{AgentHub,CircuitConnection,DesktopHub,ViewerHub}.cs` · `Server/Models/{RemoteControlSession,SettingsModel}.cs` · `Server/Services/{DataService,SessionRecordingSink,DataCleanupService,ScriptScheduler,ScriptScheduleDispatcher}.cs` · `Server/Auth/{ApiAuthorizationFilter,ExpiringTokenFilter,LocalOnlyFilter}.cs` · `Server/Options/ApplicationOptions.cs` · `Server/API/*` · `Server/Components/Devices/DeviceCard.razor.cs` · `Server/wwwroot/src/{App,InputEventHandlers,ViewerHubConnection,UI}.ts`

**العميل:** `Agent/Program.cs` · `Agent/Services/{AgentHubConnection,ChatClientService,ConfigService,Updater,UpdateDownloader,FileLogsManager}.cs` · `Agent/Services/Windows/{AppLauncherWin,UpdaterWin}.cs` · `Desktop.Win/Program.cs` · `Desktop.Win/Services/{ScreenCapturerWin,AudioCapturerWin,KeyboardMouseInputWin,FileTransferServiceWin,AppStartup,MessageLoop}.cs` · `Desktop.Shared/Services/{Viewer,ScreenCaster,DtoMessageHandler,DesktopHubConnection,ChatHostService}.cs` · `Desktop.Shared/Abstractions/*` · `Desktop.Native/Windows/{Win32Interop,User32,WTSAPI32,Kernel32}.cs`

**البيانات:** `Shared/Entities/*` (14 كيان) · `Shared/AppConstants.cs` · `Shared/Helpers/DtoChunker.cs`

**المكتبة الخارجية:** `ScreenRecorderLib/{Recorder.h,Options.h}` · `ScreenRecorderLibNative/{CameraCapture.cpp,WASAPICapture.cpp,DesktopDuplicationCapture.cpp,WindowsGraphicsCapture.cpp,AudioManager.cpp}` · `Nuget/ScreenRecorderLib.{nuspec,targets}` · `TestConsoleAppDotNetCore/*`

### 1.4 القيود — ما **لم** أفعله

- `[UNKNOWN]` **لم أشغّل أي كود**: لا `dotnet build`، لا `dotnet test`، لا benchmark، لا proof-of-concept لـ ScreenRecorderLib. كل رقم أداء في هذا التقرير هو `[UNKNOWN]` أو `[INFERRED]` — **لا يوجد أي قياس**.
- `[UNKNOWN]` **لم أشغّل سيرفر Remotely فعليًا** ولا اختبرت أي تدفق remote control حي.
- `[UNKNOWN]` ScreenRecorderLib **لم يُنفَّذ على Windows** — التوافق كله على مستوى الشيفرة فقط.
- `[VERIFIED]` **Spec Kit غير مثبّت** (تفصيل في §7) — الـconstitution كُتب يدويًا على المسار القانوني بدل توليده بأمر الأداة.
- `[VERIFIED]` **كل مسوحات البحث الخلفية الثلاثة اكتملت** (سيرفر شامل / frontend+ops+tests / ScreenRecorderLib عميق) — التقرير مبني على مخرجاتها بالكامل.
- `[VERIFIED]` الـlocal clone من Remotely بتاريخ 2024-12-17 — `master` الأصلي قد يكون تقدّم منذ ذلك الحين.

---

## 2. Existing Remotely Capabilities

### 2.1 نموذج العمليات — `[VERIFIED]`

| العملية | الدليل | الجلسة | الدور |
|---|---|---|---|
| **Remotely Agent** (Windows Service) | `Agent/Program.cs:29` → `.UseWindowsService()` | **Session 0** | اتصال `/hubs/service`، تنفيذ السكربتات، إدارة الملفات، التحديث، **إطلاق** العميل في جلسة المستخدم |
| **Remotely Desktop** (Avalonia WinExe) | `Desktop.Win/Program.cs` → `OutputType=WinExe`، لا `UseWindowsService` | **جلسة المستخدم التفاعلية** | التقاط الشاشة، حقن الإدخال، الصوت، الملفات، الشات |
| **Browser client** (TypeScript) | `Server/wwwroot/src/*` | متصفح | واجهة المشاهد |

`[VERIFIED]` جسر Session 0 → الجلسة التفاعلية: `Agent/Services/Windows/AppLauncherWin.cs:111-124` يستدعي `Win32Interop.CreateInteractiveSystemProcess(...)`، و`Desktop.Native/Windows/Win32Interop.cs:158` يضبط `si.lpDesktop = @"winsta0\" + ResolveDesktopName(dwSessionId)`، و`:174-185` `CreateProcessAsUser`.

> ⚠️ **تصحيح مهم (2026-11-06، مراجعة الـconstitution):** `Win32Interop.cs:121-128` يبحث عن **`winlogon`** في الجلسة المستهدفة، و`:134` يفتح **توكنه** بـ`TOKEN_DUPLICATE`، و`:145` `DuplicateTokenEx(..., TokenPrimary, out hUserTokenDup)`. و`winlogon.exe` يعمل كـ`NT AUTHORITY\SYSTEM` ⇒ **العملية التفاعلية الناتجة تعمل كـSYSTEM، لا كهوية الموظف**. `[SOURCE-CAPABILITY]` — يلزم تأكيد `whoami /all` في الـPoC. المسار البديل `AppLauncherWin.cs:41,111,134-143`: لو الـAgent **مش** SYSTEM ⇒ `Process.Start` ⇒ هوية مستخدم الـAgent نفسه.
> ⇒ **الأثر:** "الجلسة التفاعلية" ≠ "حساب الموظف". أي نموذج موافقة/خصوصية يفترض أن العملية التفاعلية هوية الموظف **بني على افتراض كاذب**. راجع المبدأ V في `.specify/memory/constitution.v1.1.0.proposed.md` والحاجز G6.
> وأيضاً: `ResolveDesktopName` (`Win32Interop.cs:195-215`) يرجع `"Winlogon"` (سطح المكتب الآمن) أثناء `LogonUI.exe`/`consent.exe` ⇒ أثناء الدخول وموافقة UAC تعمل العملية على **سطح المكتب الآمن** لا سطح المستخدم ⇒ الالتقاط يجب أن يُعلَّق هناك.

### 2.2 الميزات الموجودة (كلها `[VERIFIED]`)

- **إدارة الأجهزة**: تسجيل الجهاز (`Device` entity)، `ApiToken` للمصادقة، `DeviceGroup`، multi-tenant، أدوار `ServerAdmin` / `OrganizationAdmin` / `TwoFactorRequired`.
- **Remote control**: 3 أوضاع (`AppMode`: `Unattended` / `Attended` / `Chat`)، بثّ شاشة DXGI→BitBlt، حقن إدخال بـ`SendInput`، clipboard، تغيير جلسة Windows المستهدفة.
- **Chat**: من الخدمة إلى جلسة المستخدم عبر **named pipe** (`Agent/Services/ChatClientService.cs:85-107` ↔ `Desktop.Shared/Services/ChatHostService.cs:30`) — **هذا هو الـIPC الوحيد بين الخدمة والعملية التفاعلية**.
- **File transfer**: مساران — browser→device عبر `AgentHub` (`CircuitConnection.cs:41`), وdevice→browser عبر DTO chunks 40KB (`Desktop.Shared/Services/Viewer.cs:202`).
- **Scripting**: PowerShell 7.4 (`Microsoft.PowerShell.SDK 7.4.5`) + `ExternalScriptingShell` + **`ScriptSchedule` + `ScriptScheduler` + `ScriptScheduleDispatcher`** (جدولة مركزية جاهزة).
- **تسجيل جلسات**: `Server/Services/SessionRecordingSink.cs` يخزّن webm على القرص تحت `…/AppData/recordings/{yyyy-MM-dd}/`، و`DataCleanupService` ينظّفه بـ`settings.DataRetentionInDays` (افتراضي 90 يوم).
- **تحديث تلقائي**: `Agent/Services/Windows/UpdaterWin.cs` — فحص عند الإقلاع + **كل 6 ساعات**، تحميل `RemotelyUpdate.zip` + `Install-Remotely.ps1` إلى `%TEMP%` ثم تشغيله.
- **تسجيل الفيديو من جهة المتصفح**: `ViewerHub.StoreSessionRecording(IAsyncEnumerable<byte[]> webmStream)` — المتصفح يسجّل الجلسة ويبعتشرائط.

### 2.3 View Only — التحليل الدقيق (الأهم في المشروع)

`[VERIFIED]` **View Only في Remotely اليوم هو Seediness واجهة، وليس حدّ صلاحيات.** الدليل من خمس جهات مستقلة:

1. **الإنشاء**: `Server/Hubs/CircuitConnection.cs:218,266` — `RemoteControl(deviceId, viewOnly)` يضع الفلاج في `RemoteControlSession`. المصدر هو قرار الأدمن (`Server/Components/Devices/DeviceCard.razor.cs:310`).
2. **لا فحص في السيرفر**: البحث عن `ViewOnly` في كل `Server/**/*.cs` (خارج الـmodels والـcomponents) يرجع **3 نتائج فقط**: توقيع الدالة، باراميتر الدالة، والتعيين. **لا يوجد `if (session.ViewOnly)` في أي مكان.** و`ViewerHub.SendDtoToClient(byte[] dtoWrapper)` (`Server/Hubs/ViewerHub.cs:187`) يمرّر أي DTO إلى الجهاز بلا فحص.
3. **لا فحص على الجهاز**: `Desktop.Shared/Services/DtoMessageHandler.cs:63` فيه `if (!viewer.HasControl) return;` — لكن `HasControl` مُعرَّف `= true` في `Desktop.Shared/Services/Viewer.cs:95` و**لا يوجد أي تعيين له في الريبو كله** (تأكيد Serena: كل مراجع `HasControl` هي تعريفات + فحص). ⇒ **البوابة معطّلة فعلياً**.
4. **المتصفح يتحكم بالفلاج**: `Server/wwwroot/src/App.ts:23-25` يقرأ `viewonly` من query string، و`InputEventHandlers.ts:125-128` — ضغطة على `ViewOnlyButton` **تبدّل** `ViewerApp.ViewOnlyMode`. الحماية كلها `if (ViewerApp.ViewOnlyMode) return;` في كل input handler.
5. **الفلاج يضيع**: `ViewerHub.cs:91` في `ChangeWindowsSession` ينفّذ `SessionInfo = SessionInfo.CreateNew()`، و`RemoteControlSession.cs:91` فيه `clone.ViewOnly = false;` ⇒ **تغيير جلسة Windows المستهدفة يمسح view-only من جهة السيرفر**.

`[VERIFIED]` الفلاج **لا يُنقل للجهاز أصلاً**: `Shared/Interfaces/IAgentHubClient.cs:54-60` — توقيع `RemoteControl(...)` المُرسل للوكيل **لا يحمل أي باراميتر view-only**؛ و`DesktopHub.ReceiveUnattendedSessionInfo(...)` يحمل `sessionId / accessKey / machineName / requesterName / organizationName` فقط. وبحث `viewonly|view-only` في `Desktop.Shared/` و`Desktop.Core/` = **صفر نتائج** ⇒ الجهاز لا يعرف أن الجلسة view-only.

**نقاط إضافية مؤكَّدة (من مسح السيرفر الكامل):**
- `Server/Models/RemoteControlSession.cs:40` — `RelativeAccessUri` يثبّت `ViewOnly = false` **حرفياً**.
- `Server/API/RemoteControlController.cs:138-145,176` — الجلسة المنشأة عبر **API لا تحمل ViewOnly إطلاقاً**، و`:176` يرجّع رابطاً **بلا** `viewonly` ⇒ **التحكم عن بُعد عبر API always full-control**.
- `ViewerHub.cs:162` (`InvokeCtrlAltDel`) و`ViewerHub.cs:77` (`ChangeWindowsSession`) — **بلا بوابة view-only**، تماماً مثل `SendDtoToClient`.

**ما هو مطبَّق فعلاً على السيرفر (وهو ما يجب أن يُبنى عليه نظام الالتقاط):**
| الضابط | الموقع | الحالة |
|---|---|---|
| عضوية مجموعة الجهاز | `DataService.cs:1018-1036` `DoesUserHaveAccessToDevice` (org + admin أو عضو المجموعة) | **مُطبَّق** — `:235` `:381` `:212` `:297` `:319` + `ScriptingController.cs:72` + `DeviceCard.razor.cs:204` |
| مطابقة `accessKey` للجلسة غير المراقَبة | `ViewerHub.cs:210-221` | **مُطبَّق** |
| الموافقة (`EnforceAttendedAccess`) | `CircuitConnection.cs:268` → `ViewerHub.cs:245-262` → `PromptForAccess` → `PromptForAccessResult` | **مُطبَّق** |
| مصادقة/OTP لصفحة المشاهدة | `Server/Filters/ViewerAuthorizationFilter.cs:23-40` | **مُطبَّق** |

> **خلاصة عملية:** النموذج الحقيقي المفروض في Remotely هو **عضوية المجموعة + مفتاح الوصول + بوابة الموافقة** — وليس View Only. ⇒ نظام الالتقاط يجب أن يُبنى على هذا النمط (org-scoped + موافقة + صلاحية لكل جهاز)، **لا** على View Only. كما أن تقوية View Only على السيرفر قرار **مستقل** لا يجوز دفنه في feature الالتقاط (المبدأ II في الـconstitution).

### 2.4 أصول قابلة لإعادة الاستخدام للالتقاط

| الأصل | الموقع | الاستخدام المقترح |
|---|---|---|
| `ScriptSchedule` (Interval, NextRun, LastRun, StartAt, RunOnNextConnect, m:n مع Devices) | `Shared/Entities/ScriptSchedule.cs` | **نموذج الجدولة مباشرة** — يضاف نوع الالتقاط + المدة + الموافقة |
| `ScriptScheduler` + `ScriptScheduleDispatcher` | `Server/Services/` | محرّك الجدولة المركزي |
| `SessionRecordingSink` + `DataCleanupService` | `Server/Services/` | نمط تخزين الوسائط على القرص + retention — **يُوسَّع لكل نوع التقاط** |
| `AppLauncherWin` + `Win32Interop` | `Agent/Services/Windows/`, `Desktop.Native/Windows/` | إطلاق عملية التقاط داخل جلسة المستخدم |
| `MessageLoop` (STA + `SystemEvents.SessionSwitch`) | `Desktop.Win/Services/MessageLoop.cs:45-47,97` | نقطة الإيقاف التلقائي عند logout |
| `IScreenCapturer` / `IAudioCapturer` / `IFileTransferService` | `Desktop.Shared/Abstractions/` | **نقطة التجريدNatural لمحرك الالتقاط** |
| `Device` (CPU, CurrentUser, OSArchitecture, Drives) | `Shared/Entities/Device.cs` | حالة الجهاز والتelemetry |
| `ApiAuthorizationFilter` (مقيّد بالمسار وIP) | `Server/Auth/` | نمط تفويض الـAPI الجديد |

---

## 3. Fourteen-Dimension Analysis

### 1) Project Overview — `[VERIFIED]`
منتج تحكّم عن بُعد مفتوح المصدر: جهاز Windows يتصل بسيرفر ASP.NET Core؛ المتصفح يتحكم به (عن بُعد أو داخل الشبكة). ثلاثة مسارات مستقلة: `/hubs/service` (الخدمة، Session 0)، `/hubs/desktop` (العملية التفاعلية)، `/hubs/viewer` (المتصفح). المستخدمون: Technical Support (أدمن) + موظف (جهاز) + مشاهد (متصفح، داخل المؤسسة أو خارجها).

### 2) Tech Stack — `[VERIFIED]`
.NET 8 (`net8.0`؛ `Desktop.Win` و`Desktop.Win.Tests` = `net8.0-windows`) · ASP.NET Core 8 + Blazor Server + Razor Components · SignalR + MessagePack 2.5.187 · EF Core 8 (Sqlite / SqlServer / InMemory — و`Npgsql.PostgreSQL 8.0.10` مُعلن في السيرفر) · ASP.NET Core Identity + 2FA · Serilog · Avalonia 11.1.4 + CommunityToolkit.Mvvm 8.3.2 · SkiaSharp 2.88.8 + SharpDX 4.2.0 (DXGI) · NAudio 2.2.1 · PowerShell SDK 7.4.5 · TypeScript مُصرَّف بـ`Microsoft.TypeScript.MSBuild 5.6.2` (**لا npm**) · MSTest + Moq + coverlet.

### 3) Architecture — `[VERIFIED]`
Server composition root في `Program.cs`؛ registration في `Server/Extensions/`؛ العميل التفاعلي **لا يستخدم Generic Host** — `services.BuildServiceProvider()` يدوي (`Desktop.Win/Program.cs:59`) + **service locator** (`StaticServiceProvider.Instance = services` في `IServiceProviderExtensions.cs:59`). العميل يتصل **مباشرة** بالسيرفر (`DesktopHubConnection.cs:404-409` → `/hubs/desktop`) ولا يمرّ على الـAgent — الـAgent دوره في مسار التحكم الوحيد هو **إطلاق** العميل في الجلسة الصحيحة.

**هل يدعم هيكل الـAgent الحالي Capture Agent بأمان؟ نعم — بشرط:** الالتقاط ينفَّذ داخل العملية التفاعلية (لا Session 0)، والتواصل بين الخدمة والعملية التفاعلية يجب أن يكون عبر قناة **مُصادَق عليها** (pipe موجود مثال، لكنه غير مُصادَق currently — انظر §10).

### 4) Features

| التصنيف | العناصر |
|---|---|
| **موجود وقابل لإعادة الاستخدام** | device enrollment، device auth، SignalR transport، scripting + scheduling، file transfer (chunked)، session recording + retention، alerts، admin UI بـBlazor، 2FA/API keys، client auto-update |
| **موجود لكن يحتاج تعديل** | `ScriptSchedule` (لإضافة نوع الالتقاط)، `DataCleanupService` (retention لكل نوع)، `SessionRecordingSink` (تخزين وسائط خرّج من الـDB)، `Desktop.Shared` abstractions (توسعة `IAudioCapturer` للميكروفون)، `FileTransferServiceWin` (resume/idempotency)، `UpdaterWin` (توقيع + جدولة خارج أوقات العمل) |
| **مفقود ويجب تطويره** | التقاط الميكروفون (اليوم **loopback** فقط)، الكاميرا، التسجيل المجدول،Consent flow، privacy pause، upload resumable + queue 5GB، retention لكل نوع، monitor device state + upload failures، View Only مُصادَق عليه على السيرفر |

**Mic gap (مؤكد)**: `Desktop.Win/Services/AudioCapturerWin.cs:11,85` → `WasapiLoopbackCapture` = **صوت النظام الخارج**، لا ميكروفون. لا يوجد `WasapiCapture` ولا endpoint إدخال في الريبو.

### 5) Database Schema — `[VERIFIED]`
14 كيان في `Shared/Entities/`: `Alert, ApiToken, BrandingInfo, Device, DeviceGroup, InviteLink, KeyValueRecord, Organization, RemotelyUser, SavedScript, ScriptResult, ScriptRun, ScriptSchedule, SharedFile`.
**الترحيلات**: 3 مجلدات مزوّد — `Sqlite` **26** migration · `SqlServer` **20** · `PostgreSql` **20** = **66 صنف migration**؛ الأحدث `Add_KeyValueRecords` (2024‑02‑21) عبر الثلاث. الفهارس اليدوية: `RemotelyUser.UserName` + `Device.DeviceName` (`Server/Data/AppDb.cs:138,151`) وباقي الفهارس مولّدة من EF.
⚠️ **كلفة تشغيلية**: كل تغيير في النموذج = **3 مجموعات ترحيلات** (`Utilities/Add-Migration.ps1:21-23` يولّد الثلاثة). ⇒ إضافة `CaptureSchedule`/`CaptureRecord`/`CaptureConsent` = 3× ترحيلات لكل مزوّد + اختبار على الثلاثة.
نقاط التوسعة المقترحة [PROPOSED]: `CaptureSchedule` (أو توسيع `ScriptSchedule` بـ`CaptureType`)، `CaptureRecord` (metadata + hash + حالة)، `CaptureConsent` (منح لكل قدرة + صلاحية + تاريخ)، `AuditEvent`.

⚠️ **`SharedFile.FileContents` عمود `byte[]` داخل الـDB** (`Shared/Entities/SharedFile.cs:13`) — **غير صالح للوسائط الكبيرة**؛ لازم filesystem. وجود `SharedFile` كجدول مرجعي لا كـstorage.

### 6) API and Communication Design — `[VERIFIED]`
**15 controller** في `Server/API/`، كلها `[Route("api/[controller]")]`. التفويض **لا يستخدم `[Authorize]` إطلاقاً** — كله عبر `ServiceFilter` (`ApiAuthorizationFilter` / `ExpiringTokenFilter` / `LocalOnlyFilter`) أو عبر `[Authorize]` على مستوى الـBlazor components.
⚠️ **نقطتان مفتوحتان (خارج نطاق الالتقاط لكن لازم تُسجَّل):**
- `ClientDownloadsController.GetInstaller(platformId, organizationId)` (`:117-121`) و`DevicesController.Create` (`:124-135`) **بلا أي فلتر تفويض** ⇒ قابل للوصول بلا مصادقة.
- `Server/Filters/ViewerAuthorizationFilter.cs:23-27` — إذا كان `settings.RemoteControlRequiresAuthentication == false` ⇒ **التحكم عن بُعد مفتوح بلا مصادقة** (من مُفتاح في `SettingsModel`).
- `Server/RateLimiting/PolicyNames.cs:3-6` — سياسة rate-limit **واحدة فقط**، على تنزيل حزم تحديث الوكيل. لا rate limiting على الـAPI العام.

Hubs: `/hubs/desktop`, `/hubs/viewer`, `/hubs/service` (`Program.cs:308-310`). مصادقة الـAPI: `ApiAuthorizationFilter` — header `ApiKeyHeaderName` بصيغة `keyId:secret`، والتحقق `ValidateApiKey(keyId, secret, path, remoteIp)` (مسار + IP مُقيّدين) + مسار هوية المستخدم الإداري.

**بثّ الشاشة ليس WebRTC** — `[VERIFIED]`: SignalR WebSocket + MessagePack للـDTO + بثّ ثنائي للإطارات:
- `Desktop.Shared/Services/ScreenCaster.cs:205` — `EncodeBitmap(frame, SKEncodedImageFormat.Jpeg, quality)` ⇒ **JPEG**.
- `:216-222` — ترويسة إطار **28 بايت** = int32 طول + 4×float32 (X/Y/W/H للمنطقة) + int64 timestamp ⇒ يطابق `CaptureProcessor.ts:6,28-38` بالضبط.
- `:203` — `CropBitmap(result.Value, diffArea)` ⇒ **يرسل الجزء المتغير فقط** (diff).
- `:226` — تقسيم 50KB، `ViewerHub.GetDesktopStream()`، والمتصفح `ViewerHubConnection.ts:131` (`stream("GetDesktopStream")`) ← `CaptureProcessor.ts:42-48` (`createImageBitmap` + `drawImage`).

⚠️ **حدّ استقبال SignalR = 100,000 بايت** (`Server/Program.cs:194` `MaximumReceiveMessageSize`) ⇒ أي chunk (50KB الحالي) سليم، لكن **الملف المرفوع كـDTO واحد أو chunk كبير يBreaking**. ⇒ **تصميم رفع ملفات الوسائط لازم يحترم هذا الحد** (وهو يفرض chunking + streaming، لا POST ضخم).
⚠️ حدّ الرفع في الـAPI = `AppConstants.MaxUploadFileSize = 100,000,000` (100MB)؛ chunks: DTO 50KB، ملف 40KB، إطار 50KB. العقود: MessagePack `DtoWrapper` + `DtoType` (24 قيمة، `Enums/DtoType.ts`).

### 7) UI/UX Patterns — `[VERIFIED]`
لوحة الأدمن **Blazor Components** (لا Pages): `Server/Components/{Devices,Scripts,Account,Layout,ModalContents,Pages,...}`. جهاز = `DeviceCard.razor` + `DevicesFrame.razor` + `ChatCard.razor` + `Terminal.razor`؛ صفحة المشاهدة = `Server/Pages/Viewer.cshtml` (نقطة دخول المتصفح: `:254` يحمّل `/src/App.js` كـmodule، `:257` ينادي `ViewerApp.Init()`).
عميل المتصفح = **30 ملف TypeScript** في `Server/wwwroot/src/`: `App.ts` (نقطة الدخول) · `ViewerHubConnection.ts` (غلاف SignalR `/hubs/viewer` + MessagePack `:34`) · `MessageSender.ts` (20+ أمر) · `DtoChunker.ts` (50KB) · `DtoMessageHandler.ts` · `CaptureProcessor.ts` (28-byte header) · `InputEventHandlers.ts` (~16 نقطة فحص ViewOnly) · `SessionRecorder.ts` (تسجيل webm عبر MediaRecorder) · `FileTransferService.ts` · `SettingsService.ts` (إعدادات في `localStorage["Viewer_Settings"]`).
الحد الأدنى المطلوب: (أ) واجهة إدارة الجداول + حالة الأجهزة, (ب) صفحة results لكل جهاز, (ج) **مؤشر موافقة/حالة التقاط ظاهر على جهاز الموظف** (جديد — لا يوجد اليوم), (د) زر privacy pause محلي (جديد).
⚠️ **الـJS المولَّد مُودَع في git** (30 `.js` + 30 `.js.map` بجانب الـ`.ts`) ⇒ **الملف المُسلَّم فعلياً هو الـJS المُودَع**، وقد يكون أقدم من الـ`.ts` (تعليق صريح في `.azure-pipelines/Release Build.yml:88-89`: مهمة MSBuild لا تُصرّف TS بدون خطوة build منفصلة). ⇒ **أي تعديل على عميل المتصفح يجب أن يشمل إعادة توليد + commit للـJS**، وإلا لن يعمل التعديل في المتصفح.

### 8) File Structure — `[VERIFIED]`
`Server/` (API, Hubs, Components, Pages, Auth, Data, Migrations, Services, Extensions, Options, wwwroot) · `Shared/` (Entities, Interfaces, Helpers, Primitives, Services) · `Agent/` (Services/{Windows,Linux,MacOS}, Models, Interfaces) · `Desktop.Core/` · `Desktop.Shared/` (Abstractions, Services, Startup, ViewModels, Reactive) · `Desktop.UI/` · `Desktop.Linux/` · `Desktop.Win/` · `Desktop.Native/{Windows,Linux}` · `Tests/` · `Utilities/` · `docker-compose/` · `.github/` · `.azure-pipelines/`.
**نقاط التوسعة الثابتة**: `Desktop.Shared/Abstractions/` (العقود) · `Shared/Entities/` (النموذج) · `Server/Services/` (منطق السيرفر) · `Agent/Services/` (منطق الجهاز).

### 9) Dependencies — `[VERIFIED]`
أهمها: `SkiaSharp 2.88.8` (سيرفر + عميل، native!) · `SharpDX 4.2.0` (DXGI، **مهجور/قديم** — أعلى إصدار 4.2.0 منذ 2020) · `NAudio 2.2.1` · `Avalonia 11.1.4` · `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.10` (مُعلن لكن خارج switch الـ`DbProvider` المُتحقق) · `Microsoft.TypeScript.MSBuild 5.6.2` · `Microsoft.Web.LibraryManager.Build 2.1.175` (**LibMan** — مصدر المكتبات الأمامية بدل npm).
⚠️ **لا npm إطلاقاً**: `glob **/package.json` = صفر ⇒ المكتبات الأمامية (signalr 8.0.7، msgpack 3.0.0-beta2، signalr-protocol-msgpack 8.0.7، font-awesome 6.5.2) تُجلب عبر `Server/libman.json` من cdnjs/unpkg — **مع ازدواجية**: 68 ملف vendor **مُودعة** في `Server/wwwroot/lib` رغم أن LibMan يُنزّلها أيضاً.
⚠️ **لا حزم محلّلات خارجية** (`Analyzer|StyleCop|Roslynator|SonarAnalyzer` = صفر)، و`EnforceCodeStyleInBuild` غير معرَّف ⇒ قواعد `.editorconfig` ذات severity `error` **لا تُترجم إلى فشل بناء** عملياً.
مخاطر: `SharpDX` قديم؛ `SkiaSharp` يحتاج native assets لكل arch؛ **لا `packages.lock.json`** ⇒ الـrestore غير قابل للتثبيت؛ `System.CommandLine 2.0.0-beta4` (beta) في السيرفر.

### 10) Security Analysis — `[VERIFIED]`
**إيجابي:** مصادقة device قائمة على `ApiToken`، `Secret` مُخزَّن في الـDB كنص (`ApiToken.cs:22`)، `ValidateApiKey` مقيّد بالمسار+IP، 2FA، org scoping، `KnownProxies` للـforwarded headers.

**سلبي (الأخطر في الريبو — خارج نطاق الالتقاط لكن يجب تسجيله):**
1. **التحديث غير موقَّع إطلاقاً**: بحث `SignedXml|X509Certificate|Authenticode|VerifySignature` في كود العميل = **صفر نتائج**. `UpdaterWin.cs:136-146` ينزّل `Install-Remotely.ps1` + الـzip ثم `Process.Start` — **MITM ⇒ تنفيذ كود كـSYSTEM**.
2. **View Only بلا فرض على السيرفر** (§2.3).
3. **Path traversal في نقل الملفات**: `FileTransferServiceWin.cs:88` يستخدم `fileName` القادم من الطرف الآخر **حرفياً** في `Path.Combine(baseDir, fileName)` — بدون تنقية `..`.
4. **صلاحيات واسعة**: `FileTransferServiceWin.cs:157-203` يمنح `BuiltinUsersSid` صلاحية **Modify** على الملفات المستلَمة.
5. **تسريب الأسرار**: `Agent/Services/ConfigService.cs:91,96,119` → `ConnectionInfo.json` **نص صريح** بجوار الـexe.
6. `_partialTransfers` dictionary غير محدود ⇒ leak عند انقطاع نقل (انظر §11).

### 11) Performance Analysis — `[UNKNOWN]` (لا قياس)
بنية معروفة [VERIFIED]: DXGI أولاً ثم BitBlt مع **change detection** (`GetNextFrame` → "No screen changes occurred") + **إرسال الجزء المتغير فقط** (`ScreenCaster.cs:203` `CropBitmap(diffArea)`) ⇒ تكلفة idle منخفضة وحجم بث صغير. الترميز الحالي JPEG لحظي (SkiaSharp) — **ليس تسجيل ملف**، فلا يصلح مؤشراً على أداء التسجيل.
**لا يوجد أي قياس** لـ CPU/RAM/encoding على أجهزة Intel 5th–10th. **[UNKNOWN]** كيف تتأثر أداء المشاهدة عن بُعد أثناء تسجيل → يجب قياسه (المبدأ VII).
خطر معماري [INFERRED]: التسجيل + البث في نفس العملية = contention علىencoder؛ الحل فصل العملية.

### 12) Testing Strategy — `[VERIFIED]`
`dotnet test` (MSTest) على `master`. الإجمالي **26 test method**: `Server.Tests` 18 (AgentHub 2، CircuitConnection 6 — كلها عن **استيقاظ الأجهزة**، DataService 8، ScriptScheduleDispatcher 2) · `Shared.Tests` 1 · `Desktop.Win.Tests` 7 (**كلها `[Ignore("Manual test.")]` على الصنف ⇒ لا تُنفَّذ**) ⇒ **19 تُنفَّذ فعلياً**.
**فجوات حرجة**: لا تغطية لـ`ViewerHub` ولا `DesktopHub` ولا `AgentHub` (غير مسارَي الحظر) · **لا تغطية لمسار `RemoteControl(deviceId, viewOnly)` ولا `EnforceAttendedAccess`** · لا تغطية لنقل الملفات · لا تغطية لالتقاط الشاشة (كل اختبارات الـcapture يدوية ومُتجاهَلة) · **صفر تغطية لعميل TypeScript** (لا Jest/Vitest) ⇒ `CaptureProcessor`/`DtoChunker`/`InputEventHandlers`/`MessageSender` بلا أي اختبار.
**مشاكل CI مؤكَّدة:**
- ⚠️ **`.github/workflows/run_tests.yml:37` و`build.yml:44,107` يثبّتون SDK 7** بينما كل المشاريع `net8.0` ⇒ `NETSDK1045` متوقع. (`.azure-pipelines/Release Build.yml:50` يستخدم 8.x ✓).
- ⚠️ **`Tests/Desktop.Win.Tests` غير مُدرج في `Remotely.sln`** ⇒ لا يعمل في أي CI إطلاقاً.
- ⚠️ `coverlet` موجود في `Shared.Tests` و`Desktop.Win.Tests` فقط — **ليس** في `Server.Tests` (18 اختباراً بلا تغطية).
- ⚠️ `build.yml:33` يستخدم runner **`macos-10.15` المهجور**؛ و`.azure-pipelines/Release Build.yml:103` يستخدم `$(BuildConfiguration)` **غير معرَّف** ⇒ `--configuration` بلا قيمة.

### 13) Anti-Patterns — `[VERIFIED]`
- **God objects**: `Server/Services/DataService.cs` (>2300 سطر، واجهة `IDataService` بعشرات methods) · `Desktop.Core/Services/Viewer.cs` · `Agent/Services/AgentHubConnection.cs`.
- **شجرة مكرّرة ميتة**: `Desktop.Core/` (يعتمد `SignalR.Client 6.0.6` — أقدم من `Desktop.Shared` 8.0.10) فيه نسخة مكرّرة من `Viewer/DtoMessageHandler/ChatHostService` + `CasterSocket`; **ليس مُشاراً إليه من أي csproj** ⇒ كود ميت يربك القراءة.
- **Service locator**: `Desktop.Shared/StaticServiceProvider.cs` + `IServiceProviderExtensions.cs:59`.
- **`async void` handlers**: `AudioCapturerWin.cs:32`، `UpdaterWin.cs:167`.
- **تسريب موارد**: `_partialTransfers` غير محدود (`FileTransferServiceWin.cs:22`).
- **⚠️ مسار vendor مكسور**: `Server/Pages/Viewer.cshtml:33,38` يشير إلى `/lib/microsoft-signalr-protocol-msgpack/...` بينما الملف الفعلي في `wwwroot/lib/microsoft/signalr-protocol-msgpack/` ⇒ **404 ⇒ `signalR.protocols.msgpack` غير معرّف ⇒ عميل المتصفح لا يعمل في Development**.
- **⚠️ فشل صامت في أداة المشغّل**: `Utilities/Update-Database.ps1:39,43,47,51` يبحث عن `"DBProvider"` بينما المفتاح الفعلي `"DbProvider"` (`appsettings.json:24`) والبحث بـ`.Contains()` حسّاس لحالة الأحرف ⇒ **السكربت لا يبدّل مزوّد القاعدة إطلاقاً**.
- **⚠️ متغير بيئة ميت**: `docker-compose.yml:38` يمرّر `Remotely_ApplicationOptions__DockerGateway` بينما الخاصية في `ApplicationOptions.cs:7` اسمها **`DockerGatewayIp`** ⇒ القيمة تعمل فقط لأن `appsettings.json:25` يعرّفها صراحةً.
- **⚠️ `.editorconfig:1` ترويسة غير قياسية** `[ "*.cs" ]` ⇒ قواعد `:3-28` (CA1416/CS1591/CA1822/var) **قد لا تُطبَّق أصلاً**، و`EnforceCodeStyleInBuild` غير معرَّف ⇒ قواعد severity `error` لا تُترجم إلى فشل بناء.
- **⚠️ لا Caddyfile في الريبو** رغم أن README يعلن Caddy الوكيل المعتمد الوحيد ⇒ لا يوجد proxy جاهز في `docker-compose/` (خدمة واحدة فقط: `immybot/remotely:latest`، منفذ 5000 HTTP، volume `/var/www/remotely` → `/app/AppData`).
- **موارد ثابتة**: `Program.cs` (سيرفر) + `ApplicationOptions` + `FileLoggerDefaults`.
- **لا single-instance guard**: بحث `Mutex` في الريبو = **صفر** ⇒ عميل سطح المكتب متعدد النسخ ممكن ⇒ **خطر التقاط مزدوج** لو أُضيف عملية التقاط.

### 14) Missing Features — Gap Analysis
انظر §6 (الجدول التفصيلي). الخلاصة: **5 من 8 قدرات مطلوبة غير موجودة**؛ الموجود منها: البنية التحتية للجدولة، البنية التحتية للتخزين+retention، وقناة الاتصال المؤمّنة — وكلها تحتاج توسعة لا بناء.

---

## 4. ScreenRecorderLib Compatibility Assessment

### 4.1 التغليف — `[VERIFIED]` (الأهم في هذا القسم)
**هذه ليست مكتبة C# مُدارة إطلاقاً.** لا يوجد أي مشروع مكتبة مُدار في المستودع:
- `ScreenRecorderLib/ScreenRecorderLib.vcxproj:31` — `TargetFrameworkVersion` = **v4.8** · `:38` `ConfigurationType=DynamicLibrary` · `Keyword=ManagedCProj` + `CLRSupport=true` ⇒ **C++/CLI mixed-mode** على .NET Framework.
- `ScreenRecorderLibNative/ScreenRecorderLibNative.vcxproj:198…` — `ConfigurationType=StaticLibrary` ⇒ **الكود الأصلي مربوط ساكناً داخل الـDLL المُدار** ⇒ **لا `ScreenRecorderLibNative.dll` منفصل ولا `DllImport`** (بحث `DllImport` = صفر نتائج في المستودع كله).
- **لا يوجد `netstandard` ولا `netcoreapp` ولا `TargetFrameworks` في أي مكان** ⇒ `netstandard2.0` **غير متاح**؛ ولا يوجد `PackageReference` واحد ⇒ **صفر تبعيات NuGet**.
- `Nuget/ScreenRecorderLib.nuspec` v7.0.1: ملف واحد لكل معمارية (`build/{x86,x64,ARM64}/ScreenRecorderLib.dll`)، **لا `lib/` folder**، و**الحزمة ليست TFM-aware** (لا `packageTargetFrameworks` ولا `dependencyGroups` في كتالوج NuGet الرسمي) ⇒ تُعامَل كحزمة legacy بلا تفاوض TFM.
- `Nuget/ScreenRecorderLib.targets:4-7` — `PlatformCheck` يرفع **Error** إن لم يكن `$(Platform)` ∈ {x86, Win32, x64, ARM64} ⇒ **AnyCPU ممنوع**؛ و`:11-22` يحقن `<Reference>` بـ`HintPath` (مرجع تجريبي لا مرجع حزمة).
- `ScreenRecorderLib/AssemblyInfo.cpp:32` — `AssemblyVersion = "1.0.*"` دائماً ⇒ **تعارض إصدارات** مع نسخة الحزمة 7.0.1.
- `Nuget/pack.cmd:1` — التغليف **يدوي بالكامل**، وليس في CI.

**متطلبات نظام التشغيل** (`README.md:2`): Windows 8+ · **Visual C++ Redistributable** مطابق للمعمارية · **Media Foundation** — وهي **غير مثبّتة افتراضياً** على Windows Server، وعلى نسخ Windows **N/KN** تحتاج "Media Feature Pack". ⇒ **شرط تشغيلي إضافي على أجهزة الموظفين** (وعملاء الـServer).

**أين الفخ الحقيقي:** التحميل من عملية `net8.0`. Microsoft توثّق أن C++/CLI لا يستهدف .NET Standard، وأن مسار .NET المدعوم هو `/clr:netcore` + `<CLRSupport>NetCore</CLRSupport>` + `<TargetFramework>net8.0</TargetFramework>`. و`dotnet/runtime#10333` ينص أن الـassemblies المبنية على .NET Framework "مربوطة بتهيئة .NET Framework" وتحتاج **إعادة بناء** للعمل بشكل صحيح على .NET Core/5+ (مع ملاحظة أن بعضها يعمل في وضع توافق). ⇒ **المسار المُوصى به: بناء `ScreenRecorderLib.vcxproj` محلياً** (يحل مشكلة الحزمة + يتيح `CLRSupport=NetCore`) بدل الاعتماد على NuGet.

### 4.2 القدراتnative — `[VERIFIED]`
`ScreenRecorderLibNative/`: `DesktopDuplicationCapture.cpp` (DXGI) · `WindowsGraphicsCapture.cpp` (WGC) · `WASAPICapture.cpp` (صوت) · **`CameraCapture.cpp` (كاميرا — موجود!)** · `AudioManager.cpp` (خلط) · MF `CMFSinkWriterCallback` (ترميز) · `MouseManager.cpp` (تراكب مؤشر) · `TimelineManager.cpp` · `Resizer.cpp` · `OutputManager.cpp`.
الـAPI المُدارة (`Recorder.h`): `Recorder.CreateRecorder(options)`, `RecorderOptions{ SourceOptions, AudioOptions }`, `SourceOptions{ OutputDimensions, SourceCoordinates }`, `AudioOptions{ AudioSources }`, `AudioSourceBase` (مشتقات: `CaptureAudioSource` = microphone، `LoopbackAudioSource` = صوت النظام), `DisplayRecordingSource.MainMonitor`, `RecorderStatus.Idle/Paused`, أحداث `OnRecordingComplete`/`OnStatusChanged`, `Record(path)/Stop()/Pause()/Resume()`.

### 4.3 مطابقة المتطلبات الخمسة

| المطلوب | الحالة | الدليل / الفجوة |
|---|---|---|
| لقطات شاشة مجدولة لشاشتين | **مدعوم** | `Recorder.GetDisplays()` + `DisplayRecordingSource` (متعدد المصادر في `SourceOptions.RecordingSources`) + `TakeSnapshot(path)`. ⚠️ **شاشة محددة فقط** — لا مصدر "virtual desktop"، والتركيب متعدد الشاشات = عدة `DisplayRecordingSource` مع `Position`/`AnchorPoint`. ⚠️ **لا DPI awareness** (`SetProcessDpiAwareness` غير موجود) ⇒ على شاشتين مختلفتي الـDPI الإحداثيات قد تكون غير مربعة |
| تسجيل شاشة + صوت ميكروفون | **مدعوم** | `CaptureAudioSource` (ميكروفون/نقطة إدخال) + `LoopbackAudioSource` (صوت النظام) + `AudioManager::MixAudioSamples` |
| تسجيل ميكروفون فقط | **مدعوم** *(تصحيح لما سبق)* | `Options.h:123-125` — `IsVideoCaptureEnabled=false` ⇒ *"only audio is recorded if enabled, and video will be black"* ⇒ الناتج MP4 بمسار صوتي وصورة سوداء |
| لقطات كاميرا | **مدعوم** | `VideoCaptureRecordingSource` + `RecordableCamera` + `GetSystemVideoCaptureDevices()` + `GetSupportedVideoCaptureFormatsForDevice()`؛ و`TakeSnapshot` تلتقط **المصدر النشط** ⇒ لقطة كاميرا ممكنة |
| فيديو كاميرا **بدون صوت** | **مدعوم** | `VideoCaptureRecordingSource` + `AudioOptions.IsAudioEnabled=false` ⇒ MP4 بلا مسار صوت |

**قدرات إضافية مهمة للمشروع:**
- `Recorder.SetExcludeFromCapture(IntPtr hwnd, bool)` ⇒ **استبعاد نافذة من الالتقاط** — capability مباشرة لمطلب «استثناء محتوى الاجتماعاء» (لكنه مبني على HWND ⇒ يتاح فقط في الجلسة التفاعلية).
- `Recorder.GetWindows()` ⇒ تعداد النوافذ مع `Title` + `Pid` ⇒ ممكن كشف نافذة اجتماع بالاسم (كمدخل policy لا كضمان).
- الترميز: **Media Foundation داخل العملية**، H.264/H.265، مع `IsHardwareEncodingEnabled` اختياري (اختيار MFT عبر `MFTEnumEx`)؛ الحاوية **MP4 فقط** (`GetVideoExtension()` → `.mp4`).
- `IsFragmentedMp4Enabled` / `IsMp4FastStartEnabled` / `IsFixedFramerate` / `Bitrate` / `Quality` / `Framerate` — كل knobs مطلوبة لـ«مدة/فترة قابلة للضبط».

### 4.4 المخاطر والافتراضات غير المتحققة
1. **`[VERIFIED]` حاجز mixed-mode**: consumption من `net8.0` غير مضمون (Microsoft تنصح بإعادة البناء على `/clr:netcore`). الحل: بناء محلي للـvcxproj.
2. **`[VERIFIED]` تعارض AnyCPU**: Remotely يبني `Desktop.Win` بـ`AnyCPU;x86;x64` ⇒ **يجب تثبيت `x64`** في بناء العميل — وهذا يمس الـinstaller والـupdate.
3. **`[VERIFIED]` متطلبات تشغيل إضافية**: VC++ Redistributable + **Media Foundation** (غير مثبتة على Windows Server / N / KN) ⇒ تُضاف لقائمة preflight لكل جهاز.
4. **`[VERIFIED]` لا DPI awareness** ⇒ خطر على شاشتين مختلفتي الـDPI (متطلب الشاشتين).
5. **`[VERIFIED]` لا فحص Session 0**: لا `WTS`/`CreateDesktop`/فحص window station ⇒ **لا دليل في المصدر على ملاءمة Session 0** (متسق مع المبدأ IV: الالتقاط في الجلسة التفاعلية فقط).
6. **`[VERIFIED]` لا حدود صريحة** على الدقة/الإطارات/المدة/حجم الملف ⇒ التحقق منها مسؤولية التكوين.
7. **`[VERIFIED]` لا single-instance mutex** داخل المكتبة ⇒ تعدّد `Recorder` ممكن ⇒ **يجب** قفل على مستوى الجهاز في Remotely.
8. **`[UNKNOWN]` الأداء** على Intel 5th gen + شاشتين: لا benchmark في الريبو ولا قياس محلي.
9. **`[VERIFIED]` MP4 مجزّأ**: إصلاح #375 موجود في **master فقط** ⇒ `v7.0.1` على NuGet (=`6463832`) **لا يتضمنه** (master = commit واحد متقدّم).
10. **`[VERIFIED]` الرخصة**: MIT — **متوافقة مع GPL-3.0**، الشرط الوحيد: الإبقاء على إشعار حقوق الملكية والإذن في التوزيع (والنشرة تشحن LICENSE أصلاً).
11. **`[VERIFIED]` صيانة**: **bus factor = 1** — 759 من 805 commit (~94%) لمساهم واحد؛ نشاط 2025 هبط إلى 18 commit ثم تعافى 2026 إلى 48. **CI لا يشغّل الاختبارات إطلاقاً** (`msbuild.yml` يبني فقط) ⇒ لا شبكة أمان عند الترقية.
12. **`[VERIFIED]` لا تغطية اختبار للكاميرا** ولا لصوت صامت ⇒ السلوك في هذه المسارات غير مُختبَر من المؤلف.

### 4.5 الحكم
ScreenRecorderLib **يغطي وظيفياً كل المتطلبات الخمسة** (شاشة/شاشتين، ميكروفون، صوت فقط، كاميرا، كاميرا صامتة) + `SetExcludeFromCapture` Privacy + knobs المدة/الترميز — وهذا **أقوى** مما كنت أظن. لكن **تبنّيه كمحرك وحيد الآن مخاطرة بنيوية**: mixed-mode v4.8 بلا netstandard، AnyCPU ممنوع، VC++ redist + Media Foundation مطلوبان، DPI غير مُعالَج، CI بلا اختبارات، bus factor = 1.
⇒ **القرار المقترح: تبنَّه خلف abstraction** (`ICaptureEngine`)، مع **بناء محلي للـvcxproj** بدل الاعتماد على NuGet، ومع **محرك بديل مُجمَّل** (MF/WGC مُدار، أو `Windows.Graphics.Capture`) كخطة B — لأن `Windows.Graphics.Capture` و`Media Foundation` متاحان أصلياً على Windows 10/11 بدون redist.

---

## 5. Architecture Decision

### 5.1 مقارنة الخيارين

| المعيار | **A. توسيع الـAgent بمكوّن التقاط معزول في جلسة المستخدم** | **B. Capture Agent منفصل منتشر** |
|---|---|---|
| حدود العمليات | عملية التقاط **داخل** جلسة المستخدم (يفضَّل: داخل عملية Desktop الحالية) | عملية/خدمة منفصلة + Porter منفصل |
| Session 0 | الالتقاط **لا يعمل** في Session 0 — التنفيذ في الجلسة التفاعلية | نفس القيد — لا فرق |
| التأثير على View Only | منخفض: لا يمس مسار التحكم؛ بس **`HasControl` يجب إصلاحه بشكل منفصل** | أعلى: عميل ثالث على نفس الجهاز قد يصطدم بالـdesktop |
| CPU/RAM | إضافة عملية واحدة فقط؛ idle ≈ صفر | عميلان + traffic إضافي + Surface duplication |
| التثبيت والتحديث | ممتد لعميل واحد (نسخة واحدة تُحدَّث مع الـAgent) | **مكوّن تثبيت وتحديث ثانٍ** ⇒ ضد أولوية #5 |
| عزل الفشل | فشل الالتقاط لا يوقف الـAgent ولا الـServer | أفضل عزل نظري، لكن بتكلفة تشغيلية |
| الصلاحيات | عملية التقط تلتزم الـdesktop session | عملية إضافية ⇒ سطح هجوم أكبر |
| الارتباط بـupstream | منخفض (abstraction جديد + نقاط توسعة) | متوسط (بروتوكول مشترك) |
| تكلفة الصيانة | منخفضة — مسار واحد | **عالية** — منتجان إلى التشغيل |
| توافق ScreenRecorderLib | مباشرة (نفس x64/AnyCPU constraint) | نفس القيد + القيود منفصلة |

### 5.2 التوصية — **الخيار A** (بشكل محدَّد)

**التوصية:** توسيع الـAgent الحالي بمكوّن التقاط يعمل داخل **عملية Remotely التفاعلية القائمة** (Desktop.Win)، مع:
1. **Abstraction** على نمط `Desktop.Shared/Abstractions/`: `ICaptureEngine` (واجهات: `CaptureScreen`, `CaptureMicrophone`, `CaptureCamera`, `Stop`) ⇒ استبدال المحرك لاحقاً ممكن.
2. **الخدمة (Agent)** تبقى مسؤولة عن: طابور الملفات (5GB)، الرفع، الاستئناف، الاستمرارية بعد الـlogout.
3. **قناة service↔session مُصادَق عليها**: الـpipe الحالي (`ChatHostService`) غير مُصادَق (اسم pipe عشوائي GUID يُمرَّر في سطر الأوامر) ⇒ **يجب** إضافة مصادقة/توقيع للرسائل (HMAC بمفتاح مشترك بين الخدمة والعملية) + **single-instance mutex** (غير موجود اليوم ⇒ خطر التقاط مزدوج).
4. **نقطة الإيقاف عند الـlogout**: `Desktop.Win/Services/MessageLoop.cs:45-47` (`SystemEvents.SessionSwitch`) + `Process.GetCurrentProcess().SessionId` ⇒ ربط الإيقافبه.

**لماذا A وليس B (بأدلة):**
-split الموجود فعلاً ومُختبَر في الإنتاج (AppLauncherWin) ⇒ لا نحتاج بنية session جديدة.
- الكود الأساسي للالتقاط (`IScreenCapturer` + DXGI + change detection) موجود — ScreenRecorderLib يُضاف كمحرك **بديل/مكمّل** لا كبداية من الصفر.
- الفاقد الأساسي (mic/camera/الجدولة/الموافقة/الرفع) كله **منطق جديد** لا علاقة له بنشر عميل ثالث.
- B يخالف أولويتي #5 (صغر التثبيت) و#6 (أقل تغييرات على Remotely) صراحةً.

### 5.3 شروط تغيّر التوصية
1. **لو PoC أثبت أن ScreenRecorderLib لا يعمل على Win10/11 بدون .NET Framework** ⇒ نُبقي الـabstraction ونكتب محركاً بديلاً مُداراً (MF/WGC) — **التوصية المعمارية (A) لا تتغير**.
2. **لو Desktop.Win تبيّن غير مستقر كـhost للالتقاط** (Avalonia + DI يدوي + service locator + لا single-instance) ⇒ **A′**: عملية التقاط **منفصلة خفيفة** داخل نفس جلسة المستخدم،-launchها نفس `AppLauncherWin` (لا عميل مستقل جديد ولا Porter ولا تثبيت منفصل) — حل وسط يحافظ على A'.
3. **لو الامتثال يتطلب التقاطاً بحساب مخصص غير حساب المستخدم التفاعلي** ⇒ A وB كلاهما غير صالح كما هو؛ يلزم تصميم جلسة مخصصة (خارج النطاق الحالي).
4. **لو الأجهزة بها جلسة تفاعلية غير متاحة (kiosk / always-locked)** ⇒ الالتقاط **يتوقف** (fail-closed) — متسق مع المبدأ III.

---

## 6. Requirement-to-Component Mapping

| # | المتطلب المؤكَّد | المكوّن | الحالة | الملاحظات |
|---|---|---|---|---|
| R1 | لقطة شاشة مجدولة للشاشتين | **جديد** (`CaptureSchedule` + `ICaptureEngine`) + `ScriptScheduler` (جدولة) | **جديد** | `ScreenCapturerWin` يعطي الشاشة؛ ScreenRecorderLib cams. النتيجة على FS لا DB |
| R2 | تسجيل شاشة + صوت ميكروفون | **جديد** (mic) — الميكروفون غير موجود اليوم | **جديد** | `AudioCapturerWin` اليوم loopback فقط · ScreenRecorderLib: `CaptureAudioSource` |
| R3 | تسجيل ميكروفون فقط | **جديد** | **جديد** | ScreenRecorderLib: `IsVideoCaptureEnabled=false` ⇒ صوت فقط (صورة سوداء) — مدعوم على مستوى المصدر |
| R4 | لقطة كاميرا | **جديد** | **جديد** | ScreenRecorderLib: `VideoCaptureRecordingSource` + `TakeSnapshot` |
| R5 | فيديو كاميرا بدون صوت | **جديد** | **جديد** | ScreenRecorderLib: `IsAudioEnabled=false` — مدعوم على مستوى المصدر |
| R6 | جدول مستقل لكل نوع + مدة/فترة | `ScriptSchedule` **موسَّع** | **تعديل** | حقل `CaptureType` + `Duration/Interval` |
| R7 | جدول لكل جهاز يُدار مركزياً | `ScriptSchedule` m:n + `Device` | **موجود + تعديل** | نمط m:n موجود |
| R8 | أولوية تسجيل الشاشة على الميكروفون | **جديد** (policy في الـAgent) | **جديد** | قاعدة أولوية في طابور الخدمة |
| R9 | توقف الالتقاط بعد logout والخدمة تستمر | `MessageLoop` + `Agent` service | **موجود + تعديل** | `SessionSwitch` + queue في الخدمة |
| R10 | موافقة مُعلنة + موافقة كاميرا منفصلة + مؤشر ظاهر | **جديد** (`CaptureConsent` + UI على الجهاز) | **جديد** | `RequireConsent` في `DesktopHub` نموذج قابل للتقليد |
| R11 | privacy pause محلي | **جديد** (زر + حالة) | **جديد** | محلي بالكامل — لا يُدار من السيرفر |
| R12 | تخطي الالتقاط عند عدم ضمان الخصوصية | **جديد** (policy fail-closed) | **جديد** | Adaptive = [UNKNOWN] ⇒ تخطي |
| R13 | طابور محلي 5GB | **جديد** (في الخدمة) | **جديد** | `ScriptResult`/queue؛ eviction policy |
| R14 | رفع كل الأنواع الخمسة بعد الانتهاء | `FileTransferService` (مُوسَّع) + `SessionRecordingSink` | **تعديل** |Chunks 40KB موجودة — idempotency ناقصة |
| R15 | استئناف بدون تكرار | **جديد** | **جديد** | اليوم: no resume, no retry, leak |
| R16 | retention مختلف لكل نوع | `DataCleanupService` + `SettingsModel` | **تعديل** | اليوم global 90 يوم |
| R17 | مراقبة حالة الجهاز + فشل الرفع + التخزين + الجداول | **جديد** (Blazor) | **جديد** | `Device` فيه telemetry بالفعل |
| R18 | تحديث موقَّع خارج أوقات العمل | `UpdaterWin` | **تعديل + إصلاح أمني** | **اليوم بلا توقيع إطلاقاً** |
| R19 | الحفاظ على View Only وفصل صلاحياته | **خارج نطاق الالتقاط** | **إصلاح مستقل** | §2.3 — لا يُدمج في feature |
| R20 | Windows Service منفصل عن الالتقاط التفاعلي | `Agent` + `Desktop` (موجود) | **موجود ✓** | المبدأ IV |

---

## 7. Spec Kit Status

### 7.1 الحالة — `[VERIFIED]`
| العنصر | النتيجة |
|---|---|
| `specify` CLI على PATH | **غير موجود** |
| `uv` / `uvx` / `pipx` | **غير موجودين** |
| `.specify/` في الريبو | **غير موجود** (قبل هذا العمل) |
| `specs/` في الريبو | **غير موجود** |
| `CLAUDE.md` / `.claude/` / `.cursorrules` / `copilot-instructions.md` | **غير موجودة** |
| Slash-command files للـspeckit في الـruntime | **غير موجودة** |
| `AGENTS.md` | **موجود** (أُنشئ في مرحلة سابقة) — تم الحفاظ عليه |

### 7.2 الأوامر الرسمية (من README الرسمي لـspec-kit، تم الجلب من `github.com/github/spec-kit`)
```bash
uv tool install specify-cli
specify init <project-name> --integration <agent-key>
cd <project-name>
```
ولريبو قائم: `specify init . --integration <agent-key>` (راجع «Existing projects»). أرقام الـintegration تُختار حسب الوكيل — مرجعها الرسمي: `github.github.io/spec-kit/integrations/index.html`.

### 7.3 الأوامر (skills) في شات الوكيل — بصيغة الشرطات (الهيمنة)
```
/speckit-constitution
/speckit-specify
/speckit-plan
/speckit-tasks
/speckit-implement
/speckit-converge
```
(ملاحظة: الصيغة الحديثة ** بشرطة** `-` وليس بنقطة `.`؛ والأمر يُنفَّذ **في شات الوكيل** لا في الطرفية.)

### 7.4 ما تم إنشاؤه في هذه المرحلة
✅ **ملف واحد فقط**: `.specify/memory/constitution.md` (13,081 بايت) — constitution v1.0.0، 11 مبدأ + قسم Governance (تعديل/إصدار/امتثال/تصديق).
❌ **لم** يُنشأ: `specs/`, أي spec feature، أي plan/tasks/implement.

### 7.5 المسارات
- Constitution: `.specify/memory/constitution.md`
- التقرير: `REVERSE-ENGINEERING-REPORT.md` (جذر الريبو)

---

## 8. Specification Status

**الحالة: لم يُنشأ أي spec — بانتظار تصديق الـconstitution النهائي.** (قاعدة Part 3: "STOP after constitution creation… Do not automatically invoke specify until the constitution is approved.")

**⚠️ إعادة ترقيم بقرار المالك (2026-11-06):**

| المواصفة | المحتوى | المسار المتوقع |
|---|---|---|
| **SPEC-001** | **Remote Access / View Only Authorization Hardening** — فرض view-only على السيرفر + تفويض رسائل التحكم + حماية تغيّر الجلسة + دفاع متعمّق على الجهاز + اختبارات أمنية سلبية. مواصفة أمنية مستقلة **قبل** أي عمل على الالتقاط. | `specs/001-remote-access-view-only-authorization-hardening/spec.md` |
| **SPEC-002** | **Remotely Foundation and Capture Integration** — المواصفة التي كانت مقترحة كـSPEC-001: الحفاظ على التسجيل + View Only · تدفقات الأدمن والموظف · حدود Service ↔ Capture Helper · موافقة وخصوصية قبل أي التقاط · **إثبات end-to-end واحد فقط**: طلب لقطة شاشة بموافقة → إنشاء نتيجة محلية → نقل آمن → إقرار السيرفر → ظهور النتيجة للأدمن · تسجيل مخاطر التوافق قبل تنفيذ باقي features. | `specs/002-remotely-foundation-and-capture-integration/spec.md` |

**سبب الترتيب:** §2.3 أثبت أن View Only **ليس** حدّ صلاحيات على السيرفر. تنفيذ الالتقاط أولاً يعني توسيع سطح المراقبة على جهاز فيه مسار تحكم غير مفروض.

**مواصفات لاحقة (خارج SPEC-001/002):** الجدولة المتكررة الكاملة · تسجيل شاشة بصوت · تسجيل صوتي مستقل · كاميرا صورة وفيديو صامت · تقسية الرفع + حد 5GB · retention ومكتبة الوسائط · إدارة التحديث · سياسة الالتقاط التكيفي أثناء المكالمات.

**الأداة:** `specify-cli 1.3.1` مثبَّت (تفصيل التنفيذ والدليل في `CONSTITUTION-REVIEW.md` §10) · تكامل `codex` مهيّأ · المهارات في `.agents/skills/` بصيغة `$speckit-constitution` / `$speckit-specify`.

---

## 9. Risks and Blockers

| # | الخطورة | الدليل | الأثر | التحقق المقترح |
|---|---|---|---|---|
| R-01 | **حرجة** | `UpdaterWin.cs:136-146` — تنزيل وتشغيل بلا توقيع/هاش | MITM ⇒ تنفيذ كود كـSYSTEM على كل جهاز | توقيع Authenticode + تحقق الهاش — **خارج نطاق الالتقاط لكن يجب حسمه** |
| R-02 | **حرجة** | View Only بلا فرض على السيرفر (§2.3) | ادعاء «صلاحيات منفصلة» غير صحيح اليوم | تنفيذ check على السيرفر + device — قرار مستقل |
| R-03 | **عالية** | ScreenRecorderLib mixed-mode v4.8 + AnyCPU ممنوع + لا netstandard | تعطّل بناء العميل / عدم تشغيل على أجهزة بدون .NET Framework | **PoC على Win10 + Win11 x64** + بناء محلي للـvcxproj بـ`CLRSupport=NetCore` |
| R-03b | **عالية** | `README.md:2` — VC++ Redistributable + **Media Foundation** مطلوبان | فشل تشغيل على نسخ N/KN أو أجهزة بلا MF | preflight check + Media Feature Pack في حزمة التثبيت |
| R-03c | **متوسطة** | لا `SetProcessDpiAwareness` في المكتبة | إحداثيات/حجم خاطئ على شاشتين مختلفتي الـDPI | ضبط DPI awareness في العملية قبل التهيئة + اختبار على إعدادين مختلفين |
| R-03d | **متوسطة** | ScreenRecorderLib: 759/805 commit لمساهم واحد، وCI لا يشغّل الاختبارات | fork/carrier risk عند الاعتماد | تقييم محرك بديل مُجمَّل (MF/WGC) كـplan B |
| R-04 | **عالية** | لا قياس أداء إطلاقاً | اختيار المحرك/الإعدادات بلا أساس | benchmark على Intel 5th gen + شاشتين |
| R-05 | **عالية** | `FileTransferServiceWin.cs:88` — `fileName` حرفي | path traversal عند استلام ملفات | تنقية المسار + allow-list |
| R-06 | **عالية** | لا single-instance guard (Mutex=0) | التقاط مزدوج من عميلين | mutex على مستوى الجهاز قبل الالتقاط |
| R-07 | **متوسطة** | `_partialTransfers` غير محدود + لا resume/retry | فقد ملفات + leak | طابور محدود + idempotency بالمحتوى |
| R-08 | **متوسطة** | `SharedFile.FileContents` `byte[]` | وسائط كبيرة داخل الـDB | تخزين على FS (نمط `SessionRecordingSink`) |
| R-09 | **متوسطة** | `ConnectionInfo.json` نص صريح | تسريب بيانات الاتصال | تشفير أو DPAPI |
| R-10 | **متوسطة** | `SharpDX 4.2.0` مهجور | خطر صيانة على مسار الالتقاط | تقييم استبدال بـDXGI/VT أو WGC |
| R-11 | **عالية** | `run_tests.yml:37` + `build.yml:44,107` تثبّت SDK 7 والمشاريع `net8.0` | `NETSDK1045` ⇒ **CI على GitHub معطّل فعلياً** | تثبيت SDK 8 في الـworkflows (سطر واحد لكل ملف) |
| R-11b | **عالية** | `Viewer.cshtml:33,38` مسار vendor خاطئ ⇒ 404 على msgpack | **عميل المتصفح لا يعمل في Development** | تصحيح المسار + فحص 404 في smoke test |
| R-11c | **متوسطة** | `Update-Database.ps1:39` يبحث عن `"DBProvider"` ≠ `"DbProvider"` | **فشل صامت**: السكربت يظن أنه بدّل المزوّد ولم يفعل | إصلاح المطابقة + اختبار على appsettings |
| R-11d | **متوسطة** | لا Caddyfile في الريبو رغم إعلان README | نشر بدون reverse proxy ⇒TrustedProxies/HTTPS معطّل | توفير Caddyfile مُدار أو توثيق إعداد يدوي |
| R-15 | **عالية** | `RemoteControlController.cs:138-145,176` — جلسة API بلا ViewOnly | **التحكم عن بُعد عبر API = full-control دائماً** | إما تمرير viewOnly في الـAPI، أو توثيقه كـ[UNKNOWN] حدّ صلاحيات |
| R-16 | **متوسطة** | `Program.cs:194` `MaximumReceiveMessageSize = 100,000` | أي رفع ملف عبر SignalR أكبر من 100KB يفشل | chunking ≤ 50KB + streaming للوسائط |
| R-17 | **متوسطة** | 30 ملف `.js` مولَّد **مُودَع** في git | التعديل على `.ts` بلا إعادة توليد = كود قديم مُسلَّم | خطوة build منفصلة + commit للـJS (كما في pipeline) |
| R-18 | **متوسطة** | 3 مجموعات ترحيلات (Sqlite 26 / SqlServer 20 / PostgreSql 20) | 3× تكلفة ترحيل لكل كيان جديد | أي كيان التقاط جديد يُضاف للثلاثة + اختبار على الكل |
| R-19 | **عالية** | `ClientDownloadsController.GetInstaller` و`DevicesController.Create` بلا فلتر تفويض؛ و`ViewerAuthorizationFilter.cs:23-27` يفتح التحكم عن بُعد بلا مصادقة إذا أُطفئ `RemoteControlRequiresAuthentication` | سطح وصول غير محمي على سيرفر مكشوف للإنترنت | إغلاق المسارات غير المصادَق + تفعيل الـsetting افتراضياً |
| R-20 | **منخفضة** | لا rate-limit إلا على تنزيل حزم التحديث | لا حماية من الإساءة على الـAPI | إضافة سياسات rate-limit أساسية |
| R-21 | **حرجة** | `Win32Interop.cs:121-145` — التوكن مشتق من **winlogon = SYSTEM** ⇒ العملية التفاعلية **SYSTEM** لا الموظف | نموذج موافقة مبني على افتراض كاذب ⇒ عملية SYSTEM "توافق" نيابةً عن الموظف | قرار هوية صريح + موافقة منسوبة للموظف + `whoami /all` في PoC |
| R-22 | **عالية** | `ResolveDesktopName` → `"Winlogon"` (سطح المكتب الآمن) أثناء الدخول/UAC | التقاط أثناء سطح المكتب الآمن ⇒ إطار غير متوقع أو تسريب UAC | تعليق الالتقاط على السطح الآمن + اختبار |
| R-12 | **منخفضة** | `Desktop.Core/` شجرة مكرّرة ميتة | تشويش + خطر تعديل الكود الخطأ | حذف/عزل في PR مستقل |
| R-13 | **منخفضة** | coverage 26 test فقط | لا شبكة أمان | regression tests قبل أي تغيير في التحكم |
| R-14 | **منخفضة** | `Npgsql` معلن وغير مستخدم |_packages زائدة | مراجعة تبعيات |

---

## 10. Next Action

**أمر واحد:**

```
/speckit-constitution
```

**لماذا هذا هو الخطوة التالية (وليست `/speckit-specify`):**
1. الـconstitution مكتوب كمسودة `v1.0.0` في `.specify/memory/constitution.md` لكنه **غير مصدَّق** — وقسم Ratification فيه معلَّم *"pending owner approval"*. أمر `/speckit-constitution` هو إجراء spec-kit الرسمي لتثبيت/مراجعة الـconstitution — تشغيله الآن يجعل المراجعة **مؤسسية** (عبر أمر الأداة وقالبها) بدل مراجعة نصّي حر.
2. بعد التصديق فقط، الخطوة التالية تصبح `/speckit-specify` لـSPEC-001 — **ولن يُنفَّذ قبل ذلك**، لأن الـconstitution (خصوصاً المبدأين II وIII) هو ما يمنع بناء feature فوق افتراضات خاطئة عن View Only والموافقة.
3. **متطلب سابق يجب تنفيذه أولاً**: Spec Kit غير مثبّت. قبل تشغيل أي أمر:
```bash
uv tool install specify-cli
specify init . --integration <agent-key>
```
> هذا التثبيت **يتطلب تصريحك الصريح** (المهمة تمنع تثبيت أدوات بدون إذن)، وسيضيف `.specify/` (قوالب + commands) إلى الريبو — وقد يحذف/يستبدل المسار `constitution.md` المكتوب يدوياً، فسيُعاد التحقق منه بعد التثبيت.

**خلال المراجعة، المفصلات الأربعة التي أحتاج قرارك فيها:**
1. هل View Only يُقوّى على السيرفر كـ**decision مستقلة قبل** الالتقاط، أم يبقى كما هو مع توثيقه كـ"[UNKNOWN] حدّ صلاحيات"؟
2. هل ScreenRecorderLib يُعتمد كمحرك وحيد أم يُعتمد خلف abstraction مع محرك بديل مُجدول؟
3. هل capture process يكون داخل `Desktop.Win` أم عملية منفصلة خفيفة في نفس الجلسة (A′)؟
4. هل يُسمح بتثبيت spec-kit (uv/specify) في هذه البيئة؟

---

*تم إنشاء: `.specify/memory/constitution.md` + `REVERSE-ENGINEERING-REPORT.md` فقط. لا تعديل على كود الإنتاج، لا install، لا migration، لا deploy، لا commit، لا push.*

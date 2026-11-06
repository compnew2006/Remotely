# EVIDENCE MAP — خط الأساس المُتحقَّق منه (P00)

**المشروع:** Remotely Capture Extension (fork of `immense/Remotely`)
**الحالة المُقيَّمة:** `master @ ec0d45d8f83ee512e60306ce4ddefed6509bd903` · 974 ملفاً متتبَّعاً · **صفر ملف معدَّل**
**تاريخ الفحص:** 2026-11-06 · **الفاحص:** وكيل (Principal .NET Engineer / Independent Security Reviewer)
**النطاق:** خط أساس قابل للتحقق — لا تنفيذ كود إنتاج.

---

## 0. المنهجية وخلاصة الثقة

كل استنتاج رئيسي في هذه الوثيقة مبني على **قراءة المصدر الفعلية** في هذه الجلسة، لا على الذاكرة ولا على تقرير سابق. وكل استنتاج يحمل:
- **مرجعاً قابلاً للتحقق** (مسار + رقم سطر) — آلة قابلة لإعادة الفحص.
- **تصنيف دليل** من §2.
- **طريقة التحقق** — كيف تأكّدت (قراءة سطر، تنفيذ أمر، فحص شبكة).

**ما لم أفعله:** لم أشغّل التطبيق، لم أختبر على جهاز Windows حقيقي، لم أنشر، لم أعدّل أي ملف إنتاج.

### 0.1 خلاصة صريحة

| البند | النتيجة |
|---|---|
| **اختبارات مُنفَّذة** | **19/19 نجحت (جلسة المرحلة صفر 2026-11-06، T-07)** بشرط `DOTNET_ROLL_FORWARD=Major` على runtime 10.0.14 — لا على .NET 8 أصلي؛ سابقًا: صفر |
| **بناء `Server`** | ✅ **نجح** بعد إزالة إدخالات `libman.json` (كان LIB002 + تلويث شجرة LF) |
| **بناء الحل كاملًا** | ✅ **نجح** (`dotnet build Remotely.sln`) |
| **بوابات اجتازت** | **0 من 7** — لم تُنفَّذ فحوص بوابات في هذه الجلسة |
| **كود إنتاج معدَّل** | ملفان إنشائيان (build-only): `Server/libman.json` · `.github/workflows/run_tests.yml` — بتفويض المالك للمرحلة صفر (ADR-P00-09) |
| **استنتاجات مسنَدة** | **47** (39 أصلي + E-36…E-43) — كل واحد بمرجع `مسار:سطر` أو سجل تنفيذ قابل لإعادة الفحص |

> ⚠️ **الخلاصة التي يجب أن تسبق كل شيء آخر (محدَّثة في المرحلة صفر):** خط الأساس الاختباري **أصبح قابلًا للتنفيذ جزئيًا** — 19/19 تعمل بـroll-forward (T-07) — لكنه **غير مُستوفى كاملًا**: لا runtime 8.0 أصلي مثبَّت، وشرط Execution-Environment-Uniformity الدستوري لم يُستوفَ بعد، وCI لم يُسجَّل أخضر. أي ادعاء `[TEST-EVIDENCE]` يجب أن يذكر شرط الـroll-forward صراحةً. (كانت الصيغة الأصلية: "لا خط أساس إطلاقًا" — انظر سجل التغيير §11.)

---

## 1. خريطة الأدلة — الاستنتاجات الرئيسية

### 1.1 مسار View Only — لماذا ليس حدّ صلاحيات

| # | الاستنتاج | التصنيف | الدليل (المسار:السطر) | طريقة التحقق |
|---|---|---|---|---|
| E-01 | الفلاج **يُكتب** في جلسة تحكّم عن بُعد واحدة ولا يُقرأ في أي مسار يقيّد | `[SOURCE-VERIFIED]` | `Server/Hubs/CircuitConnection.cs:266` — `ViewOnly = viewOnly,` | قراءة مباشرة |
| E-02 | مسار relay يمرّر أي DTO بلا فحص | `[SOURCE-VERIFIED]` | `Server/Hubs/ViewerHub.cs:187-197` — `SendDtoToClient(byte[] dtoWrapper)` → `return _desktopHub.Clients.Client(...).SendDtoToClient(dtoWrapper, Context.ConnectionId);` بلا أي شرط على `ViewOnly` | قراءة مباشرة |
| E-02b | **الفلاج لا يعبر إلى الوكيل إطلاقاً**: صفر ورود لـ`ViewOnly` في `Shared/` و`Agent/` و`Desktop.Shared/` و`Desktop.Win/` | `[SOURCE-VERIFIED]` | `Shared/Interfaces/IDesktopHubClient.cs:24` — `SendDtoToClient(byte[] dtoWrapper, string viewerConnectionId)` بلا أي باراميتر وضع وصول · `Desktop.Shared/Services/DesktopHubConnection.cs:323-329` يستقبل الحمولة بلا فحص | بحث شامل عبر `*.{cs,ts,cshtml,razor}` |
| E-02c | **الحراسة كلها في المتصفح**: 19 إشارة لـ`ViewerApp.ViewOnlyMode` في `InputEventHandlers.ts` (منها 16 حراسة `if`)، و`App.ts:23-25` | `[SOURCE-VERIFIED]` | `Server/wwwroot/src/InputEventHandlers.ts` · `Server/wwwroot/src/App.ts:23-25` · `Server/Pages/Viewer.cshtml:163` | بحث شامل |
| E-02d | **الرابط الذي يولّده السيرفر للوضع غير المحضور يكتب `viewonly=False` حرفياً** | `[SOURCE-VERIFIED]` | `Server/Models/RemoteControlSession.cs:40` — `public string RelativeAccessUri => $"/Viewer?mode=Unattended&sessionId={UnattendedSessionId}&accessKey={AccessKey}&viewonly=False";` | قراءة مباشرة |
| E-02e | `RelativeAccessUri` **لا مستهلك اليوم** في الريبو ⇒ العيب **كامن** لا مُفعَّل | `[SOURCE-VERIFIED]` | بحث `RelativeAccessUri` عبر كل `*.cs`: مطابقة واحدة فقط — التعريف نفسه. الرابط الحيّ هو `Server/Components/Devices/DeviceCard.razor.cs:336` — `$"&viewonly={viewOnly}"` وهو يمرّر القيمة الفعلية | بحث شامل |
| E-03 | **المتصفح نفسه** يبدّل الفلاج | `[SOURCE-VERIFIED]` | `Server/wwwroot/src/InputEventHandlers.ts:125-128` — `ViewerApp.ViewOnlyMode = ViewOnlyButton.classList.contains("toggled");` | قراءة مباشرة |
| E-04 | القيمة الابتدائية تأتي من **query string** | `[SOURCE-VERIFIED]` | `Server/wwwroot/src/App.ts:23-25` — `ViewOnlyMode: queryString["viewonly"] ? ... : false` | قراءة مباشرة |
| E-05 | تغيير جلسة Windows **يمسح** الفلاج | `[SOURCE-VERIFIED]` | `Server/Hubs/ViewerHub.cs:91` — `SessionInfo = SessionInfo.CreateNew();` ← `Server/Models/RemoteControlSession.cs:92` — `clone.ViewOnly = false;` | قراءة مباشرة |
| E-06 | بوابة الجهاز **معطّلة** | `[SOURCE-VERIFIED]` | `Desktop.Shared/Services/Viewer.cs:95` — `public bool HasControl { get; set; } = true;` · الاستهلاك `Desktop.Shared/Services/DtoMessageHandler.cs:63` — `if (!viewer.HasControl) { return; }` | قراءة + بحث شامل لكل مراجع `HasControl` في الريبو |
| E-07 | جلسات API **قابلة للتحكم** افتراضياً | `[SOURCE-VERIFIED]` | `Server/API/RemoteControlController.cs:138-145` — إنشاء الجلسة بلا أي تعيين لـ`ViewOnly` ⇒ القيمة الافتراضية `false` | قراءة مباشرة |
| E-08 | **تعديل محلي**: `Desktop.UI/Views/MainView.axaml:42` يربط `IsChecked="{Binding HasControl}"` على `CheckBox` (ربط ثنائي الاتجاه في Avalonia) ⇒ للعميل المحليSetter على نفس الخاصية | `[SOURCE-VERIFIED]` | `Desktop.UI/Views/MainView.axaml:42` | قراءة + معرفة Avalonia أن `ToggleButton.IsChecked` ثنائي الاتجاه افتراضياً |

> **تصحيح على أدبيات سابقة:** دستور v2.0.0 يقول إن `HasControl` "لا يُسنَد في أي مكان في الريبو". العبارة **دقيقة لأغراضها** (لا مسار سيرفر→جهاز يسيّره) لكنها **غير دقيقة حرفياً**: `Desktop.UI/Views/MainView.axaml:42` مسار كتابة محلي. الأثر العملي هو **أقوى** لا أضعف: قراري، لا السيرفر. ⇒ يُسجَّل في §5 كتصحيح.

### 1.2 ما هو **مُنفَّذ فعلاً** (الضوابط التي يجب الحفاظ عليها)

| # | الاستنتاج | التصنيف | الدليل |
|---|---|---|---|
| E-09 | مطابقة `accessKey` مفروضة على الجلسات غير المحضورة | `[SOURCE-VERIFIED]` | `Server/Hubs/ViewerHub.cs:210-221` — `accessKey != session.AccessKey` ⇒ `Result.Fail("Authorization failed.")` |
| E-10 | موافقة الحضور مُوصَّلة | `[SOURCE-VERIFIED]` | `Server/Hubs/CircuitConnection.cs:268` — `RequireConsent = settings.EnforceAttendedAccess` |
| E-11 | مُرشِّح تفويض API مُطبَّق على المسارات الحسّاسة | `[SOURCE-VERIFIED]` | `Server/API/DevicesController.cs:29,46` · `Server/API/RemoteControlController.cs:48` · `Server/API/ScriptingController.cs:42` · `Server/API/ClientDownloadsController.cs:106` |

### 1.3 هوية التنفيذ

| # | الاستنتاج | التصنيف | الدليل |
|---|---|---|---|
| E-12 | الوكيل يعمل كخدمة Windows ⇒ **Session 0** | `[SOURCE-VERIFIED]` | `Agent/Program.cs:29` — `.UseWindowsService()` (و`:30` `.UseSystemd()`) |
| E-13 | مسار الإطلاق **يكرّر توكن winlogon** | `[SOURCE-VERIFIED]` | `Desktop.Native/Windows/Win32Interop.cs:121` (PID winlogon في الجلسة) · `:134` `OpenProcessToken(..., TOKEN_DUPLICATE)` · `:145` `DuplicateTokenEx(..., TokenPrimary, out hUserTokenDup)` |
| E-14 | winlogon يعمل كـSYSTEM ⇒ **المتوقَّع** أن العملية التفاعلية ترث SYSTEM — ✅ **تأكد تنفيذيًا في المرحلة 4** (E-43/T-09: التوكن الفعلي للابن `S-1-5-18` بتكامل System على `winsta0\Default`) | `[SOURCE-VERIFIED]` (قدرة مصدر) + **مُؤيَّد `[RUNTIME-VERIFIED]` بـE-43** | استنتاج من E-13 + `[SOURCE-VERIFIED]` لـ"`winlogon.exe` runs as SYSTEM" — كان بلا تنفيذ حتى المرحلة 4 (E-43) |
| E-15 | سطح المكتب عند logon/UAC هو **الآمن** | `[SOURCE-VERIFIED]` | `Desktop.Native/Windows/Win32Interop.cs:158` — `si.lpDesktop = @"winsta0\" + ResolveDesktopName(dwSessionId)` |

> ~~**لم يُنفَّذ** (بلا سجل): التوكن الفعلي · SID المستخدم · معرّف الجلسة · حالة الارتفاع · إمكانية الوصول لسطح المكتب · آلية الإطلاق. ⇒ G7 = **UNKNOWN**، لا "FAIL".~~ **حُدِّث في المرحلة 4 (2026-11-06): الوقائع الست نُفِّذت وسُجِّلت في E-43/T-09 على Windows Server 2025 حقيقي ⇒ G7 انتقلت UNKNOWN → FAIL بقرار المالك (AD-14) — لا ترقية ذاتية؛ الفحص صار موجودًا وأثبت فشل شرطي الهوية (1) و(2) من شروط G7 الخمسة.**

### 1.4 أسطح الهجوم المسجَّلة

| # | الاستنتاج | التصنيف | الدليل |
|---|---|---|---|
| E-16 | إنشاء جهاز **بلا مصادقة** | `[SOURCE-VERIFIED]` | `Server/API/DevicesController.cs:124-125` — `[HttpPost] public async Task<IActionResult> Create(...)` بلا `ApiAuthorizationFilter` ( بينما `Get` في `:29` و`:46` محميّان) |
| E-17 | تنزيل المثبّت **بلا مصادقة** | `[SOURCE-VERIFIED]` | `Server/API/ClientDownloadsController.cs:117-121` — التحميلoverload التنزيل `:106-115` محميّ بـ`[ServiceFilter(typeof(ApiAuthorizationFilter))]`، لكن `:117` ليس كذلك |
| E-18 | تنزيل ثنائيات مخصّصة **بلا مصادقة** — **جديد، غير مسجَّل في الدستور** | `[SOURCE-VERIFIED]` | `Server/API/CustomBinariesController.cs:8-16` — `[Route("api/custom-binaries")] [ApiController]` بلا مصادقة على `GetWinX86Desktop` / `GetWinX64Desktop` |
| E-19 | مسار الملفات عن بُعد **بلا canonicalisation** | `[SOURCE-VERIFIED]` | `Desktop.Win/Services/FileTransferServiceWin.cs:88` — `var filePath = Path.Combine(baseDir, fileName);` حيث `fileName` من الطرف البعيد |
| E-20 | صلاحية `BuiltinUsersSid` واسع | `[SOURCE-VERIFIED]` | `Desktop.Win/Services/FileTransferServiceWin.cs:175` — `new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null)` |
| E-21 |Named pipe **بلا مصادقة** | `[SOURCE-VERIFIED]` | `Desktop.Shared/Services/ChatHostService.cs:30` — `new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)` — بلا ACL ولا تحقّق من العميل |
| E-22 | تحديث العميل **بلا توقيع ولا هاش** | `[SOURCE-VERIFIED]` | `Agent/Services/Windows/UpdaterWin.cs:136-149` — تنزيل `Install-Remotely.ps1` + zip ثم `Process.Start("powershell.exe", "-ExecutionPolicy Bypass -File ...")` |

### 1.5 سلسلة التوريد وسلسلة البناء

| # | الاستنتاج | التصنيف | الدليل |
|---|---|---|---|
| E-23 | **ثغرات عالية الخطورة** في الحزم المستعملة | `[SOURCE-VERIFIED]` (تنبيه `NU1903` أثناء الاستعادة) | `NU1903` (high): `MessagePack 2.5.187` · `Microsoft.AspNetCore.SignalR.Protocols.MessagePack 8.0.10`. `NU1902` (moderate): `MailKit 4.8.0` · نفس حزم SignalR ⇒ **عُولجت في المرحلة 3** (راجع E-42/GAP-13) |
| E-24 | **بناء `Server` مكسور**: LibMan يفشل في استرجاع مكتبتين | `[SOURCE-VERIFIED]` | `Microsoft.Web.LibraryManager.Build` **2.1.175** في `Server/Server.csproj` · `error LIB002` لـ`@msgpack/msgpack@3.0.0-beta2` و`@microsoft/signalr-protocol-msgpack@8.0.7` (مزوّد `unpkg`) |
| E-25 | **الأصول موجودة** على unpkg ⇒ العطل في عميل LibMan لا في المزوّد | `[RUNTIME-VERIFIED]` | كل روابط الملفات الثمانية + `?meta` تُرجع HTTP 200 من هذا الجهاز؛ و`cdnjs` ones نجحت. ⇒ لا انقطاع شبكة ولا حزمة مفقودة |
| E-26 | **لا يوجد .NET 8 runtime** على جهاز التقييم | `[RUNTIME-VERIFIED]` | `dotnet --list-runtimes` ⇒ 6.0.11 / 9.0.17 / 10.0.14 فقط · `dotnet --list-sdks` ⇒ 9.0.315 فقط |
| E-27 | **CI لا يمكنه أن ينجح**: يثبّت SDK 7 والمشاريع تستهدف net8.0 | `[SOURCE-VERIFIED]` | `.github/workflows/run_tests.yml` — `dotnet-version: 7` مع `dotnet test` على مستوى الحل · كل المشاريع `net8.0` ⇒ `NETSDK1045` متوقّع |
| E-28 | متغيّرات CI **ميتة** | `[SOURCE-VERIFIED]` | `.github/workflows/run_tests.yml` — `Test_Project_Path: Tests\Tests.csproj` (ملف غير موجود) |
| E-29 | ~~مسار مكتبة في الواجهة **مكسور**~~ — **FALSE POSITIVE (صُحِّح 2026-11-06)**: `Viewer.cshtml:33,38` يطلب `/lib/microsoft/signalr-protocol-msgpack/dist/browser/...` و`libman.json` ينزّل إلى `wwwroot/lib/microsoft/signalr-protocol-msgpack/` — **المسار مطابق**، والملفات موجودة على القرص وملتزَمة | `[SOURCE-VERIFIED]` (إعادة فحص) | `Server/Pages/Viewer.cshtml:33,38` · `Server/libman.json` · وجود `wwwroot/lib/microsoft/signalr-protocol-msgpack/dist/browser/` ✅ |
| E-30 | `Desktop.Core` **شيفرة ميتة** (مكرّرة لـ`Desktop.Shared`) | `[SOURCE-VERIFIED]` | `Desktop.Core` غير مُدرج في `Remotely.sln` ولا يشير إليه أي مشروع · المسار الحيّ هو `Desktop.Shared` (تستهلكه `Desktop.Win`/`Desktop.Linux`/`Desktop.UI`) |
| E-31 | `Tests/Desktop.Win.Tests` **خارج الحل** وكل اختباراته `[Ignore]` | `[SOURCE-VERIFIED]` | `Tests/Desktop.Win.Tests/ManualTests.cs` — 7 × `[TestMethod]` تحت `[Ignore("Manual test.")]` · المشروع غير مُدرج في `Remotely.sln` |

### 1.6 بنية المشروع · ما هو صحيح وما كان مُبالَغاً فيه

| # | الاستنتاج | التصنيف | الدليل |
|---|---|---|---|
| E-32 | **3 مزوّدات ترحيلات** | `[SOURCE-VERIFIED]` | `Server\Migrations/` ← `PostgreSql` (41 ملف) · `Sqlite` (53) · `SqlServer` (41) |
| E-33 | **66 صنف ترحيل** فعلياً | `[SOURCE-VERIFIED]` | 66 ملفاً من أصل 135 تحت `Server\Migrations` يعرّف `class … : Migration` ⇒ **ادعاء الدستور صحيح** |
| E-34 | **26 طريقة اختبار** إجمالاً، لكن **19 فقط** داخل الحل | `[SOURCE-VERIFIED]` | `Server.Tests` (2+6+8+2 = 18) + `Shared.Tests` (1) = **19** · `Desktop.Win.Tests` (7) خارج الحل و`[Ignore]` ⇒ 26 |
| E-35 | `AGENTS.md` يوصف "26 test methods total" ⇒ **عدد المصدر صحيح، لكنه مضلِّل** لأنه يُقرأ كـ"26 قابلة للتنفيذ" | `[SOURCE-VERIFIED]` | E-34 |

### 1.7 استنتاجات إضافية (جلسة المرحلة صفر 2026-11-06)

| # | الاستنتاج | التصنيف | الدليل | طريقة التحقق |
|---|---|---|---|---|
| E-36 | قالب Docker يضبط `Remotely_ApplicationOptions__DockerGateway` بينما الكود يقرأ `DockerGatewayIp` ⇒ **لا يربط إطلاقًا** في النشر الحاوي | `[SOURCE-VERIFIED]` | `docker-compose/docker-compose.yml:38` مقابل `Server/Options/ApplicationOptions.cs` (`DockerGatewayIp`) و`Server/appsettings.json:25` | قراءة الملفين؛ سلوك runtime غير مُنفَّذ |
| E-37 | Swagger مكشوف في الإنتاج بلا شرط بيئي | `[SOURCE-VERIFIED]` | `Server/Program.cs:293-294` (`UseSwagger/UseSwaggerUI` خارج `IsDevelopment`) | قراءة مباشرة |
| E-38 | SQLite يسجّل KnownProxy `0.0.0.0/0` افتراضيًا + ترحيل تلقائي عند كل إقلاع | `[SOURCE-VERIFIED]` | `Server/Data/SqliteDbContext.cs:27` · `Server/Program.cs:88` (`MigrateAsync`) | ظهر في تحذير build + قراءة مباشرة |
| E-39 | تحذيرات MailKit 4.8.0 وSignalR.Protocols.MessagePack 8.0.10 (NU1902/NU1903) — **أُعيد التحقق منها هذه الجلسة** | `[RUNTIME-VERIFIED]` | مخرج `dotnet build Server` الحرفي: GHSA-9j88-vvj5-vhgr (MailKit) · GHSA-86xp-94pg-8r6r + GHSA-f8h2-vmm9-qhj6 (SignalR msgpack) | تنفيذ build في هذه الجلسة ⇒ **أُغلقت بالترقية في المرحلة 3** |
| E-42 | ثغرات سلسلة التوريد عبر `dotnet list --vulnerable --include-transitive`: **كل High/Moderate أُغلقت**؛ بقي **Low ×2** (`NuGet.Packaging`/`Protocol` 6.11.0، GHSA-g4vj) عابِرة من أداة scaffolding تصميمية (`CodeGeneration.Design`) — ليست في سطح تشغيل السيرفر الحي | `[RUNTIME-VERIFIED]` | قبل/بعد (هذه الجلسة): SignalR `8.0.10→8.0.33` · MailKit `4.8.0→4.18.2` · MessagePack `2.5.187→2.5.307` · pins للـAgent: `System.IO.Packaging 8.0.1` / `Cryptography.Pkcs 8.0.2` / `Cryptography.Xml 8.0.5` (سلسلة GHSA-846f/37gx/cvvh/r955) · `CodeGeneration.Design 8.0.6→8.0.23` (أزال `Microsoft.Build` High) · `Server.Tests` EF `8.0.10→8.0.33` (تفادي NU1605) |
| E-40 | LibMan restore يعيد كتابة الأصول الملتزَمة بنهايات LF عند كل بناء على Windows ⇒ **شجرة git متسخة بعد كل بناء** (~37 ملفًا، محتوى متطابقًا — فروق أسطر فقط) | `[RUNTIME-VERIFIED]` | بعد بناء ناجح: `git status` أظهر 37 ملف M تحت `wwwroot/lib/fontawesome` + `microsoft-signalr`؛ `git diff --ignore-cr-at-eol --name-only` = **صفر** ⇒ أسطر فقط. أُصلح بإزالة **كل** إدخالات libman (وليس unpkg وحدها) | بناءان متكرران في نفس الجلسة + مقارنة diffs |
| E-41 | فلتر التفويض ي**فشل مفتوحًا** (fail-open) — لو رمى `Authorize()` استثناءً (عطل DB في `ValidateApiKey`/`GetUserByName`) يُبتلع في `catch` ولا يُضبط `context.Result` ⇒ MVC ينفّذ الـ action **بلا مصادقة**، فيقوّض كل نقطة تُعتَبر محميّة | `[SOURCE-VERIFIED]` | `Server/Auth/ApiAuthorizationFilter.cs:24-31` و`Server/Auth/ExpiringTokenFilter.cs:28-35` (قبل الإصلاح) — أُصلح بإسناد `context.Result = new UnauthorizedResult()` داخل `catch` (fail-closed) في المرحلة 2 |
| E-43 | **PoC هوية G7 على Windows حقيقي (المرحلة 4)**: إعادة إنتاج سلسلة `Win32Interop.CreateInteractiveSystemProcess:105-193` حرفيًا (مهمّة مجدولة SYSTEM في الجلسة 0 — نفس موضع وكيل الإنتاج — تكرير توكن winlogon PID 2100 ثم `CreateProcessAsUser` بتوكن TokenPrimary على `winsta0\Default`)؛ الابن (PID 117920) سجّل الوقائع الست: **(1)** توكن `NT AUTHORITY\SYSTEM`، TokenPrimary، SeTcb/SeDebug/SeImpersonate مفعّلة **(2)** SID `S-1-5-18` (مجموعات: Administrators/Everyone/Authenticated Users — **بلا logon SID**) **(3)** جلسة 1 = console، ومستخدم الجلسة `Administrator` ⇒ **فجوة الهوية مُثبتة تنفيذيًا** **(4)** تكامل System ‏`S-1-16-16384` + `isElevated=true` + ElevationType=Default **(5)** `WinSta0`/`Default` وطاقة الإدخال `Default` (لا LogonUI/consent وقت الفحص) **(6)** سلسلة الإطلاق مُسجّلة كاملة. ⇒ **تُرفع E-14 من "متوقَّع من المصدر" إلى مُؤيَّد تنفيذيًا** | `[RUNTIME-VERIFIED]` | الأداة `poc/G7Identity/` (خارج الحل، قياس فقط) · التقرير `poc/results/g7-report.json` بما فيه `whoami /all` الابن · تنفيذ 2026-11-06 10:04 UTC · **تحفّظات تمثيلية**: جهاز Server 2025 بمسجّل عليه built-in Administrator (لا توكن موظف مُرشَّح بـUAC)؛ الآمن (Winlogon desktop) غير مُختبَر (لا LogonUI/consent حاضر)؛ انحرافان قياسيان عن الإنتاج: `lpApplicationName`/`lpCurrentDirectory` مُمرَّرتان بإطلاق مطلق (الإنتاج null — أنتاجا 1314 ERROR_BAD_PATHNAME في بيئة القياس) |

---

## 2. تصنيف الأدلة — المعتمد هنا

الدستور المُصادَق (v2.0.0 §Evidence taxonomy) هو المرجع. هذه الوثيقة تلتزم به، **وتوفّر جسراً صريحاً** إلى المصطلحات المستخدمة في تكليف P00:

| التسمية في تكليف P00 | التسمية في الدستور v2.0.0 | متى تُستخدم |
|---|---|---|
| `SOURCE-CAPABILITY` | `[SOURCE-VERIFIED]` | استنتاج عن **قدرة** الكود على فعل شيء (ما الذي في المصدر يسمح به) — لا عن سلوك مُنفَّذ. هذه هي تسمية E-14 |
| `RUNTIME-VERIFIED` | `[RUNTIME-VERIFIED]` | سلوك مُنفَّذ على جهاز/بيئة ممثّلة وسجل مُلتقط |
| `PROPOSED` | `[PROPOSED]` | سلوك أو تصميم غير موجود في الكود بعد |
| `UNKNOWN` | `[UNKNOWN]` | غير قابل للتحديد من المادة المتاحة |
| — | `[TEST-EVIDENCE]` | اختبار آلي كُتب ونُفِّذ ونجح (لا يثبت جاهزية إنتاج) |
| — | `[RELEASE-GATE]` | اجتياز بوابة إصدارية موثَّق |

> **الفجوةGAP-16 (P1):** الدستور لا يحوي طبقة اسمها `SOURCE-CAPABILITY`، رغم أن التكليف يستخدمها مرتين كتصنيف أساسي. التفويض الحالي هو: **السلوك المتوقَّع** (E-14) = `[SOURCE-VERIFIED]` + نص صريح تمنع استنتاج سجل تنفيذ. **إضافة الطبقة الخامسة** هي تعديل دستور (v2.1.0) مقترح في §6 — **لم يُطبَّق** (المادة تمنع اعتماد الوكيل لتعديله بنفسه).

**قاعدة الاستخدام:** كل استنتاج في هذه الوثيقة يحمل **تسمية واحدة** فقط. لا يُرفع تصنيف بدون دليل جديد.

---

## 3. الاختبارات: ما نُفِّذ وما لم يُنفَّذ

### 3.1 ما نُفِّذ فعلياً

| # | الأمر | النتيجة | التصنيف |
|---|---|---|---|
| T-01 | `dotnet test` (جذر الحل) | **فشل · exit 1** | `[RUNTIME-VERIFIED]` |
| T-02 | `dotnet --list-sdks` / `--list-runtimes` | SDK 9.0.315 فقط · runtimes 6.0.11 / 9.0.17 / 10.0.14 · **لا 8.0** | `[RUNTIME-VERIFIED]` |
| T-03 | `dotnet --list-sdks` مقابل `net8.0` | البناء بـSDK 9 **نجح** لـ`Shared`/`Shared.Tests` ⇒ `NETSDK1045` محلياً غير حاضر | `[RUNTIME-VERIFIED]` |
| T-04 | فحص HTTP على unpkg (8 روابط ملفات + `?meta` لكل حزمة) | كلها HTTP 200 ⇒ LibMan وحده هو العطل | `[RUNTIME-VERIFIED]` |
| T-05 | **(جلسة المرحلة صفر)** إزالة إدخالات unpkg من `libman.json` ثم `dotnet build Server/Server.csproj` | **نجح — 0 Error** (كان LIB002×2 قبلها مباشرة في نفس الجلسة) | `[RUNTIME-VERIFIED]` |
| T-05b | **(المرحلة صفر)** إزالة **كل** إدخالات libman (E-40: cdnjs كان يلوث الشجرة) ثم بناء متكرر: `Build succeeded · 0 Error` + `git status` نظيفة على `wwwroot` | `[RUNTIME-VERIFIED]` |
| T-06 | **(جلسة المرحلة صفر)** `dotnet build Remotely.sln` كاملًا | **نجح — Build succeeded** | `[RUNTIME-VERIFIED]` |
| T-07 | **(جلسة المرحلة صفر)** `DOTNET_ROLL_FORWARD=Major` + `dotnet test` المشروعين | **19/19 نجحت** (Shared.Tests 1/1 · Server.Tests 18/18 · exit 0) — على runtime 10.0.14 بالـroll-forward، لا على .NET 8 أصلي | `[TEST-EVIDENCE]` (بشرط roll-forward الموثق هنا) |
| T-08 | **(المرحلة 3)** بعد ترقيات الحزم: `dotnet build Remotely.sln` + `DOTNET_ROLL_FORWARD=Major dotnet test` + `dotnet list --vulnerable` على المشاريع الأربعة | **build 0 Error · 19/19 · صفر High/Moderate** (Agent/Shared/Desktop.Shared نظيفة تمامًا؛ Server به Low ×2 NuGet تصميمي) | `[RUNTIME-VERIFIED]` + `[TEST-EVIDENCE]` (بشرط roll-forward) |
| T-09 | **(المرحلة 4)** PoC هوية G7: بناء `poc/G7Identity` (0 أخطاء) وتشغيله كمهمّة مجدولة SYSTEM ⇒ تكرير توكن winlogon + `CreateProcessAsUserOk=true` وتسجيل الوقائع الست في `poc/results/g7-report.json` (تكررت ثلاث مرات حتى مخرجات نظيفة؛ حُذفت المهمة بعدها) | **نُفِّذ — النتائج في E-43** | `[RUNTIME-VERIFIED]` |

### 3.2 سلسلة الفشل كما سُجِّلت حرفياً

```
Shared -> ...\Shared\bin\Debug\net8.0\Shared.dll
Shared.Tests -> ...\Tests\Shared.Tests\bin\Debug\net8.0\Remotely.Shared.Tests.dll
Test run for ...\Remotely.Shared.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.1 (x64)
dotnet : Testhost process for source(s) '...\Remotely.Shared.Tests.dll' exited with error:
         You must install or update .NET to run this application.
  App: ...\testhost.exe
  Architecture: x64
  Framework: 'Microsoft.NETCore.App', version '8.0.0' (x64)
The following frameworks were found:
  6.0.11 / 9.0.17 / 10.0.14
```

```
libman.json : error LIB002: The "@msgpack/msgpack@3.0.0-beta2" library could not be
              resolved by the "unpkg" provider [Server\Server.csproj]
libman.json : error LIB002: The "@microsoft/signalr-protocol-msgpack@8.0.7" library
              could not be resolved by the "unpkg" provider [Server\Server.csproj]
  One or more libraries failed to restore
```

### 3.3 ما **لم** يُنفَّذ — ولماذا

| # | ما لم يُنفَّذ | السبب | الأثر |
|---|---|---|---|
| N-01 | تشغيل أي حالة اختبار | لا .NET 8 runtime | **0/19** اختباراً مُنفَّذاً |
| N-02 | بناء `Server` ثم `Server.Tests` | `LIB002` في LibMan | 18 اختباراً من `Server.Tests` لم تُبنَ ولم تُنفَّذ |
| N-03 | تشغيل `Desktop.Win.Tests` | خارج الحل + كله `[Ignore]` | 7 اختبارات لا تعمل بالتصميم |
| N-04 | أي اختبار على Windows فعلي (سلوك، الصلاحيات، سطح مكتب) | ~~لا جهاز Windows ممثّل في هذه البيئة~~ **بعضه نُفِّذ (المرحلة 4)**: PoC هوية G7 نُفِّذ على Windows Server 2025 حقيقي (E-43/T-09) — يبقى فحص موافقة G6 في جلسة حيّة وفحوص الصلاحيات السلوكية الأخرى | E-14 رُفد تنفيذيًا بـE-43؛ G6 تبقى بلا دليل تشغيلي |
| N-05 | `dotnet build` بمخرجات نظيفة | نفس عطل LibMan | — |

**الأثر الحاكم:** تحت Principle XIII، **لا يمكن لأي ادعاء في هذه الوثيقة أن يحمل `[TEST-EVIDENCE]`**. كل ما سبق `[SOURCE-VERIFIED]` أو `[RUNTIME-VERIFIED]` (لفحص الأدوات نفسه). لا `[RELEASE-GATE]`.

---

## 4. مصفوفة الفجوات (P0 / P1 / P2)

**التعاريف:** `P0` = يمنع الإنتاج ويمنع كل عمل على نظام الالتقاط · `P1` = ثغرة أو عطل يجب إصلاحه قبل الاعتماد على المكوّن المتأثر · `P2` = دقّة/صيانة/تدهور خط الأساس.

| ID | P | الفجوة | الدليل | البوابة | الإجراء المقترح | الحالة |
|---|---|---|---|---|---|---|
| GAP-01 | **P0** | **View Only ليس حدّ صلاحيات على السيرفر** — لا فحص في مسار relay | E-01 · E-02 | G1 | SPEC-001 / FR-010…FR-012 | مفتوحة — في نطاق SPEC-001 |
| GAP-02 | **P0** | **المتصفح يبدّل الفلاج بنفسه** | E-03 · E-04 | G1 | SPEC-001 / FR-006 · FR-007 | مفتوحة |
| GAP-03 | **P0** | **دورة حياة الجلسة تمسح الفلاج** (`CreateNew` → `ViewOnly = false`) | E-05 | G1 | SPEC-001 / FR-013 · FR-016 | مفتوحة |
| GAP-04 | **P0** | **بوابة الجهاز معطّلة** (`HasControl = true` دائماً) | E-06 · E-08 | G1 · G4 | SPEC-001 / FR-017…FR-020 | مفتوحة |
| GAP-05 | **P0** | **جلسات API قابلة للتحكم افتراضياً** | E-07 | G1 · G4 | SPEC-001 / FR-021…FR-024 | مفتوحة |
| GAP-06 | **P0** | **لا خط أساس اختباري قابل للتنفيذ** — 0/19 اختباراً | T-01 · N-01 · N-02 | Principle X · SC-007 | تثبيت .NET 8 runtime + إصلاح بناء `Server` (§6 ADR-03/04) | 🔶 **مغلقة جزئيًا (2026-11-06 المرحلة صفر)**: البناء يُصلح (T-05/T-06) و19/19 تُنفَّذ بـroll-forward (T-07) — يبقى شرط Execution-Environment-Uniformity غير مستوفى (لا runtime 8.0 أصلي؛ إعادة تثبيت R-08 لم تُعتمد بعد) |
| GAP-07 | **P1** | إنشاء جهاز بلا مصادقة | E-16 | G4 · شرط إضافي | إغلاق أو قبول خطي من المالك | ✅ **مغلقة (2026-11-06 المرحلة 2)** — إضافة `ApiAuthorizationFilter` إلى `Create` (`DevicesController.cs:125`). لا مستهلك في المستودع (التسجيل عبر SignalR `AddOrUpdateDevice`) |
| GAP-08 | **P1** | تنزيل مثبّت بلا مصادقة | E-17 | شرط إضافي | إغلاق المسار `:117` | ✅ **مغلقة (2026-11-06 المرحلة 2)** — إضافة الفلتر إلى `:117` و`GetDesktop :35/:71`. **تنزيل العام للمثبّت** (`/Content/*.zip` + `AgentUpdate/DownloadPackage`) قُبل **خطيًا** مع التخفيف — راجع DECISION-LOG / PC-1 |
| GAP-09 | **P1** | تنزيل ثنائيات مخصّصة بلا مصادقة | E-18 | شرط إضافي | إضافة `ApiAuthorizationFilter` | ✅ **مغلقة (2026-11-06 المرحلة 2)** — إضافة الفلتر إلى `CustomBinariesController` (`:16` و`:27`)؛ المستهلك `Deploy.razor:200-201` عبر كوكي الأدمن فلا يكسر التنزيل |
| GAP-10 | **P1** | تحديث العميل بلا توقيع/هاش | E-22 | G2 | توقيع + هاش مثبَّت | مفتوحة **تنفيذياً** — ~~بوابة قرار OD-9~~ ✅ **حُسِمت بقرار المالك `AD-13`** (DECISION-LOG): نموذج هجين (Authenticode على المحمّلات + SHA-256 موقّع لكل حزمة يتحقق منه العميل قبل فكّ الضغط) · مفاتيح KMS/HSM خارج المستودع · إبطال بـKey-ID + رفض الأقدم. **لم تُنفَّذ أي شيفرة توقيع** — AD-13 قرار نموذج لا تفويض تنفيذ؛ الباقي مواصفة (مرشَّحة SPEC-003) + تصريح + دليل بوابة |
| GAP-11 | **P1** | اجتياز مسار في نقل الملفات + صلاحية `BuiltinUsersSid` واسع | E-19 · E-20 | G3 | canonicalisation + أقل امتياز | مفتوحة |
| GAP-12 | **P1** | Named pipe بلا مصادقة | E-21 | G5 | ACL + تحقّق من العميل | مفتوحة |
| GAP-13 | **P1** | ثغرات `NU1903` عالية الخطورة في الحزم | E-23 · **E-39** · **E-42 (المرحلة 3)** | Principle VIII | ترقية الحزم | ✅ **مغلقة (2026-11-06 المرحلة 3)** — صفر High/Moderate عبر كل المشاريع (T-08)؛ متبقٍّ Low ×2 (`NuGet.*` أداة تصميمية) مقبولة كبقايا — راجع E-42 |
| GAP-14 | **P1** | **بناء `Server` مكسور** ⇒ `Server.Tests` لا يُبنى | E-24 · E-25 · **E-40** | Principle X | ADR-04 (§6) | ✅ **مغلقة (2026-11-06 المرحلة صفر)** — إزالة كل إدخالات `libman.json` (الأصول ملتزَمة أصلًا؛ cdnjs كان يلوث الشجرة LF) · T-05/T-05b/T-06/T-07 |
| GAP-15 | **P1** | ~~مسار مكتبة الواجهة مكسور~~ | E-29 | Principle X | — | ❌ **ملغاة — false positive** (E-29 صُحح؛ المسار مطابق للموجود على القرص) |
| GAP-16 | **P1** | تصنيف الأدلة يخلو من طبقة `SOURCE-CAPABILITY` | §2 | Principle XIII | تعديل دستور v2.1.0 (§6) | مفتوحة — **تحتاج تصريح المالك** |
| GAP-22 | **P1** | **الرابط الذي يولّده السيرفر يكتب `viewonly=False` حرفياً** — يسلّم رابطاً قابلاً للتحكم مهما كان وضع الجلسة | E-02d · E-02e | G1 | يُصلح مع SPEC-001 (FR-003) | مفتوحة — **كامنة** (لا مستهلك اليوم) |
| GAP-17 | **P2** | `Desktop.Core` شيفرة موتة مكرّرة لـ`Desktop.Shared` | E-30 | Principle I | حذف أو توثيق صراحة | مفتوحة |
| GAP-18 | **P2** | `Desktop.Win.Tests` خارج الحل و`[Ignore]` | E-31 | Principle X | إدراج في الحل أو حذف | مفتوحة |
| GAP-19 | **P2** | CI لا ينجح (SDK 7 مقابل net8.0) + متغيّرات ميتة | E-27 · E-28 | Principle X | `setup-dotnet@v4` بـ`8.0.x` | 🔶 **مُصلَح في المصدر (2026-11-06)** — `8.0.x` + حذف المتغير الميت؛ **اجتياز CI نفسه غير مُسجَّل بعد** (لا تشغيل على GitHub Runner في هذه الجلسة) |
| GAP-23 | **P1** | تباعد اسم متغير Docker gateway (`DockerGateway` مقابل `DockerGatewayIp`) ⇒ الإعداد لا يربط في النشر الحاوي | E-36 | — | تصحيح اسم المتغير في `docker-compose.yml:38` + فحص runtime | مفتوحة — جديدة (مرحلة صفر) |
| GAP-24 | **P2** | Swagger مكشوف في الإنتاج | E-37 | — | حصره بـDevelopment أو حماية | مفتوحة — جديدة |
| GAP-25 | **P2** | ترحيل تلقائي عند الإقلاع + KnownProxy `0.0.0.0/0` في SQLite | E-38 | — | قرار مالك: بوابة ترحيل + تضييق proxies | مفتوحة — جديدة |
| GAP-26 | **P1** | فشل مفتوح في فلاتر التفويض (fail-open) يُبطل الحماية عند عطل DB | E-41 | G4 | جعل الفلاتر fail-closed | ✅ **مغلقة (2026-11-06 المرحلة 2)** — `context.Result = new UnauthorizedResult()` داخل `catch` في `ApiAuthorizationFilter` و`ExpiringTokenFilter` |
| GAP-20 | **P2** | انحراف في الاستشهادات | §5 | Principle XIII | تصحيح الاقتباسات | **مُغلق في هذه الجلسة** |
| GAP-21 | **P2** | `AGENTS.md` يوحي بأن 26 اختباراً قابلة للتنفيذ | E-34 · E-35 | Principle XIII | تصحيح `AGENTS.md` | **مُغلق في هذه الجلسة** |

---

## 5. تصحيحات على أدبيات مُصدَرة سابقاً

| # | الادعاء السابق | الصواب | المرجع |
|---|---|---|---|
| C-01 | "`HasControl` لا يُسنَد في أي مكان في الريبو" (دستور v2.0.0، §Evidence) | دقيق لأغراض مسار السيرفر→الجهاز، **غير دقيق حرفياً**: `Desktop.UI/Views/MainView.axaml:42` ربط ثنائي الاتجاه يكتب الخاصية. الأثر **أقوى** لا أضعف | E-06 · E-08 |
| C-02 | "66 صنف ترحيل" | **صحيح** — 66 من أصل 135 ملف | E-33 |
| C-03 | "26 طريقة اختبار" (AGENTS.md) | 26 إجمالاً، **19 داخل الحل**، و**0 مُنفَّذة** | E-34 · T-01 |
| C-04 | `RemoteControlSession.cs:91` (تقرير الهندسة العكسية) | السطر الصحيح **92** (`clone.ViewOnly = false;`) — دستور v2.0.0 مُحقَّق | E-05 |
| C-05 | مسار `Server/Shared/RemoteControlSession.cs` (اقتباس في تقرير المراجعة) | المسار الحقيقي `Server/Models/RemoteControlSession.cs` | E-05 |
| C-06 | "بناء `Server` مكسور بسبب LibMan" (مُفترض دائماً) |_libMan_ وحده، لا المزوّد ولا الشبكة — كل الأصول تُرجع 200 | E-25 |
| C-07 | `Desktop.Core` كمسار حيّ | **ميت** — غير مُدرج في الحل ولا مُشار إليه | E-30 |
| C-08 | E-29/GAP-15: "مسار مكتبة الواجهة مكسور (404)" | **false positive** — النص `/lib/microsoft/signalr-protocol-msgpack/...` في `Viewer.cshtml:33,38` مطابق لوجهة `libman.json` وللموجود على القرص | E-29 (المصحح) · تصحيح جلسة المرحلة صفر |

---

## 6. ADR + المخاطر

### 6.1 قرارات معمارية (ADR)

| ID | القرار | البدائل المرفوضة | السبب | الحالة |
|---|---|---|---|---|
| **ADR-P00-01** | وثائق الحوكمة (Decision Log · Gate Register · Evidence Map) **ملفات مستقلة** يُشير إليها الدستور، لا أقسام داخله | دمجها في نص الدستور | إبقاء v2.0.0 المُصادَق **غير قابل للمس**؛ تعديل الدستور يمرّ بإجراء التعديل الرسمي | **مُقترح — يحتاج التصديق** |
| **ADR-P00-02** | تصنيف الأدلة يتبع **الدستور** (6 طبقات)، مع جسر صريح إلى مصطلحات التكليف | اعتماد `SOURCE-CAPABILITY` كمصطلح مستقل في وثائقنا | الدستور هو حكم التسوية (§Enforcement)؛ الخلط بين طبقتين يُنتج استنتاجات أوسع مما يسمح به الدليل | **مُطبَّق** |
| **ADR-P00-03** | **لم** يُثبَّت .NET 8 runtime على جهاز التقييم | `winget install Microsoft.DotNet.Runtime.8` | تغيير نظامي يحتاج تصريح المالك صريحاً؛ و GAP-06 له مسار بديل (ADR-P00-04 + حاوية CI) | **مُقترح — يحتاج التصديق** |
| **ADR-P00-04** | إصلاح بناء `Server`: **إزالة كل إدخالات الاسترجاع من `libman.json`** بدل ترقية الأداة — الأصول كلها ملتزَمة في `wwwroot/lib/`، وrestore cdnjs كان يعيد كتابتها LF ويلوّث شجرة git بعد كل بناء (E-40) | (أ) `dotnet tool` أحدث · (ب) تبديل المزوّد إلى `jsdelivr` · (ج) تجاهل/`-LibraryManager` flag | الأداة لا تُحتاج أصلًا؛ والترقية تُبقي تلويث الشجرة (المشكلة الثانية التي كشفها التنفيذ الفعلي ولم تكن في التحليل النظري) | ✅ **نُفِّذ (المرحلة صفر، بتفويض ADR-P00-09)** |
| **ADR-P00-05** | الفجوات تُسجَّل **بأدلة** (مسار:سطر) لا بوصف عام | كل ادعاء مسنود بمرجع قابل لإعادة الفحص آلياً | **مُطبَّق** |
| **ADR-P00-06** | بوابة G7 = **UNKNOWN** لا FAIL | وضعها FAIL | لا يوجد سجل تشغيل ⇒ "فشل" و"غير معروف" معنيان مختلفان؛ والفرق يوجّه العمل: UNKNOWN ⇒ نكتسب الدليل | **مُطبَّق** |

### 6.2 المخاطر

| ID | الخطر | الاحتمال | الأثر | التخفيف |
|---|---|---|---|---|
| R-01 | خط الأساس يُعامَل كـ"مُقفل" بينما **لا يوجد اختبار قابل للتنفيذ** ⇒ ثقة زائفة | عالٍ | عالٍ | §0.1 يجعله أول سطر في الوثيقة · Gate Register يُظهر 0/7 بوابات · GAP-06 في P0 |
| R-02 | Decision Log يُقرأ كتّ مصطلح "قرارات مُعتمدة" وهي ليست كذلك | متوسط | متوسط | كل مدخل يحمل **المُعتمد** + **شرط العكس**؛ المداخل غير المُعتمدة تُوسم `PROPOSED` صراحة |
| R-03 | تحديث `AGENTS.md` بإدخال جديد يُنسَى في الغياب | منخفض | منخفض | تعديل طفيف (سطر مؤشر) فقط؛ التفاصيل في الوثائق الثلاث |
| R-04 | تعديل الدستور v2.1.0 بلا تصديق ⇒ دستور غير مُصادَق يبقى "مُصادَق" | منخفض | عالٍ | ADR-P00-01/02 وGAP-16 معلَّمة `PROPOSED`؛ **لم يُعدَّل `constitution.md`** |
| R-05 | إصلاح LibMan بالتخطّي (إزالة `-LibraryManager` من البناء) يترك الأصول منسيّة | متوسط | متوسط | ADR-P00-04 يختار تثبيت الأصول لا تجاهل الاسترجاع — الأصول **موجودة** اليوم في `wwwroot/lib/` |

---

## 7. المهام خطوة بخطوة (المقترحة — لم تُنفَّذ منها إلا خطوة P00-0)

| # | الخطوة | البوابة/الفجوة | تغيير | اختبار | التراجع |
|---|---|---|---|---|---|
| **P00-0** | **إقفال خط الأساس** (هذه الجلسة): إنشاء Evidence Map + Decision Log + Gate Register + تصحيح `AGENTS.md` | P00 | وثائق فقط | تحقق: git status ⇒ 0 ملف متتبَّع معدَّل | حذف الملفات |
| **P00-1** | تثبيت **.NET 8 runtime** (الخيارات: جهاز التقييم · أو صورة Docker/CI) | GAP-06 | تغيير نظامي | `dotnet --list-runtimes` يظهر 8.0.x | إزالة الحزمة |
| **P00-2** | إصلاح بناء `Server` (ADR-P00-04) | GAP-14 | `Server/libman.json` (+`Server.csproj` إن لزم) | `dotnet build Server/Server.csproj` ينجح | `git checkout -- Server/libman.json` |
| **P00-3** | تشغيل `dotnet test` كاملاً وتسجيل النتيجة | GAP-06 · SC-007 | بلا تغيير | 19 اختباراً تُنفَّذ وتُسجَّل | — (تسجيل فقط) |
| **P00-4** | تصحيح مسار المكتبة في `Viewer.cshtml` | GAP-15 | سطر واحد | فحص HTTP 200 على المسار | `git checkout --` |
| **P00-5** | تصحيح CI: `setup-dotnet@v4` بـ`8.0.x` + `libman restore` | GAP-19 | `.github/workflows/run_tests.yml` | تشغيل ناجح في CI | `git checkout --` |
| **P00-6** | ترقية الحزم المتأثرة (`NU1903`) | GAP-13 | `Server/Shared/Agent` `.csproj` | `dotnet build` بلا NU1903 | `git checkout --` |
| **P00-7** | تصحيح ادعاء `AGENTS.md` ("26 قابلة للتنفيذ") | GAP-21 | `AGENTS.md` | قراءة | `git checkout --` |
| **P00-8** | **SPEC-001** — تنفيذ FR-001…FR-029 مع اختبارات سلبية | G1 | كود إنتاج | اختبارات SPEC-001 | فرع/Tag |
| **P00-9** | إغلاق GAP-07/08/09 (المصادقة على نقاط API) | G4 | كود إنتاج | اختبارات 401/403 | — |

> ⚠️ **ترتيب مقصود:** P00-1 و P00-2 **قبل** P00-8. تنفيذ SPEC-001 قبل وجود خط أساس اختباري قابل للتنفيذ = ادعاء `[TEST-EVIDENCE]` بلا سند، وهو بالضبط ما يرفضه Principle XIII.

---

## 8. حالة البوابات (ملخّص — التفصيل في `GATE-REGISTER.md`)

| البوابة | الحالة | السبب |
|---|---|---|
| G1 — Server-enforced View Only | ❌ **FAIL** | لا فحص على السيرفر ولا على الجهاز (GAP-01…GAP-05) |
| G2 — Signed and verified client updates | ❌ **FAIL** | E-22 — بوابة القرار `OD-9` **حُسِمت بـ`AD-13`** (هجين + KMS/HSM + Key-ID)؛ الرُّزمة (R-07/GAP-13) تمت لكن التحقق من سلامة التحديث **لم يُبنَ بعد** — ينتظر مواصفة + تصريح تنفيذ + `[RELEASE-GATE]` |
| G3 — Safe file paths and transfer boundaries | ❌ **FAIL** | E-19 · E-20 |
| G4 — Authenticated and authorized device actions | ❌ **FAIL** | E-16 · E-07 · E-18 |
| G5 — Authenticated local IPC | ❌ **FAIL** | E-21 |
| G6 — Verified consent and privacy controls | ❬ **UNKNOWN** | آلية موافقة الحضور موجودة في المصدر (E-10) — بلا أي دليل تشغيلي |
| G7 — Privileged process isolation | ❬ **UNKNOWN** | E-13/E-14 على مستوى المصدر فقط — لا سجل هوية مُنفَّذ |

**المجموع: 0 PASS · 5 FAIL · 2 UNKNOWN.**

---

## 9. بوابة مرحلة P00

| شرط البوابة | الحالة | الدليل |
|---|---|---|
| الدستور مُصادَق | ✅ **PASS** | `.specify/memory/constitution.md` = v2.0.0 RATIFIED · `sha256:7B1D259F4658FD49` · سجل التصديق §"Ratification" |
| Decision Log موجودة | ✅ **PASS** | `DECISION-LOG.md` |
| Gate Register موجودة | ✅ **PASS** | `GATE-REGISTER.md` |
| بدون كود إنتاج | ✅ **PASS** | `git status --porcelain` ⇒ **صفر ملف متتبَّع معدَّل** (974 ملفاً) |
| الأدلة موثَّقة بمسار:سطر | ✅ **PASS** | هذا الملف، §1 |
| الفجوات مصنَّفة P0/P1/P2 | ✅ **PASS** | §4 |

### ✅ **حالة بوابة P00: PASS**

> **ما لا يقوله هذا PASS:** لا يعني أن المنتج جاهز. يعني أن **خط الأساس موثَّق ومُحكَم**، وأن البوابات **مسجَّلة صريحة** بحالاتها الحقيقية. عشر بوابات مستقلة عن هذه المرحلة تبقى كما هي: 5 FAIL · 2 UNKNOWN.

---

## 10. الإجراء التالي (واحد)

> **الحصول على خط أساس اختباري قابل للتنفيذ** — تثبيت **.NET 8 runtime** على جهاز التقييم + إصلاح بناء `Server` (P00-1 + P00-2).
>
> **لماذا هذا واحد وليس ثلاثة:** لا يمكن لـ SPEC-001 أن تُنفَّذ بجدوى قبل ذلك. كل بند في SPEC-001 يقابله اختبار، و SC-007 يشترط تشغيل المجموعة في CI. بدون .NET 8 runtime **لا يعمل اختبار واحد**، وبدون بناء `Server` لا يُبنى 18 من الـ19 ⇒ G1 لا يمكن أن يُقاس. وهذا لا يُلغي أي عمل حوكمة، لكنه **يفتح الطريق لكل عمل بعدها**.
>
> **يحتاج:** تصريح صريح من المالك (تغيير نظامي + تعديل في مشروع إنتاج). **لم يُنفَّذ.**

> **تحديث المرحلة صفر (2026-11-06):** شقُّ "إصلاح بناء Server" **نُفِّذ** (GAP-14 مغلقة)، وشقُّ "خط الأساس الاختباري" **تحقق جزئيًا** بـroll-forward (T-07: 19/19). المتبقي من هذا الإجراء: تشغيل الاختبارات على .NET 8 أصلي (تثبيت runtime أو حاوية CI) وتسجيل CI أخضر على GitHub Runner.

---

## 11. سجل التغيير

| التاريخ | التغيير |
|---|---|
| 2026-11-06 (P00) | إنشاء الخريطة — 39 استنتاجًا مُصنفًا + مصفوفة فجوات P0/P1/P2 |
| 2026-11-06 (المرحلة صفر) | تصحيح C-08: E-29/GAP-15 **false positive** · إضافة E-36…E-39 وGAP-23…GAP-25 · إغلاق GAP-14 (شطب unpkg من libman.json، T-05/T-06) · تنفيذ T-07: **19/19** بـ`DOTNET_ROLL_FORWARD=Major` · GAP-06 مغلقة جزئيًا · GAP-19 مُصلَحة المصدر (لم تُسجَّل CI green بعد) |
| 2026-11-06 (المرحلة 2 — R-04/PC-1) | حماية `DevicesController.Create` + `ClientDownloadsController` (`GetDesktop :35/:71` و`GetInstaller :117`) + `CustomBinariesController` بـ`ApiAuthorizationFilter` ⇒ **إغلاق GAP-07/08/09** · اكتشاف **E-41/GAP-26** (فلاتر fail-open) وإصلاحها fail-closed · **القبول الخطي** لتنزيل المثبّت العام (`/Content/*.zip` + `DownloadPackage`) مع التخفيفات — راجع DECISION-LOG (AD وD) وGATE-REGISTER PC-1 · بناء `Server` ناجح 0 أخطاء · لا تغيير على حالة بوابات G1–G7 |
| 2026-11-06 (المرحلة 3 — R-07/GAP-13) | **ترقيات سلسلة التوريد** (وكيلة قابلة للتنفيذ): SignalR `8.0.10→8.0.33` · MailKit `4.8.0→4.18.2` · MessagePack `2.5.187→2.5.307` · pins للـAgent (IO.Packaging/Pkcs/Xml) · CodeGeneration.Design→8.0.23 (أزال Microsoft.Build High) · محاذاة Server.Tests EF (NU1605). **النتيجة: صفر High/Moderate · build 0 أخطاء · 19/19** · متبقٍّ Low ×2 (NuGet تصميمي) — راجع E-42/T-08. **أمّا R-03/G2 (توقيع التحديثات) فبوابة قرار (OD-9)** — لم تُنفَّذ أي شيفرة توقيع (تحتاج إدارة مفاتيح معتمدة) |
| 2026-11-06 (المرحلة 3 — بوابة القرار) | **إغلاق `OD-9` بإجابة المالك ⇒ `AD-13`** (DECISION-LOG §1): نموذج هجين (Authenticode على المحمّلات + SHA-256 موقّع لكل حزمة يتحقق منه `UpdaterWin`/Linux/Mac قبل فكّ الضغط) · مفاتيح KMS/HSM خارج المستودع مع pubkey مضمَّن في العميل · إبطال بـKey-ID + رفض الأقدم. **لا شيفرة توقيع كُتبت** — G2 تبقى FAIL (مواصفة مرشَّحة SPEC-003 + تصريح تنفيذ + `[RELEASE-GATE]` باقية) — راجع GAP-10 وGATE-REGISTER G2 |
| 2026-11-06 (المرحلة 4 — R-05 PoC) | **PoC هوية G7 نُفِّذ على هذا الجهاز (Windows Server 2025، Build 26100، جلسة console 1)**: أداة `poc/G7Identity/` (خارج الحل، قياس فقط) أعادت إنتاج سلسلة `CreateInteractiveSystemProcess` حرفيًا من موضع مُكافئ للإنتاج (مهمّة مجدولة SYSTEM في الجلسة 0) وسجّلت الوقائع الست ⇒ **E-43 (جديد) وT-09 (جديد)** · تحديث EVIDENCE-MAP E-14/N-04 · العدد 46→47 · **G7: UNKNOWN → FAIL** (فُحص وفشل شرطا الهوية (1)(2) — بتوجيه المالك AD-14، لا بترقية ذاتية) · **G5/G6 لم تتغيّرا** (فحص الأنبوب الحيّ وموافقة الجلسة الحيّة لم يُنفَّذا) · تحفّظات التمثيلية موثّقة في E-43 (built-in Administrator، بلا UAC filtering؛ السطح الآمن غير مُختبَر) · الأداة والنتائج تُركت في الشجرة العمليّة **بلا commit** والمهمّة المُجدولة حُذفت بعد التشغيل |

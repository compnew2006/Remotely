# التقرير الهندسي — المعمارية والـTech Stack والفجوات البرمجية

**تاريخ المراجعة:** 2026-11-08  
**النسخة:** HEAD `73cfcb147d4d1162fb14fd7f0587e6cd04108061`  
**التصنيف:** EVOLVE / CRITICAL  
**النطاق:** بنية النظام، التقنيات والإصدارات، العيوب البرمجية، الاختبارات والتوافق. لا يتضمن هذا التقرير تقييمًا للهوية أو التفويض أو الأمان أو الخصوصية.

## Executive Summary

Remotely تطبيق self-hosted للتحكم عن بعد وتشغيل scripts. الخادم modular monolith مبني على ASP.NET Core وBlazor وSignalR، مع Agent وخدمة Desktop منفصلين. الاختيار العام مناسب لحجم المنتج؛ لا يوجد دليل يبرر الانتقال إلى microservices.

توجد فجوات برمجية مؤثرة تستدعي معالجة قبل اعتبار هذا الإصدار مستقرًا:

- استقبال ملفات المتصفح يمرر File DTO إلى معالج SessionMetrics بسبب غياب break، وقد يكرر chunks الملفات أو يفشل معالج المقاييس.
- تغيير مجلد تخزين screenshots أو camera أو audio لا ينقل الملفات القائمة، بينما تستخدم القراءة والحذف المسار الجديد.
- تنظيف الوسائط يعتمد على استقبال كتابة جديدة، لذلك لا يعمل كضمان زمني إذا توقف الجهاز أو الالتقاط.
- EF Core 8 ينتهي دعمه في 2026-11-10؛ الترقية تحتاج تنسيقًا مع مزودي قواعد البيانات، خصوصًا Npgsql.
- SDK المشروع مثبت على 10.0.403، بينما dotnet على PATH يستخدم SDK 9.0.315.
- أداة Update-Database تغيّر appsettings.json وتنفذ ثلاث قواعد بيانات افتراضية من دون حماية كافية لاستعادة الإعداد أو تحديد target نشر بوضوح.
- لا توجد اختبارات browser أو اختبارات relational migrations ظاهرة في CI.

**الحكم ضمن هذا النطاق: REQUIRES SIGNIFICANT CHANGES.** السبب هو عيب نقل الملفات، ومخاطر سلامة دورة حياة ملفات الوسائط، ونهاية دعم EF Core 8 القريبة، مع نقص اختبارات التوافق. هذا حكم على المعمارية والـtech stack والفجوات البرمجية فقط؛ لا يقرر جاهزية النشر من جوانب خارج هذا النطاق.

لم أشغّل build أو tests أو migrations أو التطبيق. لم تتغير ملفات المصدر.

## Your Understanding of the Project and Its Purpose

الغرض هو إدارة أجهزة عليها Agent من واجهة متصفح: مشاهدة الشاشة والتحكم بها، نقل ملفات، تشغيل scripts، وإدارة جلسات remote support. الخادم ينسق الجلسة والرسائل، ويخزن بيانات التطبيق ووسائط الالتقاط.

المشاريع الأساسية:

- Server: ASP.NET Core وBlazor وSignalR وواجهات HTTP وخدمات البيانات.
- Agent: خدمة اتصال وأوامر وبدء desktop client.
- Desktop.Shared: واجهات وخدمات سطح المكتب المشتركة.
- Desktop.Win وDesktop.Linux وDesktop.Native: تنفيذات خاصة بالمنصة.
- Desktop.UI: واجهة Avalonia.
- Shared: DTOs وentities وواجهات مشتركة.
- Tests: Server.Tests وShared.Tests وLoadTester.

الفرع يتضمن أيضًا Task Manager وscreen monitoring وcamera/audio capture وkeyboard capture وتحديثات العملاء. يزداد عبء الاختبار مع اختلاف قدرات المنصات ووجود browser client مترجم إلى JavaScript ومودع في المستودع.

## Current Architecture

الخادم تطبيق واحد بحدود داخلية حسب الخدمات والـhubs. هناك اتصال منفصل لكل من المتصفح والـAgent والـDesktop client، وقاعدة بيانات علائقية عبر EF Core وثلاثة مزودين، بالإضافة إلى ملفات وسائط داخل AppData.

~~~mermaid
flowchart LR
    Browser[Blazor and TypeScript client]
    Viewer[ViewerHub]
    Circuit[CircuitConnection and session state]
    DesktopHub[DesktopHub]
    Desktop[Desktop.Shared and platform client]
    AgentHub[AgentHub]
    Agent[Background Agent]
    Services[Server services and DataService]
    DB[(SQLite / SQL Server / PostgreSQL)]
    Files[(AppData and media files)]

    Browser <--> Viewer
    Viewer <--> Circuit
    Circuit <--> DesktopHub
    DesktopHub <--> Desktop
    Agent <--> AgentHub
    Circuit <--> AgentHub
    Services --> DB
    Services --> Files
    AgentHub --> Services
    DesktopHub --> Services
~~~

المسار الأساسي: المتصفح ↔ ViewerHub ↔ CircuitConnection ↔ DesktopHub ↔ Desktop client. يتصل Agent منفصلًا بـAgentHub، ويبدأ desktop process عند الحاجة. تستخدم طبقات الخادم DataService وIAppDbFactory للتعامل مع قاعدة البيانات وخدمات الملفات.

تظهر Graphify مراكز اعتماد متكررة حول Device وIDataService وIAppDbFactory وDataService وRemoteControlSession وCircuitConnection. هذا يتسق مع نظام يتمحور حول الجهاز والجلسة والخدمات المشتركة؛ ينبغي الحفاظ على هذه الحدود واضحة بدل إضافة طبقات توزيع جديدة بلا حاجة.

## Architecture Assessment

النموذج الحالي مناسب: خادم واحد للتنسيق والتخزين، وعميل Agent وخدمة Desktop منفصلان. لا أوصي بإعادة كتابة النظام أو تقسيمه إلى خدمات مستقلة.

نقاط التحسين المعمارية الأساسية:

- حالة الجلسات واتصالات الأجهزة موجودة في مخازن داخل العملية. هذا مناسب لخادم واحد، لكنه لا يثبت دعم تعدد نسخ الخادم أو failover. يجب توثيق حدود النشر الحالية قبل تصميم توزيع الحالة.
- مسؤوليات media موزعة بين خدمات الملفات وmetadata في قاعدة البيانات. تخزين الاسم فقط ثم إعادة بناء المسار من إعداد الجهاز الحالي يربط البيانات التاريخية بإعداد قابل للتغيير.
- DTO contracts موجودة في C# وTypeScript يدويًا. غياب اختبارات contract يسمح بانجراف enum أو serialization، كما يوضح fall-through في File handler.
- كل منصة لديها مجموعة ميزات مختلفة. واجهات Desktop.Shared مفيدة، لكن مصفوفة capability واضحة لكل OS وdisplay server غير ظاهرة للمستهلك.
- وجود ثلاثة providers يزيد عبء migrations واختبار schema ثلاث مرات. احتفظ بها إذا كانت مطلوبة فعليًا، لكن اجعل لكل provider اختبار ترقية على قاعدة علائقية مؤقتة.
- واجهات الخدمات تسهّل الفصل المنطقي، لكن يظل من المهم منع services من تنسيق قواعد بيانات وملفات عبر مسارات غير ذرية أو غير قابلة للاستعادة.

## Technology & Dependency Audit

| المجال | ما وجدته | التقييم |
|---|---|---|
| Runtime | المشاريع تستهدف net10.0 وglobal.json يثبت SDK 10.0.403 | اختيار .NET 10 ملائم؛ الدعم الرسمي حتى 2028-11-14. |
| EF Core | مراجع EF وASP.NET المرتبطة في Server على 8.0.33 | EF Core 8 ينتهي دعمه 2026-11-10، بعد يومين من تاريخ المراجعة. يلزم التخطيط لترقية متناسقة. |
| PostgreSQL | Npgsql.EntityFrameworkCore.PostgreSQL إصدار 8.0.10 | يجب ترقية provider بالتوافق مع إصدار EF Core المختار واختبار migrations الخاصة به. |
| SignalR browser | @microsoft/signalr وsignalr-protocol-msgpack إصدار 8.0.29 | لا يكفي فرق الإصدار وحده لإثبات عدم التوافق؛ اعتمد مصفوفة إصدار مدعومة للخادم والعميل. |
| MessagePack | @msgpack/msgpack إصدار 3.1.3 | إصدار مباشر محدد في package.json ومقيد بـpackage-lock.json. |
| TypeScript | Microsoft.TypeScript.MSBuild إصدار 5.6.2 | يُجمع ضمن dotnet build؛ package.json لا يعرّف build أو test أو lint أو typecheck للمتصفح. |
| Desktop capture | NAudio 2.2.1 وScreenRecorderLib 7.0.1 | قدرات التسجيل مرتبطة ببناء Windows x86/x64؛ لا تعني أن البناء AnyCPU أو Linux يوفر الميزة نفسها. |
| إدارة الحزم | Server/package-lock.json موجود | لم أجد packages.lock.json أو Directory.Packages.props. الإصدارات المباشرة محددة، لكن NuGet transitive dependency graph غير مقيد بملف lock. |

مصادر الإصدارات: global.json، ملفات csproj، Server/package.json، Server/package-lock.json. استُخدمت نتائج Context7 الرسمية لدورة دعم .NET وEF Core وسياسة SignalR. لم يُفحص dependency graph العابر بالكامل، ولم أتحقق من build بعد أي ترقية.

محليًا، dotnet على PATH يشير إلى SDK 9.0.315؛ المسار الصريح داخل ملف المستخدم يعرض SDK 10.0.403. CI يقرأ global.json عبر actions/setup-dotnet. لذلك اختلاف المضيف لا يثبت عيبًا في المشروع، لكنه يجعل أوامر التطوير والصيانة المحلية قابلة للفشل إذا استُخدم executable غير المقصود.

## Missing Engineering Foundations

| المكوّن | الحالة | الدليل |
|---|---|---|
| Browser tests | MISSING في نطاق المشروع المفحوص | Server/package.json لا يعرّف test script، ولم تظهر browser tests في CI. |
| اختبار DTO contract بين C# وTypeScript | PARTIAL | enum والـDTOs معرفة في الجانبين ولا يظهر اختبار يثبت تطابقها. |
| اختبارات migrations على قواعد علائقية | MISSING في الاختبارات المفحوصة | Server.Tests يستخدم EF InMemory؛ لم أعثر على اختبار apply/upgrade لمزود SQLite أو SQL Server أو PostgreSQL. |
| CI | PARTIAL | CI يبني Server ويختبره على Ubuntu، ويبني الحل ويختبر Shared على Windows. Container workflow منفصل ولا يعتمد على نجاح CI. |
| ثبات Dependencies | PARTIAL | npm lock موجود؛ لا يوجد NuGet lockfile أو central package file. |
| توافق المنصات | PARTIAL | build واحد لا يثبت تساوي قدرات Windows وLinux وmacOS. |
| فحص التزامن | PARTIAL | توجد caches متزامنة، لكن بعض shell/session lifecycle paths لا تحتوي serialization أو compare-and-remove. |
| استقرار media metadata | PARTIAL | metadata تخزن اسم الملف، بينما مسار الوصول مشتق من إعداد متغير على مستوى الجهاز. |
| lint/typecheck مستقل | UNKNOWN / غير معرّف في الملفات التي فُحصت | لا توجد أوامر lint أو typecheck في package.json أو CI المعروض. |

## Code-Level Gaps & Reliability Findings

### C-01 — File DTO يمرر إلى معالج المقاييس

**الشدة:** High. **الثقة:** عالية في fall-through؛ الأثر يحتاج اختبار browser.  
**الدليل:** Server/wwwroot/src/DtoMessageHandler.ts:46-49 وDtoMessageHandler.js:30-33. File case لا يتوقف قبل SessionMetrics. كلا المعالجين يستدعيان TryComplete، التي تضيف wrapper إلى قائمة chunks في Shared/Helpers/DtoChunker.cs:35-70. UI.UpdateMetrics تستخدم toFixed على حقول رقمية في Server/wwwroot/src/UI.ts:191-195.  
**الأثر والسبب:** قد تُضاف chunks الملف مرتين، وتُمرر File wrapper إلى فك SessionMetrics. هذا يعرّض استقبال الملفات متعددة الأجزاء للتلف، وقد يفشل تحديث المقاييس. السبب غياب نهاية واضحة لحالة File.  
**التوصية:** أضف break أو dispatch حصريًا، وحافظ على تزامن TS وJS المودعين.  
**الاختبار:** browser test لملف صغير وملف متعدد الأجزاء، مع مقارنة hash ومحتوى الملف واختبار metrics مستقل.  
**مخاطر التغيير:** منخفضة، مع ضرورة تحديث JS المولد وفق convention المستودع.

### C-02 — تغيير مجلد media يفصل metadata عن الملف

**الشدة:** Medium. **الثقة:** عالية.  
**الدليل:** Screenshot وCameraClip وAudioClip تحتفظ باسم الملف دون المجلد. DataService يغيّر إعدادات المجلد ويحفظها دون نقل الملفات (DataService.cs:2249-2276, 2311-2318, 2353-2360). خدمات القراءة والحذف تستخدم المجلد الحالي مع اسم الملف المسجل.  
**الأثر والسبب:** بعد تغيير المجلد قد لا تعود الملفات القديمة قابلة للعرض أو التنزيل؛ وقد تترك الملفات القديمة orphaned عند الحذف أو retention.  
**التوصية:** خزن مسارًا أو معرف object ثابتًا على كل media row، أو نفّذ نقلًا ذريًا مع سجل استعادة.  
**الاختبار:** غيّر المجلد ثم اختبر listing والتنزيل والحذف والاحتفاظ لملفات أُنشئت قبل التغيير وبعده.  
**مخاطر التغيير:** متوسطة إلى عالية للبيانات الحالية؛ تحديد مكان الملفات القديمة قد يحتاج reconciliation.

### C-03 — تنظيف retention ليس دوريًا لكل media

**الشدة:** Medium. **الثقة:** عالية.  
**الدليل:** ScreenshotService وCameraService وAudioService وKeyboardLogService تنظف بعد حفظ بيانات جديدة. DataCleanupService يشغل CleanupOldRecords، لكنه لا يستدعي خدمات media.  
**الأثر والسبب:** إذا توقف الجهاز أو الالتقاط، قد لا يُنفذ التنظيف عند انقضاء المدة المحددة.  
**التوصية:** أضف sweep دوريًا يستهدف media metadata والملفات، واترك تنظيف ما بعد الكتابة كتحسين لا كمسار وحيد.  
**الاختبار:** اترك ملفًا منتهيًا بلا كتابات لاحقة، ثم شغّل cleanup وتحقق من metadata والملف.  
**مخاطر التغيير:** متوسطة بسبب تنسيق حذف DB والملفات عند فشل أحدهما.

### C-04 — إزالة اتصال قديم قد تمحو mapping الأحدث

**الشدة:** Medium. **الثقة:** عالية.  
**الدليل:** AgentHubSessionCache.AddOrUpdateByConnectionId يستبدل device-to-connection mapping في الأسطر 25-29، لكن TryRemoveByConnectionId يحذف mapping الجهاز بلا تحقق من أنه ما زال يشير للاتصال المنقطع في الأسطر 72-79.  
**الأثر والسبب:** إذا سجل اتصال B ثم انقطع A، قد يحذف callback الخاص بـA مسار B. قد يظهر الجهاز غير متصل أو لا تصله الأوامر حتى reconnect آخر.  
**التوصية:** استخدم compare-and-remove على قيمة connection ID الحالية أو generation number.  
**الاختبار:** سجّل B، ثم نفّذ disconnect لـA، وتحقق من أن mapping يبقى على B.  
**مخاطر التغيير:** منخفضة.

### C-05 — shell مشترك يتعامل مع أوامر متزامنة

**الشدة:** Medium. **الثقة:** عالية لغياب قفل في المسار المفحوص.  
**الدليل:** ScriptingShellFactory يخزن shell لكل sender (Agent/Services/ScriptingShellFactory.cs:24-49)، وPsCoreShell.WriteInput يعدّل كائن PowerShell المشترك دون lock (PsCoreShell.cs:77).  
**الأثر والسبب:** أوامر أو completion متزامنة قد تتداخل مخرجاتها أو تنتج عمليات shell مكررة بسبب سباق الإنشاء.  
**التوصية:** single-flight لإنشاء shell وتسلسل عمليات كل session مع الحفاظ على shell state التفاعلي.  
**الاختبار:** أرسل أوامر متزامنة من الاتصال نفسه وتحقق من instance واحدة ومخرجات معزولة.  
**مخاطر التغيير:** متوسطة؛ ينبغي الحفاظ على دلالات PowerShell التفاعلية.

### C-06 — أداة تحديث قواعد البيانات تعدّل الإعداد وتستخدم targets افتراضية

**الشدة:** Medium. **الثقة:** عالية.  
**الدليل:** Utilities/Update-Database.ps1:39-51 يغير DbProvider خلال تشغيل SQLite وSQL Server وPostgreSQL، وينهي الملف على SQLite. Design-time factories في Server/Data/DesignTimeContexts.cs:10-14, 28-32, 46-50 تحتوي connection strings محلية ثابتة.  
**الأثر والسبب:** قد تنفذ الأداة migrations على قاعدة غير المقصودة، وتترك الإعداد مختلفًا عن قيمته الأصلية. لا يظهر خيار provider أو target صريح للمشغل.  
**التوصية:** افصل إنشاء migration عن تطبيقها؛ اجعل provider والاتصال explicit، واحفظ الإعداد واستعده في finally، وتحقق من exit code لكل أمر.  
**الاختبار:** نفذ حالات النجاح والفشل لكل provider على قواعد disposable، وتحقق من استعادة الإعداد وعدم متابعة providers بعد فشل سابق.  
**مخاطر التغيير:** عالية إذا نُفذت الأداة على قاعدة خاطئة؛ متوسطة لتعديل الأداة.

### C-07 — مصفوفة دعم desktop غير مكتملة

**الشدة:** Medium. **الثقة:** عالية.  
**الدليل:** AppLauncherMac يعيد Feature under development لمهام desktop، وتعليق AppLauncherLinux يصف محدودية Wayland؛ ميزات ScreenRecorderLib محصورة في Desktop.Win وتكوينات x86/x64.  
**الأثر والسبب:** حالة Agent online لا تعني تساوي screen control أو recording بين الأنظمة.  
**التوصية:** انشر capability matrix حسب OS وdisplay server وarchitecture، واجعل الواجهة تعرض القدرات المتاحة فعليًا.  
**الاختبار:** smoke tests على Windows x64/x86 وLinux X11/Xwayland، وعلى كل منصة معلن دعمها.  
**مخاطر التغيير:** منخفضة لإظهار capability flags، وعالية لإضافة دعم أصلي جديد.

### C-08 — CI لا يثبت سلامة artifact المنشور أو client في المتصفح

**الشدة:** Medium. **الثقة:** عالية على إعداد الملفات.  
**الدليل:** .github/workflows/ci.yml يبني Server ويختبر Server.Tests على Ubuntu، ويبني الحل ويختبر Shared.Tests على Windows. لا تظهر browser tests. .github/workflows/container.yml ينشر عند tag أو تشغيل يدوي ولا يعتمد على نجاح CI، وينشر ghcr.io/compnew2006/remotely بينما docker-compose/docker-compose.yml يستخدم immybot/remotely:latest.  
**الأثر والسبب:** يمكن نشر container من دون علاقة صريحة بنتيجة CI، وملف Compose لا يشير إلى artifact الذي ينتجه هذا workflow.  
**التوصية:** اجعل publish مرتبطًا بنجاح CI، وعرّف image tag في مكان واحد، وأضف smoke test للـcontainer واختبارات نقل ملفات في المتصفح.  
**الاختبار:** تحقق من أن artifact لا يُنشر عند فشل CI، وأن Compose يستخدم tag الناتج، وأن health endpoint يعمل بعد بدء الحاوية.  
**مخاطر التغيير:** متوسطة؛ قد تتغير طريقة النشر الحالية.

## Technical Debt & Maintainability

- وجود ثلاثة migration trees يجعل أي تعديل schema يحتاج تحققًا متكررًا.
- TypeScript وJavaScript موجودان معًا؛ يجب أن يكون تحديثهما واختبار تزامنهما جزءًا من إجراء client.
- حدود تخزين media تجمع DB metadata ونظام الملفات من دون هوية ثابتة للموقع.
- caches داخل العملية تفترض ضمنيًا server instance واحدة. لا يوجد في الملفات المفحوصة ما يثبت multi-instance support.
- لا توجد أوامر npm test/lint/typecheck؛ CI لا يشغّل browser-level verification.
- تقرير EGM الحالي يفحص 96 قسمًا: 78 لا تسجل drift و18 تسجل نقصًا في توثيق واجهات أو exports. هذا تدقيق لوثائق العقود وليس سلوك التشغيل.
- ملفات NuGet تحدد الإصدارات المباشرة لكن لا يوجد lockfile للشجرة العابرة، ويجب إدارة ترقية EF/provider كحزمة منسقة.

## Recommended Target Architecture

احتفظ بـmodular monolith، واجعل الحدود البرمجية حول session orchestration وdesktop platform adapters وmedia persistence وdatabase providers أكثر صراحة. لا تضف توزيعًا أو خدمات جديدة حتى تظهر حاجة حمل أو توافر مقاسة.

~~~mermaid
flowchart LR
    Browser[Browser client]
    Hubs[SignalR hubs]
    Orchestrator[Session and device orchestration]
    Desktop[Desktop.Shared]
    Platform[Windows / Linux adapters]
    Media[Media repository]
    Database[DataService and provider adapters]
    DB[(Relational database)]
    Files[(Stable media paths)]
    Cleanup[Scheduled retention worker]
    Agent[Background Agent]

    Browser <--> Hubs
    Hubs <--> Orchestrator
    Orchestrator <--> Agent
    Orchestrator <--> Desktop
    Desktop --> Platform
    Database --> DB
    Media --> Files
    Cleanup --> Media
    Cleanup --> Database
~~~

التصميم المستهدف:

1. طبقة session orchestration تنسق Hubs والخدمات من دون امتلاك تفاصيل filesystem أو EF.
2. Media repository يحتفظ بموقع ثابت لكل عنصر ويعالج نقل/حذف الملفات مع metadata.
3. Database provider adapters تحمل إعداد كل مزود ومهاجراته واختبارات توافقه.
4. Retention worker يعمل دوريًا وبشكل idempotent.
5. Platform capability registry يعلن ما يمكن لكل Agent وDesktop build تنفيذه.
6. Browser contract tests تتحقق من serialization وDTO dispatch، وCI يختبر artifact الفعلي.

## Alternative Designs & Trade-offs

| القرار | الاستمرار والتحسين | البديل | المفاضلة |
|---|---|---|---|
| خادم واحد | أبسط في التشغيل ويلائم الشكل الحالي | عدة نسخ مع shared state | يزيد التشغيل والتكلفة؛ لا يبرره المصدر من دون متطلبات availability أو load. |
| ثلاثة DB providers | يحافظ على خيار النشر الحالي | دعم provider واحد | يقلل عبء migrations والاختبارات، لكنه قرار منتج يجب أن يستند إلى deployments الفعلية. |
| filesystem للوسائط | بسيط ومتوافق مع AppData | object storage | يفيد الأحجام الكبيرة أو تعدد نسخ الخدمة، لكنه يحتاج إعدادًا وتشغيلًا إضافيين. |
| abstractions للمنصات | تكفي حين تتقارب وظائف OS | فصل مشاريع capabilities حسب OS | يقلل شروط runtime لكنه يزيد مشاريع packaging؛ استخدمه إذا اتسعت الفروقات. |
| عقود C#/TypeScript يدوية | لا تحتاج مولدًا جديدًا | توليد DTOs أو contract package | يقلل drift لكنه يضيف build tooling؛ ابدأ باختبارات contract قبل إدخال مولد. |

لا أوصي بإعادة كتابة النظام أو استخدام Microservices أو Kubernetes. الأولوية هي تصحيح تدفق البيانات، ودورة حياة الملفات، وترقية المكتبات واختبارات المزودين.

## Prioritized Remediation Roadmap

### المرحلة 1 — إصلاح تدفق الملفات والعقود

**الملفات:** Server/wwwroot/src/DtoMessageHandler.ts ونسخة JS وbrowser test files.  
**الجهد:** منخفض. **المخاطر:** منخفضة.  
**الاختبارات ومعايير القبول:** نقل ملف من chunk واحد ومن عدة chunks؛ المحتوى والـhash يطابقان المصدر؛ لا يستدعى metrics handler مع File DTO.  
**التراجع:** إعادة client assets السابقة إن ظهر خلل توافق.

### المرحلة 2 — تثبيت تخزين الوسائط وretention

**الملفات:** ScreenshotService وCameraService وAudioService وShared.Entities وDataService.  
**الجهد:** متوسط إلى عالٍ. **المخاطر:** عالية على البيانات القديمة.  
**الاعتمادات:** تحديد مسارات الملفات الحالية وسياسة ترحيلها.  
**الاختبارات ومعايير القبول:** تغيير المجلد لا يفقد الملفات القديمة؛ cleanup يحذف metadata والملفات بعد انتهاء الوقت حتى دون كتابة جديدة؛ عمليات الحذف المتكررة آمنة.  
**التراجع:** نسخة متسقة من DB وAppData قبل migration مع إجراء restore موثق.

### المرحلة 3 — معالجة التزامن وحالة الاتصالات

**الملفات:** AgentHubSessionCache وScriptingShellFactory وPsCoreShell.  
**الجهد:** متوسط. **المخاطر:** متوسطة.  
**الاختبارات ومعايير القبول:** reconnect لا يمحو mapping الأحدث؛ إنشاء shell مرة واحدة؛ أوامر متزامنة لا تخلط المخرجات.  
**التراجع:** إبقاء السلوك السابق وراء إعداد مؤقت إذا ظهرت مشكلة في shell state.

### المرحلة 4 — توحيد migrations وSDK

**الملفات:** Utilities/Update-Database.ps1 وDesignTimeContexts.cs وUtilities/Add-Migration.ps1 وPublish.ps1 وglobal.json.  
**الجهد:** متوسط. **المخاطر:** متوسطة إلى عالية لتغييرات DB.  
**الاختبارات ومعايير القبول:** كل provider يُختبر على قاعدة disposable؛ لا تتغير إعدادات appsettings بعد الفشل؛ كل script يستخدم SDK المتوافق مع global.json.  
**التراجع:** عدم تطبيق migrations على بيانات حقيقية حتى نجاح dry-run على نسخة مستعادة.

### المرحلة 5 — ترقية المكتبات وتحسين CI

**الملفات:** Server.csproj ومشاريع providers وServer/package.json وpackage-lock.json و.github/workflows.  
**الجهد:** متوسط إلى عالٍ. **المخاطر:** متوسطة بسبب تغيير EF migrations.  
**الاعتمادات:** اختيار إصدارات متوافقة ومدعومة لكل provider وSignalR client.  
**الاختبارات ومعايير القبول:** build نظيف، اختبارات server/shared، migrations لكل provider، browser contract tests، وcontainer smoke test؛ publish لا يعمل إذا فشل CI.  
**التراجع:** تثبيت إصدارات الحزم السابقة والاحتفاظ بartifact سابق وخطة downgrade schema.

## Risks, Unknowns & Verification Needs

- EF Core 8 ينتهي دعمه بعد يومين من تاريخ المراجعة؛ لم يُنفذ upgrade أو فحص compatibility للنسخة التالية.
- لم يُشغّل build أو tests أو التطبيق؛ لذلك كل النتائج هنا مبنية على المصدر وملفات CI.
- اختبار browser الفعلي، وقياس UI أو performance، واختبار Windows/Linux runtime لم تُنفذ.
- Graphify أعاد 10,828 nodes و24,458 edges و545 communities و90% EXTRACTED. ملفات الرسم أحدث من timestamp commit لكن لا تحمل source SHA، لذلك مراجعة المصدر المباشرة هي المرجع.
- Serena كان نشطًا وC# LSP جاهزًا، لكن الأداة لا تعرض revision status مستقلًا.
- codebase-memory-mcp غير متاح في tool inventory — **SKIPPED: codebase-memory-mcp - NOT AVAILABLE - native reads / Serena / Graphify fallback used**.
- لا أعد نتائج جلسات سابقة بديلًا عن build أو tests في هذه المراجعة.
- `git status` قبل التحرير كان نظيفًا؛ Git أصدر تحذيرًا لعدم قدرته على قراءة global ignore file. لا يغير ذلك قائمة الملفات التي عرضها status.

### معايير القبول AC

| المعيار | الحالة | الدليل |
|---|---|---|
| AC-1 — وصف المعمارية ومسارات البيانات | MET | قراءة الحل والمشاريع وProgram وHubs والخدمات؛ الرسم مدعوم بـGraphify ومراجعة المصدر. |
| AC-2 — حصر الـtech stack والدعم والتوافق | PARTIAL | الإصدارات المباشرة ودورة الدعم مثبتة؛ لم يُفحص كل dependency عابرة أو مصفوفة توافق runtime. |
| AC-3 — تشخيص الفجوات البرمجية | MET على مستوى المصدر؛ runtime UNVERIFIED | النتائج C-01 إلى C-08؛ لم تُشغّل اختبارات أو التطبيق. |
| AC-4 — خطة إصلاح قابلة للقبول والقياس | MET | المراحل تحدد الملفات والاختبارات ومعايير القبول ومخاطر التغيير. |

## Implementation Follow-up — C-01 إلى C-08

**تاريخ التنفيذ:** 2026-11-08  
**النطاق:** إصلاحات المصدر المحددة في C-01 إلى C-08 فقط.

### حالة التنفيذ

- **C-01 — منفذ:** أضيف إنهاء صريح لحالات File وSessionMetrics في TypeScript وJavaScript المودع وsource map. اختبار نقل الملفات في المتصفح لم يُشغّل.
- **C-02 — جزئي:** أضيفت `StorageDirectory` الاختيارية وترحيلات SQLite وSQL Server وPostgreSQL. تُسجل المسارات الكاملة للوسائط الجديدة، ويُسند المسار للصفوف القديمة فقط عند العثور على الملف في المجلد الحالي أو الافتراضي. صفوف الملفات الموجودة في مجلد مخصص سابق غير معروف تظل بلا مسار قابل للاستعادة.
- **C-03 — جزئي:** أضيف sweep دوري كل ساعة، مع معالجة الوسائط على دفعات من 250 صفًا وتأجيل حذف metadata عند فشل حذف الملف. ترجمة `string.Compare` في استعلام PostgreSQL لم تثبت من الوثائق المتاحة أو من قاعدة فعلية.
- **C-04 — منفذ على مستوى المصدر:** أضيف قفل لكل جهاز حول التسجيل وheartbeat والانقطاع، وأصبح حذف mapping مشروطًا بأن ما زال يشير إلى الاتصال نفسه. لم يُشغّل اختبار ضغط للتزامن.
- **C-05 — منفذ على مستوى المصدر والبناء:** إنشاء shells أصبح single-flight. عمليات PowerShell وexternal shell متسلسلة لكل جلسة، ونتيجة الإكمال تُنسخ إلى DTO داخل القفل. تنظيف الجلسات يتجنب shell المشغول. لم يُشغّل اختبار تنافس runtime.
- **C-06 — منفذ:** أدوات Add/Remove/Update تستهدف provider واحدًا صراحة، وتستخدم connection variable الخاص به، وتتحقق من exit code. Update لا يغير `appsettings.json` ويدعم `ShouldProcess`/`-WhatIf`. لم تُطبّق migrations على قواعد بيانات.
- **C-07 — جزئي:** أضيفت مصفوفة قدرات حسب المنصة والمعمارية، وربطت بها عناصر DeviceCard وAudioModal. الدردشة مُمثلة كقدرة مستقلة عن Desktop adapter. Linux يظل Conditional لأن heartbeat لا يحدد X11 مقابل Wayland؛ لم يُختبر أي جهاز فعلي.
- **C-08 — جزئي:** صار نشر الحاوية يعتمد على نجاح workflow CI، وتطابق اسم الصورة المنشورة مع Compose. لم تُضف أو تُشغّل container smoke test أو browser file-transfer test، ولم يُشغّل CI أو النشر.

### التحقق

- نجح `dotnet build Remotely.sln --no-restore --verbosity quiet` عبر SDK 10 الصريح: 0 أخطاء و27 تحذيرًا.
- نجح PowerShell AST parse للسكريبتات الثلاثة: Add-Migration وRemove-Migration وUpdate-Database.
- لم تُظهر Serena diagnostics أخطاء في ملفات C# المتغيرة التي فُحصت.
- نجح `git diff --check`. تحذيرات Git كانت عن تحويل line endings إلى CRLF.
- أعاد Impeccable detector قائمة فارغة للواجهة ومصفوفة القدرات المعدلتين.
- لم تُشغّل اختبارات، أو تطبيقًا، أو متصفحًا، أو CI. لم تُطبّق migrations ولم يُنشر artifact.
- لم يجد Context7 توثيقًا مطابقًا لترجمة `string.Compare` في Npgsql 8.0.10؛ يظل سلوك PostgreSQL غير متحقق.
- `codebase-memory-mcp` غير متاح. Graphify snapshot لا يشمل تغييرات working tree؛ محاولة التحديث السابقة توقفت عند صلاحيات ملف Git ignore العام. استُخدمت قراءة المصدر وSerena كبدائل.

### معايير القبول بعد التنفيذ

| المعيار | الحالة | الدليل أو القيد |
|---|---|---|
| AC-1 — فصل File DTO عن SessionMetrics مع تطابق TS/JS | MET | الكود المودع متطابق في dispatch والبناء نجح؛ نقل الملفات runtime غير مختبر. |
| AC-2 — بقاء وسائط ما قبل تغيير المجلد قابلة للوصول | PARTIAL | يُستعاد المسار القديم إن وجد الملف في المجلد الحالي أو الافتراضي؛ مسار custom سابق غير محفوظ لا يمكن استنتاجه. |
| AC-3 — انتهاء retention دون كتابة جديدة | PARTIAL | العامل الدوري موجود؛ استعلام pagination لم يُختبر على قواعد providers، وترجمة PostgreSQL غير مؤكدة. |
| AC-4 — disconnect قديم لا يمحو اتصالًا أحدث | MET | compare-and-remove وقفل لكل جهاز موجودان؛ لا يوجد اختبار تزامن منفذ. |
| AC-5 — تسلسل إنشاء shells وعملياتها | MET | single-flight والأقفال وidle sweep بنوا بنجاح؛ لا يوجد اختبار تنافس runtime. |
| AC-6 — migration utility ذات provider وهدف صريحين | MET | فحص المصدر وAST نجحا؛ لم يُنفذ أي اتصال أو migration فعلي. |
| AC-7 — تمثيل قدرات Desktop حسب المنصة | PARTIAL | المصفوفة والواجهة موجودتان؛ Linux display server غير معلوم ولا توجد تجربة منصة فعلية. |
| AC-8 — نشر مربوط بـCI والصورة المستخدمة متطابقة | PARTIAL | الربط واسم GHCR متوافقان؛ لا smoke/browser tests ولا نتيجة CI أو publish. |

## Final Engineering Verdict

**REQUIRES SIGNIFICANT CHANGES**

تحققت إصلاحات المصدر الأساسية وبُني الحل بنجاح، لكن AC-2 وAC-3 وAC-7 وAC-8 ما زالت جزئية. يلزم حسم مسارات media القديمة المعروفة، والتحقق من استعلامات كل provider، وتجربة المنصات، وتشغيل اختبارات browser والحاوية وCI قبل قبول هذا النطاق رسميًا.

هذا الحكم يقتصر على المعمارية والـtech stack والفجوات البرمجية. لا يقيّم الهوية أو التفويض أو الأمان أو الخصوصية، ولا يمثل قبولًا رسميًا من CI أو Owner.

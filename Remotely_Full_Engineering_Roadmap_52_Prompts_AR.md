# الخطة التنفيذية الشاملة — Remotely Enterprise Capture & Live View Extension

**Roadmap + Stage Gates + Checklists + 52 Agent Prompts + Spec Kit Workflow**  
**الإصدار:** 1.0 — 6 نوفمبر 2026  
**النطاق:** تعديل Fork من `immense/Remotely` للسيرفر والعميل، وإضافة `Capture Helper` داخل نفس حزمة العميل مع `ScreenRecorderLib` كمرشح خلف `ICaptureEngine`، وتوسيع جلسات View Only باختيارات تشغيل مستقلة.  
**الحالة:** وثيقة تخطيط، **وليست دليلًا على تنفيذ أي إصلاح أو اجتياز اختبار**.

> **كيفية الاستخدام:** هذه الخطة مصممة لتُحفظ في مستودع Remotely (بعد مراجعتها) باسم `docs/ROADMAP-REMEDIATION-AND-CAPTURE-AR.md`. ابدأ من مرحلة P00، ولا تنتقل إلى المرحلة التالية إلا إذا اجتازت الـGate الموثق. كل مرحلة تحتوي على Checklist ونتائج متوقعة وقواعد قبول وبرومبت بدء مخصص. في آخر الملف **52 برومبتًا مصممة لهذا المشروع تحديدًا**، مرتبطة بعناوين ملفك الأصلي `Prompts-For-Coding-And-Developers-AR.md` على نفس الترتيب 1–52، دون نسخ النصوص الأصلية حرفيًا.

---

## 1. ملخص القرار الهندسي والقيود

### 1.1 اتجاه المنتج

- **لا نبني سيرفرًا مستقلًا من الصفر:** نستخدم `Remotely Server` (ASP.NET Core/Blazor/SignalR) كقاعدة ونضيف الوحدات فوق نقاط التوسعة. لا نستبدل وظائف الدعم البعيد القائمة بلا ضرورة.
- **لا نثبت برنامجين منفصلين على الأجهزة:** تُوزّع عملية `Capture Helper` الإضافية داخل **نفس Installer/Updater** الخاص بـRemotely Client، مع بقاء `Windows Service` مسئولًا عن الاتصال والجدولة والرفع، وHelper يعمل داخل جلسة الموظف **بهوية المستخدم وأقل امتياز**، لا كـSYSTEM. تفاصيل طريقة الإطلاق تثبت بالـPoC.
- **مكتبة ScreenRecorderLib على الـClients فقط:** مرشح أولي خلف `ICaptureEngine`. لا نربط إصدارها ولا نعدل ملفات تشغيل الإنتاج قبل إثبات توافق .NET الحديث، x64، MSVC/Media Foundation، dual-monitor، الكاميرا والميكروفون والأداء.
- **الميزات المباشرة Live View ليست التسجيلات المجدولة:** جلسة `ViewOnly` تعرض مصادر مستقلة عبر Checkboxes/Toggles: شاشة، صوت الكمبيوتر (loopback)، ميكروفون، كاميرا. تستطيع تفعيل مصدر/إيقافه في أثناء الجلسة، أو الجمع بين مصادر متعددة. لا توجد 4 Modes جامدة، ولا تحول تفعيل مصدر إلى `FullControl`.
- **الخصوصية جزء من المعمارية لا UI:** إذن سابق ومحدد وقابل للإلغاء، إشارة ظاهرة على جهاز الموظف، `Privacy Pause` محلي يفوز على أوامر الأدمن؛ عند شك في تسجيل محادثة خاصة نفشل إلى الإيقاف لا إلى التسجيل ثم الحذف.

### 1.2 المتطلبات الثابتة (Product Baseline)

| المجال | المتطلب المتفق عليه | معيار إثبات رئيسي |
|---|---|---|
| حجم النشر | حوالي 50 جهازًا داخل LAN | محاكاة 50 عميلًا واختبار تجريبي واقعي |
| الأجهزة | Windows 10/11، غالبًا Intel Gen 5–10 وRAM ‏8–16 GB، غالبًا شاشتان FHD | Device inventory وHardware compatibility matrix |
| السيرفر | Windows Server، ≥16GB RAM، قرص HDD نحو 1TB | اختبار تحميل وسعة I/O واقعية |
| إدارة الأجهزة | أجهزة AD وغير AD، مسؤول مركزي واحد في البداية | enrollment/revocation/least privilege لكل جهاز |
| الخدمة | Windows Service عند الإقلاع، تستمر بعد Logout في الإرسال والاتصال | Windows lifecycle tests |
| الالتقاط | في جلسة الموظف فقط وبموافقته، يتوقف بعد logout | Process token/session test + negative tests |
| Screen screenshots | شاشتان، فاصل زمني مختلف لكل جهاز | الصور صالحة وترتيب الشاشتين صحيح |
| Screen video | مدد مجدولة، **صوت الميكروفون فقط** وليس system loopback | فحص مسارات الصوت في ملف العينة |
| Mic-only | تسجيل صوت مستقل، ويلغى عند تعارضه مع Screen Video | scheduler collision test |
| Webcam | صور وفيديو **بلا صوت** وبإذن منفصل | كاميرا مغلقة عند سحب الإذن وعدم وجود audio track |
| Live View | Checkboxes مستقلة للشاشة، System Audio، Mic، Webcam خلال View Only | server/device confirm + independent permissions + stop controls |
| الشبكة | رفع بعد إتمام الوسائط؛ استئناف وتجزئة وTLS | قطع الشبكة وإعادتها وغياب التكرار |
| التخزين المحلي | حد أقصى **5GB للوسائط غير مؤكدة الاستلام**؛ توقف/تنبيه لا حذف صامت | disk-full chaos test |
| التخزين المركزي | الملفات على filesystem، البيانات الوصفية في DB، retention لكل نوع | purge jobs + audit trail |
| التحديث | مركزي، حزم موقَّعة، خارج أوقات عمل كل جهاز، rollback | tamper/bad-signature/failed-upgrade tests |
| المكالمات | لا ميكروفون/كاميرا لمكالمات الغير دون تصريح؛ استثناء الشاشة لا يُفترض مضمونًا | privacy fail-closed tests |

### 1.3 التمييز بين الأدلة وحالة العمل

نستعمل تصنيفات الدستور المقترح: `[SOURCE-CAPABILITY]` (قدرة ظاهرة بالكود)، `[RUNTIME-VERIFIED]` (نتيجة تشغيل موثّقة)، `[PROPOSED]`، `[UNKNOWN]`. **لا نكتب `[EXISTING]` للدلالة على جاهزية إنتاجية من مجرد نجاح Unit Test**. جميع أرقام الأداء حتى القياس `[UNKNOWN]`.

**خط الأساس المحلي من تقرير الوكيل، يحتاج إعادة تحقّق قبل كل تغيير:**

- نسخة Remotely المفحوصة: `ec0d45d8f83ee512e60306ce4ddefed6509bd903` بتاريخ 2024-12-17؛ لا نفترض أن آخر `master` في GitHub يساوي تلك النسخة. يجب فحص آخر commits/tags وforks والصيانة والـadvisories قبل تبنّي أي كود لاحق.
- `net8.0` أساس الكود، وGitHub Actions يثبت SDK 7 (خطر NETSDK1045)، بينما مشروع اختبارات Windows غير مدرج في الحل؛ مسار تطوير Viewer يشير إلى مكتبة JS بمسار مختلف.
- `ViewOnly` في المصدر المفحوص UI flag، ولا تفرضه بوابة server relay ولا device handler، و`HasControl` بقيمة ابتدائية true؛ وتوجد مسارات API وجلسات تغيير شاشة لا تحفظ قيد View Only.
- `UpdaterWin` ينفذ سكربت تحديث بلا تحقق توقيع كافٍ كما ورد في التقرير؛ `FileTransferServiceWin` معرض لمسارات ملفات غير آمنة وصلاحيات واسعة؛ وIPC عبر Named Pipe يحتاج تصديقًا وعزلًا.
- `AppLauncherWin` يستخدم token مشتقًا من `winlogon`: العملية التفاعلية للدعم قد تعمل كـSYSTEM داخل جلسة الموظف؛ **جلسة Windows لا تحدد الهوية**، ولا بد من إثباتها Runtime قبل البناء عليها.
- `ScreenRecorderLib` في الـclone المفحوص C++/CLI mixed mode مع .NET Framework 4.8/Native وتغليف x64، لا نفترض نجاح التحميل داخل .NET 10؛ و`IsVideoCaptureEnabled=false` ليس ضمانة أن الناتج صوت-only خفيف.
- `Server/Program.cs` يحد رسالة SignalR الواحدة بنحو 100,000 بايت: **هذا الحد لا يعني أن HTTP streaming محدود بنفس القيمة**. لا نعتمد SignalR لنقل ملفات الوسائط الضخمة.

### 1.4 قرارات نهائية وقرارات غير محسومة

**معتمد تصميميًا:** `SPEC-001` = تأمين View Only؛ `SPEC-002` = قاعدة دمج Capture Helper؛ `ICaptureEngine`؛ عملية مساعدة معزولة في نفس الحزمة؛ server authorization لكل نوع بث؛ `At-least-once + idempotent commit`.

**مفتوح وملزم بإصدار ADR:** أفضل آلية إطلاق user-token، اختيار DB الفعلي، `ScreenRecorderLib` بعد الـPoC أو بديل، آلية استبعاد المكالمات، صيغ التسجيل الصوتي، ربط إصدارات Client/Server، متطلبات OS inventory/ESU، ومدد الاحتفاظ النهائية. **لا تُحوَّل UNKNOWN إلى قرار ضمني**.

---

## 2. مصادر المعرفة وخطوط الحقيقة

| المصدر | الاستخدام | القاعدة |
|---|---|---|
| `Prompts-For-Coding-And-Developers-AR.md` | منهجية الـ52 برومبت من ملف المستخدم | نستخدم أرقامها وعناوينها وأسلوب Role/Inputs/Output/No-guessing |
| `REVERSE-ENGINEERING-REPORT.md` | أدلة الخط الأساس من فحص الوكيل | تعاد مطابقة المسارات والرموز بالـcommit قبل تعديلها |
| `constitution.v2.0.0.proposed.md` | قواعد الجودة والأمان G1–G7 | اعتماد الـfinal المصادق عليه فقط؛ المقترح ليس تنفيذًا |
| `CONSTITUTION-REVIEW.md` و`CONSTITUTION-v2.0.0.diff` | تاريخ القرار والثغرات والتصحيح | تُحفظ النسخ السابقة والمراجعة |
| `AGENTS.md` | تعليمات محلية للوكلاء | مراجعة الأوامر والبيئة، وعدم اتباع معلومة قديمة دون تحقق |
| `Lightweight_Enterprise_Capture_Platform_Architecture_Plan_AR.md` | متطلبات المنصة الأصلية قبل اختيار Remotely | يُستخدم للـtraceability لا كموافقة على إعادة بناء السيرفر |
| GitHub Remotely + ScreenRecorderLib | أحدث مصدر وتراخيص/فروع | pin commit SHA، وسجل فرق من الـbaseline |
| Microsoft .NET / Windows | دورة الدعم الرسمية | تحديث نسخ إطار العمل ونظام التشغيل بعد جرد نسخ Windows بالضبط |
| GitHub Spec Kit docs | سير العمل | في Codex skills تبدأ بـ`$speckit-*` وليس Terminal commands |

### 2.1 حقائق زمنية يجب الانتباه لها (6 نوفمبر 2026)

- ينتهي دعم **.NET 8 في 10 نوفمبر 2026**، و.NET 10 LTS ممتد حتى نوفمبر 2028. *المصدر:* https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/ ؛ https://dotnet.microsoft.com/en-us/platform/support/policy
- دعم .NET 10 على Windows 10 ليس عامًا لكل نسخ Home/Pro القديمة؛ توثيق Microsoft يقصر الدعم على إصدارات Enterprise/LTSC محددة، ويجب مراجعة lifecycle لكل Edition/Build: https://learn.microsoft.com/en-us/dotnet/core/install/windows ؛ https://learn.microsoft.com/en-us/windows/release-health/release-information
- `dotnet package list --vulnerable --include-transitive` على SDK الحديث يحدد حزم NuGet ذات Advisory معروف؛ عدم وجود نتيجة **لا يثبت عدم وجود ثغرات**، ولا يغني عن أدوات SAST ومراجعة الوصول: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list
- توثيق Spec Kit الكامل: https://github.github.com/spec-kit/quickstart ؛ تكامل Codex: https://github.github.io/spec-kit/integrations/codex.html

---

## 3. المعمارية المستهدفة (Target Architecture)

```text
               Administrator (authenticated, least privilege)
                             │ HTTPS / MFA
           ┌─────────────────▼───────────────────────┐
           │ Modified Remotely Server (Windows Server)│
           │  Blazor UI | Identity | Device Registry   │
           │  Remote Viewer (ViewOnly/FullControl)     │
           │  Session Media Permission Controller      │
           │  Capture Policies/Scheduler              │
           │  Upload REST API (resumable/receipts)     │
           │  Media Library + Retention + Audit        │
           │  Signed Update Catalog + Observability   │
           │    DB = metadata | HDD = media files      │
           └────────────────┬─────────────────────────┘
                 HTTPS + SignalR (control/status)
                  HTTPS streaming (bulk media)
                            │
                ≈50 authorized Windows Clients
                            │
          ┌─────────────────▼───────────────────────┐
          │ Remotely Windows Service  (Session 0)   │
          │ Device Auth | Heartbeat | Policy Cache   │
          │ Persistent Queue/SQLite | Upload/Retry   │
          │ Signed Update Agent | Event/Audit         │
          └─────────────────┬───────────────────────┘
              Authenticated IPC | per-session ACL
          ┌─────────────────▼───────────────────────┐
          │ Capture Helper — employee user token     │
          │ No elevation | interactive session only  │
          │ Consent UI + Tray + Local Pause          │
          │ ICaptureEngine → ScreenRecorderLib PoC   │
          │ Screenshots/ScreenVideo/Mic/Webcam        │
          │ Live screen/system-audio/mic/webcam       │
          │ Scheduler job execution + state machine  │
          └─────────────────────────────────────────┘
          Existing Remotely Desktop support process remains separate.
```

**مبادئ الفواصل:** (1) الـServer لا يحتاج ScreenRecorderLib؛ (2) لا يلتقط الـService شيئًا في Session 0؛ (3) UI اختيار المصدر لا يُنشئ حقًّا بل **طلبًا** يخضع لـpolicy/consent/permission؛ (4) التوقف والرفض جزء من العقد؛ (5) system loopback والميكروفون **مصدران مختلفان**؛ (6) View Only يمنع أي DTO تحكم، حتى لو UI تعرّض للتلاعب؛ (7) نقل الـBulk عبر HTTP API وتستخدم SignalR أو القناة القائمة للـcontrol/ack فقط؛ (8) لا تتسبب crash في capture بإنهاء جلسة دعم Remotely.

### 3.1 العقود/الوحدات المقترحة (تُثبَّت بعد ADR)

- `ISessionAccessPolicy`: سياسات `ViewOnly`, `FullControl` والتفويض server/device وAPI؛ server owns `AccessMode`، لا browser.
- `ILiveMediaPermissionService`: لكل جلسة ولكل مصدر من `Screen.View`, `SystemAudio.Listen`, `Microphone.Listen`, `Webcam.View`، مع state `Requested/PendingConsent/Starting/Active/Stopping/Stopped/Denied/Error`.
- `ICaptureEngine`: بدء وإيقاف والتقاط صورة/فيديو وإتاحة الأحداث، دون ربط scheduler أو transport بـScreenRecorderLib مباشرة.
- `ICaptureConsentStore`: فصل إذن الشاشة والميكروفون والكاميرا، وإذن مستقل لصوت الكمبيوتر عند الحاجة؛ سحب الإذن يوقف الاستخدام فورًا.
- `ICaptureScheduleEvaluator`: منفذ زمني يراعي UTC للجدولة/منطقة الجهاز/الـDST وساعات العمل والأولويات وحالة الجلسة.
- `IUploadQueue` + `IUploadReceiptValidator`: استئناف وتأكيد واستعادة بعد reboot والتوقف عند 5GB وعدم حذف غير المرفوع.
- `IMediaRepository` + `IRetentionPolicy`: metadata والـfilesystem منفصلان، إدارة الصلاحيات والحذف المؤكد.
- `IUpdateVerifier`: hash + signature + trusted publisher + rollback manifest، لا تنفيذ سكربت غير موثوق.

### 3.2 بيانات مقترحة (ليست جداول موجودة)

`CapturePolicy`, `CaptureSchedule`, `CaptureJob`, `CaptureArtifact`, `CaptureConsent`, `MediaUploadSession`, `LiveSessionMediaGrant`, `ClientHealth`, `UpdateManifest`, `AuditEvent`؛ لكن أي تصميم بيانات يجب أن يمر عبر ADR واختبارات توافق مزودي EF الثلاثة في Remotely، أو تعديل موثق للدستور إذا تقرر دعم Provider واحد للـfork.

### 3.3 حدود الـLive Viewer — مطلب أخير من المحادثة

الواجهة تعرض **Checkbox/Toggle مستقل لكل مصدر داخل نفس الجلسة**، وليس Radio / Modes حصرية:

| اختيار داخل جلسة View Only | السلوك عند التفعيل | عند الإلغاء | إذن إضافي؟ |
|---|---|---|---|
| `Screen` | يبدأ بث الشاشة/الشاشتين المحددتين | يوقف بث الشاشة فقط | حق مشاهدة الجلسة وسياسة الشاشة |
| `System Audio` | يبدأ WASAPI loopback من خرج الكمبيوتر | يوقف system loopback فقط | إذن Audio.Live مستقل + حظر مكالمات غير مصرّح بها |
| `Microphone` | يبدأ بث ميكروفون الجهاز | يوقف mic فورًا | إذن ميكروفون خاص، واضح وقابل للإلغاء |
| `Webcam` | يبدأ بث كاميرا الويب بجانب الشاشة/أو منفردة | يوقف الكاميرا فورًا | إذن كاميرا خاص، واضح وقابل للإلغاء |

- يمكن اختيار مصدر أو عدة مصادر معًا، ويمكن إلغاء الشاشة مع استمرار الكاميرا *فقط إن كان للمصدر تصريح مستقل*.
- فتح View Only **لا يشغّل** الميكروفون والكاميرا تلقائيًا. الافتراضي الآمن هو عدم تشغيل أي مصدر حساس دون طلب؛ ويمكن تشغيل الشاشة افتراضيًا فقط بعد تحقق صلاحية الجلسة وموافقة الشاشة المعتمدة.
- عرض معاينة كاميرا داخل نافذة على شاشة الموظف قد يظهر في `Screen` حتى لو بث الكاميرا المباشر مغلق؛ السياسة لا تضمن إزالة أي محتوى مرئي داخل سطح المكتب. لا نعد المستخدم باستبعاد الاجتماعات لمجرد وجود `SetExcludeFromCapture`.
- إلغاء Checkbox يوقف المصدر على الجهاز ويعيد ACK؛ لا يكفي تغيير حالة UI. تعديل متصفح غير موثوق أو إعادة محاولة replay لا يمنح حقًا.
- `ViewOnly` مستقل عن الوسائط. `FullControl` طلب امتياز أعلى ويحتاج تفويضًا منفصلًا؛ تفعيل صوت أو كاميرا **لا** يغيّر `AccessMode`.

---

## 4. قواعد تشغيل الوكلاء وSpec Kit

### 4.1 لا تنفيذ بلا Gate

1. كل مهمة صغيرة في فرع منفصل، مع rollback وscope محدد ومرجع SPEC/ADR واصدار/commit الأساس.
2. **أولًا READ/TRACE** ثم اختبار يثبت السلوك أو الخلل، ثم تعديل محدود، ثم اختبارات negative/regression/CI، ثم مراجعة diff مستقلة؛ لا تخلط تحديث 30 تبعية مع إصلاح View Only في PR واحد.
3. أي ادعاء: `SOURCE-CAPABILITY`, `RUNTIME-VERIFIED`, `PROPOSED`, `UNKNOWN` + path/symbol/command/log.
4. التغييرات على CSS/TS تستلزم إثبات أن JS المولّد يُسلّم بالفعل؛ تقارير البنك/الكاميرا/الميكروفون لا تتضمن أي وسائط حقيقية غير لازمة داخل CI.
5. ممنوع تشغيل Capture مستتر أو تخطي صلاحيات Windows أو كسر مؤشر الخصوصية؛ test agents على بيئة تجريبية بموافقة.
6. **لا تحوّل remediations إلى features**: P0 security blockers قبل التوسع الوظيفي، مع audit مستقل.
7. كل PR يوضح `Files Changed`, `Evidence`, `Tests Actually Run`, `Tests Not Run`, `Risks`, `Rollback`, `Gate Verdict`.

### 4.2 تسلسل Spec Kit على Codex

`$speckit-constitution` (مرة/عند تعديل المبادئ) → `$speckit-specify` → `$speckit-clarify` → `$speckit-plan` → `$speckit-checklist` → `$speckit-tasks` → `$speckit-analyze` → `$speckit-implement` (فقط بعد تصريح لتنفيذ كل دفعة) → `$speckit-converge`.

في كل Feature: **الـspec = what/why/acceptance فقط**؛ التفاصيل التقنية وملفات الواجهات والـDB والـpaths مكانها `plan.md` و`research.md`. لو لم تكن Skills متاحة بالفعل لا يخترع الوكيل أسماء أو يبادر إلى `init --force`، بل يراجع ما ثُبّت ويقرأ الـSKILL.md المحلية.

**مسارات تنظيمية مقترحة** (يحددها الوكيل طبقًا لـSpec Kit المثبّت):

```text
.specify/memory/constitution.md
specs/001-view-only-authorization-hardening/{spec.md,plan.md,tasks.md,checklists/}
specs/002-remotely-capture-foundation/{spec.md,plan.md,tasks.md,checklists/}
docs/architecture/{ADRs,flows,threat-models,compatibility-matrix}
docs/security/{release-gates,evidence,cve-register}
docs/operations/{runbooks,backup-restore,privacy-policy}
docs/test-evidence/{baseline,ci,benchmarks,pilot}
```

عند إنشاء Features مختلفة، تحقق من الملف النشط `.specify/feature.json` أو سلوك النسخة المثبتة؛ تغيير Git Branch وحده قد لا يغير Feature النشط.

### 4.3 تعريف الـGate (لا إجابات شكلية)

**Pass = دليل يمكن إعادة تشغيله**: مخرجات build/test وبيئة التشغيل وhash/commit وموافقة مراجِع مستقل وثغرات متبقية وتاريخ ومراجع الـrequirements. `Fail` أو `Blocked` أو `Unknown` لا تتحول تلقائيًا إلى Pass.

### 4.4 ترتيب المواصفات الرسمي

| Spec | الاسم | الترتيب |
|---|---|---|
| **SPEC-001** | Remote Access / View Only Authorization Hardening | أول Feature Security Spec |
| **SPEC-002** | Capture Helper Foundation, Employee Token & IPC | بعد إثبات الهوية |
| SPEC-003 | Employee Consent, Privacy Pause & Audit | قبل أي capture |
| SPEC-004 | Per-Device Scheduling and Conflict Policy | مستقلة عن المحرك |
| SPEC-005 | Dual-Monitor Screenshots | أقل Media MVP |
| SPEC-006 | Screen Video + Microphone Audio | بعد اختبار ترميز فعلي |
| SPEC-007 | Standalone Microphone Recording | إلغاء التعارض مع الفيديو |
| SPEC-008 | Webcam Photos and Silent Video | إذن كاميرا مستقل |
| SPEC-009 | Live Viewer Dynamic Media Controls | toggles شاشة/system/mic/webcam |
| SPEC-010 | Resumable Upload & 5GB Queue | HTTPS + receipts |
| SPEC-011 | Server Media Library & Retention | metadata/file system |
| SPEC-012 | Signed Agent Updates & Operational Policies | مستقبلا feature; **ثغرة G2 تُصلح أمنيًا مبكرًا** |
| SPEC-013 | Monitoring, Performance & Pilot Operations | التعميم |

> **ملاحظة مهمة:** P0 الخاص بتحديثات Remotely غير الموقعة وPath Traversal وAPI endpoints ليست ميزات تؤجل حتى SPEC-012. لها وثائق `SEC-BASELINE-*` مستقلة وفروع إصلاح مسبقة؛ SPEC-012 يضيف ميزات جدولة وتوزيع/rollback فوق أساس آمن.

---
## 5. بوابات الأمان الملزمة قبل أي نشر

| البوابة | الحالة عند خط الأساس | دليل النجاح المطلوب |
|---|---|---|
| **G1** — ViewOnly مفروض على Server + device | **غير مجتازة/لم تُختبر** | اختبارات negative forged DTO، session clone، API mode default، لا input applied |
| **G2** — توقيع تحديثات Agent/Helper | **غير مجتازة/لم تُختبر** | رفض tampered zip/script/manifest، chain and publisher verified، rollback |
| **G3** — مسارات ونقل ملفات آمن | **غير مجتازة/لم تُختبر** | رفض `../`/absolute paths، بقاء ملفات حساسة خارج الوجهة، bounded partial state |
| **G4** — كل API/Device action مفوضة | **غير مجتازة/لم تُختبر** | org/device scope، no anonymous endpoint غير مبرر، default deny |
| **G5** — IPC مصادق ومحدد الجلسة | **غير مجتازة/لم تُختبر** | pipe ACL+challenge/session binding؛ لا secrets في argv |
| **G6** — موافقة/إيقاف محلي وPrivacy | **غير مجتازة/لم تُختبر** | separate consent لكل مصدر، clear indicator، revoke stops promptly |
| **G7** — مساعد الالتقاط غير مرفوع | **غير مجتازة/لم تُختبر** | صورة من عملية فعلية `whoami/token SID/session/elevation`، لا SYSTEM |

**حواجز إضافية:** مصادقة `ViewerAuthorizationFilter` fail-closed، سدّ API endpoints غير المصرح بها، no untrusted shell scripts، audit admin actions، نظام تشغيل مدعوم، signed packages، سياسة الخصوصية، قياس performance، backup/restore. جميعها Release Blockers عند ثبوت Criticality.

### سجل ثغرات تأسيسي (من تقرير قراءة المصدر؛ يجب إعادة التحقق)

| ID | المسار المشتبه/المتأثر | العلاج المشروط | المرحلة |
|---|---|---|---|
| SEC-01 | `ViewerHub.SendDtoToClient`, `RemoteControlSession`, `ViewerAuthorizationFilter` | Source-authoritative `AccessMode`, deny input DTO and privileged actions | P03/P05 |
| SEC-02 | `UpdaterWin`، scripts/zip في TEMP | Verified signed manifest/payload + rollback + trust pinning | P03/P06 |
| SEC-03 | `FileTransferServiceWin`, `BuiltinUsersSid`, `_partialTransfers` | Safe paths, ACL, bounded transfer map | P03/P06 |
| SEC-04 | `ClientDownloadsController.GetInstaller`, `DevicesController.Create`, auth default | Endpoint-by-endpoint authorization and tests | P03/P06 |
| SEC-05 | Named Pipes/argv secrets + `ConnectionInfo.json` | per-user ACL, authenticated IPC, secrets handling | P03/P06/P09 |
| SEC-06 | `Win32Interop` SYSTEM interactive process | keep support path frozen; non-elevated user helper | P08/P09 |
| BUILD-01 | GitHub SDK7 + `net8.0`, ignored Windows tests | CI matrix & valid TRX, Windows runner | P02 |
| BUILD-02 | viewer JS/LibMan paths, stale generated JS | Browser smoke + reproducible asset build | P02/P04 |
| BUILD-03 | DBProvider casing bug / multi-provider migrations | Test utility + provider migration matrix | P02/P04/P16 |
| DEP-01 | `net8.0`, SharpDX legacy, native dependencies | .NET10 compatibility & SCA/SBOM | P04/P08 |

---

## 6. خطة التنفيذ مرحلة بمرحلة — Checklists وGo/No-Go

> **المهام غير مؤشرة بشكل متعمّد**؛ تحديد `[x]` لا يجوز إلا بعد حفظ دليل التشغيل/المراجعة في مجلد `docs/test-evidence/`. الترتيب يضع **فهم المعمارية والبناء والتحديثات ومعالجة الثغرات أولًا**، ثم إضافات الوسائط. يمكن العمل التحضيري بالتوازي في فروع لا تُدمج قبل Gate، لكن لا توجد صلاحية للنشر قبل اكتمال الحواجز.

### P00 — تصديق دستور المشروع وإقفال baseline

**المهمة:** تأسيس حوكمة لا تسمح للوكيل بأن يصادق نفسه، وجعل التقارير والقرارات قابلة للتحقق.

**Checklist:**

- [ ] مراجعة وتصحيح النسخة v2.0.0 المقترحة (هوية SYSTEM، تعريف أدلة EXISITING، عبارتي impersonation وD3).
- [ ] تحديد مصدر الموافقة على الدستور وتوثيقها، مع الاحتفاظ بـv1.0.0 وdiff.
- [ ] تسجيل G1–G7 كـOPEN/FAIL إلى أن يكون هناك اختبار فعلي.
- [ ] تسجيل قرار AC4: `AccessMode=ViewOnly` افتراضيًا في API، و`FullControl` بترخيص مستقل، مع migration لعملاء API القدامى.
- [ ] حفظ Snapshot للـGit status وSHA والملفات الأصلية؛ عدم تشغيل `--force` أو الكتابة فوق أصول المستخدم.
- [ ] ربط كل تغيير مستقبلي بوثيقة SPEC/ADR/Gate.

**معيار الخروج (Gate):** Constitution مصادق عليها + Decision Log + Gate Register، بدون كود إنتاج.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/governance/owner-decisions.md`، نسخة v2 المعتمدة، change log، risk register.

**ترابط الـ52 برومبت:** #47, #48, #49, #52.

**تحذير مراجعة:** لا تخلط موافقة تصميمية بمرور بوابات الإنتاج.

**برومبت بدء المرحلة — P00** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P00 — تصديق دستور المشروع وإقفال baseline.
Objective: تأسيس حوكمة لا تسمح للوكيل بأن يصادق نفسه، وجعل التقارير والقرارات قابلة للتحقق.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: Constitution مصادق عليها + Decision Log + Gate Register، بدون كود إنتاج.
STOP at this phase gate.
```

### P01 — إعادة اكتشاف المعمارية وجرد النظام والبيئة

**المهمة:** فهم نسخة Remotely الفعلية الحالية، الفروع، البنية والعمليات وOS قبل أي تحديث.

**Checklist:**

- [ ] التحقق من Git remote/status/commit/tag، ومقارنة نسخة 2024 بـupstream الحالي وأفضل forks/PRs ذات صلة؛ لا تعمل merge تلقائيًا.
- [ ] إنشاء مخطط Service↔Desktop↔Viewer↔Hubs↔DB↔media flows، كل boundary وما يعبره من identity/token.
- [ ] استخراج Inventory: Windows Edition/Build/ESU، arch، عدد الشاشات وDPI، كاميرا/ميكروفون، موافقات Windows، VC++، Media Foundation، RAM/GPU.
- [ ] تثبيت قائمة الحزم/الإصدارات/المشاريع/التراخيص/الـSDK والـRID قبل التعديل.
- [ ] فحص ملفات wwwroot TS/JS وLibMan وMigrations الثلاثة/معمارية EF وServer deployment.
- [ ] تسجيل السلوك المهم للتراجع والاختبارات: ViewOnly، FullControl، سكربتات، تسجيل دخول، disconnect، جهاز بلا مستخدم.
- [ ] رسم شجرة dependency risk and maintainability وعدم اقتراح إعادة بناء كاملة.

**معيار الخروج (Gate):** خريطة وظائف وتدفق بيانات كاملة + فروق نسخ وInventory 50 جهازًا أو نموذج جرد قابل للتنفيذ؛ UNKNOWN مذكور صراحة.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/architecture/as-is.md`, `inventory.csv`, `upstream-diff.md`, `dependency-baseline.json`.

**ترابط الـ52 برومبت:** #01, #02, #03, #04, #05, #06, #47.

**تحذير مراجعة:** لا تفترض أن upstream الحالي آمن أو أحدث إلا بالأدلة.

**برومبت بدء المرحلة — P01** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P01 — إعادة اكتشاف المعمارية وجرد النظام والبيئة.
Objective: فهم نسخة Remotely الفعلية الحالية، الفروع، البنية والعمليات وOS قبل أي تحديث.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: خريطة وظائف وتدفق بيانات كاملة + فروق نسخ وInventory 50 جهازًا أو نموذج جرد قابل للتنفيذ؛ UNKNOWN مذكور صراحة.
STOP at this phase gate.
```

### P02 — بناء خط أساس يعمل واختبارات قابلة للتنفيذ

**المهمة:** إصلاح بيئة البناء والتشغيل والـCI أولًا دون تغيير وظيفي واسع.

**Checklist:**

- [ ] تشغيل restore/build/test على Windows runner ببيئة موثقة؛ حفظ stdout/stderr وTRX، والفشل ليس نجاحًا.
- [ ] إصلاح SDK7/net8 mismatch في GitHub Actions قبل الحديث عن نجاح CI.
- [ ] إضافة Desktop.Win.Tests إلى matrix حقيقية أو وضع تشغيل Windows موثق، ومعالجة `[Ignore]` بحسب الاختبار.
- [ ] تحليل مسار `Viewer.cshtml` لمكتبة MessagePack وتأكيد إصلاحه عبر HTTP/browser network test.
- [ ] فحص pipeline TS→JS/JS.map وLibMan والتحقق أن المنتج النهائي لا يحمّل JS قديمًا.
- [ ] توثيق مشكلة `Update-Database.ps1` واختبارها بدل تغيير مزود DB اعتمادًا على سكربت غير يعمل.
- [ ] تثبيت SDK global.json/lockfile أو آلية restore موثقة بعد اختبار أثرها على solution.
- [ ] إنشاء baseline characterization tests لـViewOnly/FullControl/agent connection قبل تغيير الصلاحيات.

**معيار الخروج (Gate):** Build وCI قابلان لإعادة التشغيل بأدلة Windows + Web Viewer Smoke Test + Legacy behavior snapshot.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/test-evidence/baseline/`, `.github/workflows/`, `tests/characterization/` (المسار الفعلي يراجع).

**ترابط الـ52 برومبت:** #07, #08, #09, #11, #12, #28, #38, #40, #51.

**تحذير مراجعة:** لا تستبدل تحديث SDK بالترقية إلى NET10 في نفس PR الكبير.

**برومبت بدء المرحلة — P02** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P02 — بناء خط أساس يعمل واختبارات قابلة للتنفيذ.
Objective: إصلاح بيئة البناء والتشغيل والـCI أولًا دون تغيير وظيفي واسع.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: Build وCI قابلان لإعادة التشغيل بأدلة Windows + Web Viewer Smoke Test + Legacy behavior snapshot.
STOP at this phase gate.
```

### P03 — تصنيف الثغرات وتجميد النشر والتصحيح العاجل

**المهمة:** إيجاد وعزل مخاطر تنفيذ كود بصلاحيات عالية وتفويض غير محكم قبل إضافة أي قدرة تسجيل.

**Checklist:**

- [ ] عمل Threat Model لـSYSTEM↔Session↔Viewer↔Server↔Upload↔Updater، وتحديد ثغرات P0/P1 بقابلية استغلال وحدود ثقة.
- [ ] تأكيد خلل ViewOnly من source + اختبارات تحكم سلبية غير ضارة في معمل، وإنشاء SPEC-001.
- [ ] فحص unsigned update (`UpdaterWin`)، file traversal/ACL/unbounded partialTransfers، endpoints بلا auth، RemoteControlRequiresAuthentication=false.
- [ ] فحص local named pipes/command-line secrets والملفات الحساسة ذات الوصول الواسع، وإضافة ACLs وتحقيق الهوية.
- [ ] تشخيص التحقق من update signatures، تصميم channel/manifest موثوق، وتعطيل مسار غير موثوق تجريبيًا دون تعطيل الخدمة إنتاجيًا دون موافقة.
- [ ] إجراء SAST وSCA وsecrets scan وحصر CVEs بإصداراتها ومرجع advisory.
- [ ] مراجعة API scope لكل organization/device والحالة الافتراضية وحالات الخطأ fail-closed.
- [ ] وضع Security Fix Order + Repro tests + rollback لكل ثغرة؛ لا deploy قبل G1/G2/G3/G4/G5.

**معيار الخروج (Gate):** جدول ثغرات P0 مصحوب بPoC سلبي واختبار Regression ومخاطر متبقية وقرار احتواء؛ G1–G5 لا تُعلن Passed إلا بعد تنفيذ.

**الملفات/الأدلة المتوقع إنتاجها:** Threat model, `docs/security/risk-register.md`, `SEC-BASELINE-*` specs, evidence.

**ترابط الـ52 برومبت:** #20, #21, #22, #24, #28, #35, #36, #39.

**تحذير مراجعة:** الهدف حماية أجهزة الموظفين، لا كتابة وسائل لتجاوز الصلاحيات أو إخفاء التسجيل.

**برومبت بدء المرحلة — P03** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P03 — تصنيف الثغرات وتجميد النشر والتصحيح العاجل.
Objective: إيجاد وعزل مخاطر تنفيذ كود بصلاحيات عالية وتفويض غير محكم قبل إضافة أي قدرة تسجيل.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: جدول ثغرات P0 مصحوب بPoC سلبي واختبار Regression ومخاطر متبقية وقرار احتواء؛ G1–G5 لا تُعلن Passed إلا بعد تنفيذ.
STOP at this phase gate.
```

### P04 — تحديث المنصة والاعتماديات إلى نسخ مدعومة

**المهمة:** رفع استقرار Remotely المهجور وتقليل مخاطر انتهاء الدعم، مع الحفاظ على صلاحيات ووظائف Remotely.

**Checklist:**

- [ ] اتخاذ قرار نسخة Runtime للـServer والـClients بناء على جرد Windows 10 Pro/Enterprise/LTSC/ESU، مع حصر الأجهزة التي تحتاج ترقية OS.
- [ ] تخطيط ترقية .NET8→.NET10 بPRs صغيرة قابلة للرجوع؛ لا تفترض أن تغيير TargetFramework يكفي.
- [ ] تحديث EF Core/ASP.NET Core/SignalR/Avalonia/SkiaSharp/NAudio والـbuild assets بعد فحص compatibility وAPI changes وتراخيصها.
- [ ] تحقيقات خاصة بـSharpDX (قديم) وSystem.CommandLine beta وTS/LibMan والملفات البنائية القديمة؛ قرارات isolate/replace مبنية على اختبار.
- [ ] إنشاء Dependency BOM/SBOM ومسح NuGet vulnerable/outdated/transitive وتحليل التبعيات native.
- [ ] تطبيق إعادة تثبيت قابلة للإنتاج (lock files/central versions حيث يناسب)، وCI يمنع high/critical vulnerabilities بدون استثناء معتمد.
- [ ] إجراء ترحيل Migration منقح على نسخ DB الثلاثة إن بقيت مدعومة، مع backups/rollback.
- [ ] تحقق استمرارية Viewer/Agent/remote control على إصدار .NET المستهدف ولا تمرر كسر binary compatibility.

**معيار الخروج (Gate):** Supported patch release + reproducible builds + security scan + compatibility matrix + smoke tests على OS المسموح.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/upgrades/net10-plan.md`, `SBOM`, `ADRs`, test evidence.

**ترابط الـ52 برومبت:** #15, #16, #23, #24, #28, #29, #31, #33, #10.

**تحذير مراجعة:** Windows 10 Pro 22H2 خارج الدعم الأساسي؛ runtime update لا يعالج OS unsupported.

**برومبت بدء المرحلة — P04** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P04 — تحديث المنصة والاعتماديات إلى نسخ مدعومة.
Objective: رفع استقرار Remotely المهجور وتقليل مخاطر انتهاء الدعم، مع الحفاظ على صلاحيات ووظائف Remotely.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: Supported patch release + reproducible builds + security scan + compatibility matrix + smoke tests على OS المسموح.
STOP at this phase gate.
```

### P05 — SPEC-001: فرض View Only وتفويض الجلسات

**المهمة:** تحويل ViewOnly من حارس UI إلى حد صلاحيات مفروض على السيرفر والجهاز، مع حماية FullControl.

**Checklist:**

- [ ] إنشاء SPEC-001 بـ`$speckit-specify` ثم clarify/plan/tasks/analyze؛ تعريف أدوار وسياسات `AccessMode=ViewOnly|FullControl`.
- [ ] Session AccessMode مملوك للسيرفر؛ الصفر/غياب mode للـAPI يعني ViewOnly، وFullControl يحتاج صريح+تفويض مستقل.
- [ ] رفض كل Input DTO وCtrlAltDel وChangeWindowsSession وأي file/clipboard transfer ضمن ViewOnly إلا صلاحية مستقلة ومعلنة.
- [ ] إيصال capability cryptographically or authenticated bound للجهاز، وتفعيل `HasControl` كدفاع ثانٍ؛ لا الاعتماد على UI query flag.
- [ ] تثبيت mode في clone/change session/reconnect/old API path، ومنع downgrade/upgrade client-side.
- [ ] negative integration tests من API/SignalR forged client, replay, conflicting sessions, unauthorized role.
- [ ] فحص remote-control legacy FullControl regression وattended/OTP/organization scoping.
- [ ] ربط G1/G4 مع أدلة اختبارات حقيقية وتجربة على Windows قبل اعتبار المسار جاهزًا.

**معيار الخروج (Gate):** AC1-AC8 المعتمدة في سجل قرارات SPEC-001 + refusals server/device + CI actual tests.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/001-*/{spec,plan,tasks}`, `docs/security/view-only-matrix.md`.

**ترابط الـ52 برومبت:** #03, #14, #17, #18, #20, #21, #22, #35, #37, #39.

**تحذير مراجعة:** تمييز حق الجلسة عن اختيارات Live Media؛ وسيط المتصفح غير موثوق.

**برومبت بدء المرحلة — P05** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P05 — SPEC-001: فرض View Only وتفويض الجلسات.
Objective: تحويل ViewOnly من حارس UI إلى حد صلاحيات مفروض على السيرفر والجهاز، مع حماية FullControl.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: AC1-AC8 المعتمدة في سجل قرارات SPEC-001 + refusals server/device + CI actual tests.
STOP at this phase gate.
```

### P06 — تقوية باقي طبقة Remotely الأمنية والامتيازية

**المهمة:** إغلاق G2/G3/G4/G5 وإصلاح أسرار ورفع ملفات وتحديثات دون هدم remote support.

**Checklist:**

- [ ] تطبيق حزم تحديث موثّقة/موقعة والتحقق قبل تنفيذ سكربت؛ إصدار تحديثات متسلسل وفشل مغلق.
- [ ] فرض safe rooted paths وعدم استخدام remote filename كمسار، وتحجيم partial transfer state وACLs.
- [ ] تقوية device enroll/API/auth defaults وOrganization/device access وViewerAuthorizationFilter.
- [ ] تخزين أسرار Agent بصورة تحترم ACLs، وتدوير مفاتيح عند التسرب وعدم تمرير secrets في argv/Logs.
- [ ] Design قناة service-helper authenticated IPC مع session binding وanti-replay ورسائل محدودة الصلاحيات.
- [ ] اختبارات خصوم سلبيّة: tampered update، traversal strings، unauthenticated API، pipe spoofing، forced reconnect.
- [ ] تسجيل audit للأوامر الإدارية وعمليات النظام ووقف الطرق القديمة غير الموثوقة دون تغييرات صامتة.

**معيار الخروج (Gate):** G2–G5 فعليًا Passed مع أدلة اختبار وتوثيق rollout/rollback؛ سياسة FullControl مستقلة.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/security/remediation-PRs.md`, signed updater threat tests, API auth policy, pipe tests.

**ترابط الـ52 برومبت:** #07, #11, #20, #22, #24, #27, #35, #36, #40.

**تحذير مراجعة:** لا يحق تشغيل feature recorder على أجهزة حقيقية قبل إغلاق البوابات الحرجة.

**برومبت بدء المرحلة — P06** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P06 — تقوية باقي طبقة Remotely الأمنية والامتيازية.
Objective: إغلاق G2/G3/G4/G5 وإصلاح أسرار ورفع ملفات وتحديثات دون هدم remote support.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: G2–G5 فعليًا Passed مع أدلة اختبار وتوثيق rollout/rollback؛ سياسة FullControl مستقلة.
STOP at this phase gate.
```

### P07 — حدود التوسعة وتنظيف الكود دون Rewrite

**المهمة:** تهيئة بنية وحدات جديدة مع أقل تغييرات ممكنة في ريموتلي الحالي.

**Checklist:**

- [ ] تثبيت boundaries: Agent Service vs Desktop Support SYSTEM vs User Capture Helper vs Server Modules.
- [ ] مراجعة `Desktop.Core` vs `Desktop.Shared` بالأدلة قبل إزالة code dead، وعدم إزالة أي مسار يمكن استخدامه في builds أخرى.
- [ ] تصميم Contracts/DTOs versioned والـDI والتعامل مع lifecycle/stale state من غير Service Locator جديد.
- [ ] إنشاء ADR لاختيار DB provider والتزام migrations الثلاث أو تعديل دستوري مصادق.
- [ ] تحديد x64 deployment footprint والـNative binaries وموضع shared abstractions.
- [ ] وضع سياسات version compatibility بين server/agent/helper/API/streams.
- [ ] إضافة tests لعقود DTO والسياسات ضد regressions قبل نقل الملفات أو extract modules.

**معيار الخروج (Gate):** ADRs مقبولة + modular dependency map + stable extension points، بدون نثر كود داخل God Classes.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/architecture/target.md`, `docs/architecture/ADRs/`, DTO contract tests.

**ترابط الـ52 برومبت:** #01, #02, #15, #16, #29, #30, #31, #47.

**تحذير مراجعة:** لا تضف Microservices/Queues معقدة بلا قياس حاجة.

**برومبت بدء المرحلة — P07** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P07 — حدود التوسعة وتنظيف الكود دون Rewrite.
Objective: تهيئة بنية وحدات جديدة مع أقل تغييرات ممكنة في ريموتلي الحالي.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: ADRs مقبولة + modular dependency map + stable extension points، بدون نثر كود داخل God Classes.
STOP at this phase gate.
```

### P08 — PoC للهوية والعزل وScreenRecorderLib

**المهمة:** إزالة أكبر UNKNOWN قبل دمج native capture في عميل الإنتاج.

**Checklist:**

- [ ] عمل تجربة user token على Windows بأجهزة تجريبية: Token/User SID/SessionID/elevation/windowstation/desktop/process tree، عند Logon/Logout/Lock/UAC.
- [ ] تحديد طريقة launch/helper non-elevated والـIPC والقدرة على استمرار service بعد logout بدون capture.
- [ ] تجربة build/load ScreenRecorderLib C++/CLI على .NET target وx64، وتحديد مسار `/clr:netcore` إذا لزم واختبار رخصة/redistributables.
- [ ] تجربة شاشتين 1080p مختلفة DPI ومعاينة snapshots وMP4 ومدد قصيرة/مختلفة.
- [ ] تجربة ميكروفون فقط: ملف audio حقيقي أم MP4 مع black video؟ مقارنة تكلفة التخزين؛ System Audio منفصل.
- [ ] كاميرا snapshot + silent video + media track verification + permissions handling.
- [ ] اختبار hardware/software encoding والتعطل والاستئناف، وقياس idle/active CPU/RAM/GPU/disk على أضعف جهاز.
- [ ] إثبات فشل المحرك أو نجاحه بـGo/No-Go مسجَّل، وليس وعود API المصدر.

**معيار الخروج (Gate):** ADR اعتماد/رفض ScreenRecorderLib، PoC logs/metrics + identity proof + native packaging decision.

**الملفات/الأدلة المتوقع إنتاجها:** `docs/poc/capture-engine/`, `docs/poc/identity/`, `ADRs`.

**ترابط الـ52 برومبت:** #02, #08, #09, #12, #15, #16, #23, #24, #32, #36, #34.

**تحذير مراجعة:** لا تنفذ on-disk surveillance أو إذن افتراضي أثناء PoC؛ استخدام بيانات تمثيلية ومشاركين موافقين.

**برومبت بدء المرحلة — P08** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P08 — PoC للهوية والعزل وScreenRecorderLib.
Objective: إزالة أكبر UNKNOWN قبل دمج native capture في عميل الإنتاج.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: ADR اعتماد/رفض ScreenRecorderLib، PoC logs/metrics + identity proof + native packaging decision.
STOP at this phase gate.
```

### P09 — SPEC-002: نواة Capture Helper والتثبيت والاتصال

**المهمة:** بناء أصغر مسار عمل من سيرفر إلى جهاز إلى لقطة واحدة بموافقة واضحة.

**Checklist:**

- [ ] توصيف SPEC-002 عبر Spec Kit: Service & Helper boundaries، trigger/IPC، consent، event model، timeout/cancel.
- [ ] عميل واحد مثبت: Service دائم، Helper ضمن جلسة المستخدم non-elevated، ليس SYSTEM، والتعطل لا يوقف Remotely support.
- [ ] الـhelper يعمل فقط مع session/user صحيح؛ no-user/locked/secure desktop/logoff حالات محددة للفشل الآمن.
- [ ] Authenticated IPC مع ACL للـSID المستهدف وحماية replay/cross-session وبروتوكول برسائل versioned.
- [ ] طابور محلي persistent يستعيد state بعد reboot من غير فقد موافقات أو حذف تلقائي.
- [ ] End-to-end لقطة شاشة واحدة بطلب مصرح + UUID/hash + رفع آمن + ACK + يظهر في لوحة الأدمن.
- [ ] قياس Idle footprint والانحراف عن Remotely baseline، مع installer واحد وتوقيع الحزمة.

**معيار الخروج (Gate):** End-to-end golden path + negative paths على Windows + لا تنفذ Session0 capture + لا restart للـsupport.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/002-*/`, helper prototype, IPC contract tests, smoke evidence.

**ترابط الـ52 برومبت:** #03, #14, #16, #17, #18, #20, #35, #39.

**تحذير مراجعة:** لا تبرر صلاحيات SYSTEM لأن Remotely Desktop الحالي يعمل بها.

**برومبت بدء المرحلة — P09** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P09 — SPEC-002: نواة Capture Helper والتثبيت والاتصال.
Objective: بناء أصغر مسار عمل من سيرفر إلى جهاز إلى لقطة واحدة بموافقة واضحة.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: End-to-end golden path + negative paths على Windows + لا تنفذ Session0 capture + لا restart للـsupport.
STOP at this phase gate.
```

### P10 — الموافقة الموثقة وPrivacy Pause وسياسات المكالمات

**المهمة:** جعل الإذن منفصلًا لكل قدرة والتوقف الآمن شرطًا قبل التفعيل.

**Checklist:**

- [ ] تصميم consent lifecycle للموظف (grant/revoke/expiry/reauth) عبر session user identity، قابل للتحقق من الخادم.
- [ ] شاشة إعلام مبدئية واضحة وإشعار tray دائم أثناء live/scheduled capture، مع privacy pause محلي.
- [ ] فصل أنواع الإذن: Screen, SystemAudio, Microphone, Webcam، وLive مقابل Scheduled حسب سياسة قانونية معتمدة.
- [ ] رفض أوامر البث عند عدم وجود جلسة أو هوية أو Consent أو عند اختيار Pause.
- [ ] فحص الاحتمالات القانونية/الخصوصية بحسب مكان التشغيل، خاصة أصوات المشاركين في المكالمات ومحتوى الشاشة.
- [ ] سياسة Calls fail closed: لا assume كشف Zoom/Telegram/Skype شامل؛ SetExcludeFromCapture/Window titles ليست ضمانة.
- [ ] حالات الإلغاء عند تغيير المستخدم أو logout أو permissions OS وإعادة الفتح/التوصيل؛ لا حفظ مفاتيح موافقة بشكل يمكن لSYSTEM أن يزوره.
- [ ] اختبارات privacy interruption لحظيًا ولـremote viewer + scheduled jobs.

**معيار الخروج (Gate):** G6 Passed في سيناريوهات Consent/Revocation/Local Pause، دون اعتماد على UI فقط.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/003-*/`, consent matrix, privacy threat model, staff UX.

**ترابط الـ52 برومبت:** #14, #20, #22, #35, #36, #39.

**تحذير مراجعة:** لا تقبل عبارة «الموظف وافق وقت التثبيت إذن كل شيء مسموح دائمًا».

**برومبت بدء المرحلة — P10** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P10 — الموافقة الموثقة وPrivacy Pause وسياسات المكالمات.
Objective: جعل الإذن منفصلًا لكل قدرة والتوقف الآمن شرطًا قبل التفعيل.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: G6 Passed في سيناريوهات Consent/Revocation/Local Pause، دون اعتماد على UI فقط.
STOP at this phase gate.
```

### P11 — الجدولة لكل جهاز وتعارض التسجيلات

**المهمة:** تطوير scheduler مستقل عن محرك الالتقاط يدير ساعات العمل والقيود والموارد.

**Checklist:**

- [ ] سياسات منفصلة لكل جهاز ولكل نوع مع interval/duration/start/end/timezone/disabled/priority.
- [ ] Work-hour windows + holidays/overnight shifts/timezone/DST/clock skew + no-session behavior.
- [ ] تحكم مركزي مع cached version + monotonic scheduled IDs وnonce لمكافحة تكرار jobs عند reconnect.
- [ ] حالة Screen Video + Mic collision: فيديو الشاشة يأخذ الميكروفون؛ mic-only تلغى مع Reason recorded.
- [ ] تحجيم مهام الكاميرا والصوت وقواعد concurrency لمنع تنازع device resource.
- [ ] مراقبة الجداول وعرض NextRun/LastRun/SkipReason وأسباب رفض الخصوصية.
- [ ] اختبار إيقاف الجدول عند 5GB أو فقد Consent، والاستئناف عند رجوع الشبكة بعد معالجة السعة.

**معيار الخروج (Gate):** Policy evaluator deterministic باختبارات DST/collisions/reboot/missed jobs، وإقرار كل Skip.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/004-*/`, Schedule DTO, deterministic test vectors.

**ترابط الـ52 برومبت:** #14, #18, #35, #36, #37, #39, #43, #19.

**تحذير مراجعة:** لا تستخدم System.Timer في عدة أماكن بلا source of truth أو idempotent job ID.

**برومبت بدء المرحلة — P11** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P11 — الجدولة لكل جهاز وتعارض التسجيلات.
Objective: تطوير scheduler مستقل عن محرك الالتقاط يدير ساعات العمل والقيود والموارد.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: Policy evaluator deterministic باختبارات DST/collisions/reboot/missed jobs، وإقرار كل Skip.
STOP at this phase gate.
```

### P12 — صور الشاشتين — أول نوع وسائط

**المهمة:** تطبيق Screenshot engine مستقر منخفض استهلاك الموارد.

**Checklist:**

- [ ] تحديد display selection (Screen1, Screen2, Both) والمقاسات/الـDPI/الـcolor spaces وترتيب لقطات.
- [ ] استعمال Capture Engine المعتمد ومعالجة الصور atomic writes مع timestamps/UUID ودون تعليق UI.
- [ ] قياس الاستهلاك عند idle/shot والوقت/الملفات على Intel Gen5/8GB.
- [ ] تأكيد إخفاء/تجاوز secure desktop والمكالمات الحساسة عند عدم ضمان الاستثناء.
- [ ] التزام Consent وبداية/نهاية عمل كل جهاز، وPause والتخزين المحلي.
- [ ] اختبارات no-monitor/changed-monitor/remote-session/lock/logoff/error/corruption.

**معيار الخروج (Gate):** صورة واضحة لكل شاشة مع DeviceId/ScreenId، عدم التصوير في حالات المنع، أدلة الأداء.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/005-*/`, screenshot tests and media samples fabricated.

**ترابط الـ52 برومبت:** #08, #18, #23, #32, #35, #36.

**تحذير مراجعة:** لا تعيد استخدام DXGI viewer buffer دون تحقق lifetime/thread safety.

**برومبت بدء المرحلة — P12** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P12 — صور الشاشتين — أول نوع وسائط.
Objective: تطبيق Screenshot engine مستقر منخفض استهلاك الموارد.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: صورة واضحة لكل شاشة مع DeviceId/ScreenId، عدم التصوير في حالات المنع، أدلة الأداء.
STOP at this phase gate.
```

### P13 — فيديو الشاشة مع الميكروفون + صوت مستقل

**المهمة:** إضافة تسجيلات مقننة مع فصل system audio والميكروفون وحماية ملفات جزئية.

**Checklist:**

- [ ] فيديو الشاشة: source = selected screen(s) + microphone capture (ليس loopback)؛ اختبر absence speaker track.
- [ ] التسجيل الميكروفوني المستقل: audio-only playback/container والمدة الفعلية؛ لا تفترض أنه خفيف من black MP4.
- [ ] تجربة H264/MP4 fragmented/segments مع restart/abrupt shutdown/crash وتحقق مدة الملف.
- [ ] تقييد bitrate/fps/resolution وفق profile الجهاز مع Hardware Encoder fallback.
- [ ] اختبار تداخل mic-only مع video+mic والتأكد أن mic-only ملغى وليس مخفيًا وراء lock.
- [ ] Privacy Pause/revocation/call collision يوقف/يلغي ويدون الأسباب، ولا يخزن كلام مكالمة غير مصرح.
- [ ] تحليل تأخر الانتهاء وملفات قد لا يفتحها مشغل، مع media probes.

**معيار الخروج (Gate):** SPEC-006 وSPEC-007 والـAC لها، ملفات صوت/فيديو قابلة للفتح، no loopback leakage، crash-safe.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/006-*/`, `specs/007-*/`, A/V PoC evidence.

**ترابط الـ52 برومبت:** #08, #09, #11, #18, #23, #32, #36, #39.

**تحذير مراجعة:** Mic-only قد يحتاج Engine مستقل للصوت خفيف لكن لا تضف اعتمادية دون PoC/ADR.

**برومبت بدء المرحلة — P13** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P13 — فيديو الشاشة مع الميكروفون + صوت مستقل.
Objective: إضافة تسجيلات مقننة مع فصل system audio والميكروفون وحماية ملفات جزئية.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: SPEC-006 وSPEC-007 والـAC لها، ملفات صوت/فيديو قابلة للفتح، no loopback leakage، crash-safe.
STOP at this phase gate.
```

### P14 — الكاميرا: صور + فيديو صامت

**المهمة:** تحقيق أعلى حساسية للخصوصية بشكل مستقل عن صلاحية الشاشة.

**Checklist:**

- [ ] كاميرا صورة أو silent video بجدول مستقل، إذن منفصل وindicator لا يمكن أن تخفيه UI.
- [ ] التأكد من منع audio track في silent webcam recordings بفحص container.
- [ ] معالجة كاميرا غير موجودة/مستخدمة من تطبيق آخر/permissions denied/device unplugged.
- [ ] اختبار اختلاف device formats وFPS وframe orientation ومتطلبات MF على Windows N/KN.
- [ ] لا تستخدم camera إذا الموظف أوقف أو بدّل user/session؛ وتعرض deny بدل retry غير محدود.
- [ ] اختبار CPU/RAM/disk والـviewonly parallel + revocation أثناء التشغيل.

**معيار الخروج (Gate):** SPEC-008 Passed مع camera+audio-absent checks، privacy indications، no-user refusal.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/008-*/`, webcam matrix, test clips synthetic.

**ترابط الـ52 برومبت:** #14, #18, #20, #23, #35, #36, #39.

**تحذير مراجعة:** لا تفعّل الكاميرا تلقائيًا بدل فشل مصدر آخر.

**برومبت بدء المرحلة — P14** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P14 — الكاميرا: صور + فيديو صامت.
Objective: تحقيق أعلى حساسية للخصوصية بشكل مستقل عن صلاحية الشاشة.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: SPEC-008 Passed مع camera+audio-absent checks، privacy indications، no-user refusal.
STOP at this phase gate.
```

### P15 — بث View Only الحي بمصادر مستقلة قابلة للتفعيل

**المهمة:** إضافة Checkboxes داخل جلسة مشاهدة واحدة وليس Modes جامدة.

**Checklist:**

- [ ] UI checkbox مستقل لـScreen/SystemAudio/Microphone/Webcam، ويمكن تشغيل عدة مصادر أو بعضها فقط.
- [ ] حالات state واضحة لكل مصدر: requested/pending consent/starting/active/stopping/error/denied؛ Retry منضبط.
- [ ] الإجراء من UI يمر عبر server policy ثم source-specific consent ثم device ACK؛ لا تفترض Active عند الضغط فقط.
- [ ] حماية كل live stream بتفويض session-specific، وViewOnly يظل يمنع كل control DTOs.
- [ ] زر إلغاء المصدر يوقفه فورًا في الجهاز ويسجل الحدث؛ إلغاء شاشة لا يلغي الكاميرا المصرّح بها.
- [ ] SystemAudio loopback منفصل عن Mic capture؛ كل منهما قابل للإلغاء؛ منع صوت المكالمات غير المصرح بها.
- [ ] Webcam Picture-in-Picture أو لوحة جانبية وحالات جودة الشبكة؛ بدون صوت بالوضع الافتراضي.
- [ ] اختبار التزامن والتكلفة والأمان والهجوم عبر devtools/API, multiple viewers, revoke, reconnect.

**معيار الخروج (Gate):** SPEC-009 + independent toggles + negative security tests + actual source start/stop ACKs.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/009-*/`, frontend TS/JS tests, live session metrics.

**ترابط الـ52 برومبت:** #14, #17, #18, #20, #22, #23, #35, #36, #39, #49.

**تحذير مراجعة:** خارج scope المشروع تغيير ViewOnly إلى جهاز يتحكم بالميكروفون والكاميرا من غير موافقة.

**برومبت بدء المرحلة — P15** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P15 — بث View Only الحي بمصادر مستقلة قابلة للتفعيل.
Objective: إضافة Checkboxes داخل جلسة مشاهدة واحدة وليس Modes جامدة.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: SPEC-009 + independent toggles + negative security tests + actual source start/stop ACKs.
STOP at this phase gate.
```

### P16 — رفع الوسائط والسيرفر وRetention ولوحة الإدارة

**المهمة:** جعل البيانات موثوقة ومحكومة على HDD محلي، بعيدًا عن قاعدة البيانات والـSignalR.

**Checklist:**

- [ ] HTTPS resumable/chunked bulk upload مع `CaptureId`/part hashes/final SHA256 وACK idempotent.
- [ ] حفظ staging files وrename atomic بعد التحقق؛ فشل التحقق لا يكتب Artifact نهائي.
- [ ] تشفير وحماية queue محلية بحد 5GB للغير مؤكد؛ وقف مهام جديدة عند الامتلاء والتنبيه، لا حذف unacknowledged.
- [ ] حفظ metadata في DB وmedia على filesystem، صلاحية org/device and source type، audit download/delete.
- [ ] UI للمشرف: أجهزة وhealth وسعة، جدول للنوع، media filters، retention type-level، unauthorized access denied.
- [ ] سياسات retention لكل صورة/فيديو/صوت/كاميرا (وممكن override per-device فقط إذا أقِرّ) + منع Purge لملف upload incomplete.
- [ ] استراتيجية backup/restore تتحقق من اتساق DB/files، quota/disk alert، 1TB HDD IOPS وتكلفة thumbnails.
- [ ] اختبارات failure injection: disconnect/reboot/duplicate chunks/fail during commit/DB down/full disk/retention race.

**معيار الخروج (Gate):** SPEC-010/011 وتوافر الملفات بلا duplicates؛ ACL سليم، restore test وDB migration test.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/010-*/`, `specs/011-*/`, database migration set, upload protocol tests.

**ترابط الـ52 برومبت:** #17, #18, #20, #23, #35, #37, #39, #41, #42, #43, #44, #45, #46.

**تحذير مراجعة:** لا تخزن الفيديو داخل `SharedFile.FileContents` أو تستخدم signalR DTO واحد كبير.

**برومبت بدء المرحلة — P16** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P16 — رفع الوسائط والسيرفر وRetention ولوحة الإدارة.
Objective: جعل البيانات موثوقة ومحكومة على HDD محلي، بعيدًا عن قاعدة البيانات والـSignalR.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: SPEC-010/011 وتوافر الملفات بلا duplicates؛ ACL سليم، restore test وDB migration test.
STOP at this phase gate.
```

### P17 — تحديث عميل موقَّع ونشر متدرج وصيانة

**المهمة:** ترقية وظائف التحديث الموجودة إلى إدارة إصدارات مركزية موقَّعة خارج ساعات العمل.

**Checklist:**

- [ ] G2 أمنياً Passed من P06؛ هنا تمديد جدول التحديثات حسب hours/timezone لكل جهاز.
- [ ] توقيع Installer وManifest والسكربتات المستخدمة، hash verification/publisher pinning/replay protection.
- [ ] توافق إصدار Server/Agent/Helper/DB schema والـdowngrade/rollback/restart handling.
- [ ] توزيع حلقات Canary ثم pilot ثم موجة نشر، مع kill switch وrollback.
- [ ] سياسات AD/GPO وstandalone install مع صلاحيات واضحة، توثيق consent والتشغيل بعد reboot/logout.
- [ ] نسخ احتياطية قابلة للاستعادة وتدقيق مؤشرات Crash/latency/storage/Update fail.
- [ ] لا يغيّر التحديث kernel drivers أو أنظمة الحماية ولا يلتف على prompts نظام التشغيل.

**معيار الخروج (Gate):** SPEC-012 + signed update tests + staged rollout/rollback + مواعيد خارج الدوام.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/012-*/`, signed update catalog, deployment runbook.

**ترابط الـ52 برومبت:** #17, #20, #22, #24, #27, #28, #35, #36, #39, #26.

**تحذير مراجعة:** لا تنفذ PowerShell من `%TEMP%` بناءً على تنزيل غير موثق.

**برومبت بدء المرحلة — P17** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P17 — تحديث عميل موقَّع ونشر متدرج وصيانة.
Objective: ترقية وظائف التحديث الموجودة إلى إدارة إصدارات مركزية موقَّعة خارج ساعات العمل.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: SPEC-012 + signed update tests + staged rollout/rollback + مواعيد خارج الدوام.
STOP at this phase gate.
```

### P18 — اختبار شامل، Capacity، Pilot، Go/No-Go

**المهمة:** إثبات الجاهزية التشغيلية والقانونية قبل التعميم.

**Checklist:**

- [ ] تثبيت Gates G1–G7 بأدلة Windows وإثبات عميل user-token وقرارات الموافقة.
- [ ] اختبار التحميل لـ50 جهازًا، حالات الشبكة المنقطعة، تزامن الرفع، وقواعد احتفاظ مختلفة.
- [ ] قياس idle/record/live: CPU/RAM/GPU/disk/network/power وملف تثبيت على أضعف جهاز.
- [ ] اختبار ثبات 24/48/72 ساعة وفق سياسة فريق الاختبار، وتوثيق النتائج دون ادعاء قياس غير منفّذ.
- [ ] اختبار 3–5 أجهزة فعلية داخل نطاق تجريبي بموافقة، مع AD/standalone وويندوز متنوعة.
- [ ] اختبار نسخ احتياطي/استعادة/تحديث rollback ومراقبة وسعة 1TB HDD.
- [ ] مراجعة قانونية وخصوصية خاصة بأصوات أطراف ثالثة وسياسة الموظفين وساعات العمل والحذف.
- [ ] مسح خارجي مستقل لتفويض ViewOnly/FullControl وOWASP/مراجعة أسرار/وصول الملفات.
- [ ] تسليم Runbooks والمراقبة والتصعيد وخطة تحديث دوري للتبعيات من دون انتظار سنوات أخرى.

**معيار الخروج (Gate):** GO فقط عند Gates Passed وOpen Risks accepted by owner؛ وإلا No-Go موثق.

**الملفات/الأدلة المتوقع إنتاجها:** `specs/013-*/`, performance report, pilot report, runbooks, release manifest.

**ترابط الـ52 برومبت:** #13, #21, #22, #23, #25, #27, #28, #32, #38, #40, #49, #50.

**تحذير مراجعة:** مشاكل حرجة unresolved = ممنوع Production حتى لو UI جميل.

**برومبت بدء المرحلة — P18** (ارسل للوكيل ثم استعمل البرومبتات الفردية المرتبطة):

```text
You are a Principal .NET Engineer and Independent Security Reviewer working on a fork of immense/Remotely.
Task: P18 — اختبار شامل، Capacity، Pilot، Go/No-Go.
Objective: إثبات الجاهزية التشغيلية والقانونية قبل التعميم.
Inputs: current git SHA and tree; AGENTS.md; ratified constitution v2; REVERSE-ENGINEERING-REPORT.md; current Spec Kit feature; existing test evidence.
Required evidence: inspect actual source, label every major conclusion SOURCE-CAPABILITY/RUNTIME-VERIFIED/PROPOSED/UNKNOWN, cite file paths and tests.
Execute checklist items only within explicit authorization; propose first, then make one bounded change at a time with tests and rollback. Never deploy/commit/push without permission.
Output (Arabic): Evidence Map; Gap Matrix (P0/P1/P2); Step-by-step tasks; exact diff proposal; tests run/not run; Risk/ADR; Gate Pass/Fail/Unknown; one next action.
Gate that must be satisfied: GO فقط عند Gates Passed وOpen Risks accepted by owner؛ وإلا No-Go موثق.
STOP at this phase gate.
```

---

## 7. مصفوفة تتبع المتطلبات إلى المواصفات والاختبارات

| Req ID | المطلب | المواصفة | اختبار قبول أدنى |
|---|---|---|---|
| R-01 | مشاهدة ViewOnly دون تحكم مهما تغيّر JS/HTTP API | SPEC-001 | Browser devtools + forged DTO denied server/device |
| R-02 | API AccessMode الافتراضي ViewOnly؛ FullControl explicit | SPEC-001 | old API client omission results ViewOnly + migration notice |
| R-03 | Checkboxes مستقلة لمصادر live داخل نفس الجلسة | SPEC-009 | select any subset (2⁴) + no cross-permission |
| R-04 | Screen/Audio loopback/Mic/Webcam live | SPEC-009 | start/stop/ACK/consent/deny لكل source |
| R-05 | Windows service عند boot ورفع بعد logout | SPEC-002/010 | logoff retains queue, upload resumes, capture halted |
| R-06 | CaptureHelper بهوية الموظف no elevation | SPEC-002 | token/SID/session/elevation verified on Windows |
| R-07 | Screenshots لكل شاشة وفق جدولة الجهاز | SPEC-004/005 | mixed DPI/displays, configured intervals |
| R-08 | Screen video مع **ميكروفون فقط** | SPEC-006 | inspect audio source/track and exclude loopback |
| R-09 | Mic-only وإلغاء التعارض لصالح screen video | SPEC-007/004 | deterministic collision log |
| R-10 | webcam photos/silent video مع إذن منفصل | SPEC-008/003 | audio track absent, revoked consent stops |
| R-11 | رفض مكالمات خاصة/Privacy Pause | SPEC-003/009 | unknown call detection results skip/not record |
| R-12 | Reconnect/upload resumable/two monitors/queue 5GB | SPEC-010 | disconnect, duplicate, reboot, exact quota |
| R-13 | Media filesystem + DB metadata + per-type retention | SPEC-011 | metadata IDs, ACL, purge audit |
| R-14 | تحديث signed وخارج العمل وrollback | SEC-02 / SPEC-012 | bad signature refused, schedule honored |
| R-15 | 50 PCs LAN server HDD 1TB | SPEC-013 | load/capacity/latency metrics and alert |
| R-16 | AD/non-AD clients + one admin | SPEC-002/013 | enrollment identity and admin MFA |
| R-17 | فشل آمن عند lock/logoff/UAC/user switch | SPEC-002/003/009 | no capture outside eligible session |
| R-18 | Performance on Gen5/8GB/dual FHD | SPEC-013 | idle and active counters in test record |
| R-19 | ترخيص GPL3/MIT والتوزيع | SEC baseline/operations | LICENSE/NOTICE/source provision review |
| R-20 | عدم إجراء تجسس خفي | all Specs | visible indicator/local pause/consent negative cases |

### 7.1 مسارات اختبار أساسية — لا يوجد تساهل

**Access tests:** ViewOnly input/mouse/keyboard/CtrlAltDel/session switch/clipboard/file payload/API route → deny؛ FullControl authorized remains functional؛ organization and group boundaries؛ stale accessKey، replay، session clone.

**Identity tests:** reboot before logon; user login; UAC secure desktop; locked desktop; session switch; Remote Desktop session; logoff while capture active; two logged-in users; service upgrade restart; helper fails and support remains.

**Media tests:** Screenshots 2 monitors mixed DPI; video 1m/hour and actual duration; mic-only (real audio file vs black-video), webcam silent track; input device absent/in use; audio/video concurrent; consent revoked mid-stream; call privacy uncertainty.

**Transfer tests:** 5GB threshold before new job, 5GB exactly, no unacked deletion; disconnected network; duplicate chunk; out-of-order chunk; server receives then ACK lost; corrupt digest; disk full during final commit; server restore consistency.

**Deployment tests:** unsigned/tampered zip or script; trusted signer rollover; rollback; server/agent mismatched versions; delayed update at work hours; rollback no queue loss.

---

## 8. التخزين والأداء واختبار السعة قبل إنتاج أي خطة Retention

هذه **معادلات تصميم** وليست نتيجة Benchmarks:

- الصور اليومية = `عدد الأجهزة × ساعات الدوام × (60 / دقائق الفاصل) × عدد الصور لكل تشغيل`.
- مثال: 50 جهازًا × 8 ساعات × صورة كل 5 دقائق = **4,800 دفعة تصوير يوميًا**. إذا خزّنت صورة مستقلة لكل شاشة يصبح العدد **9,600 صورة**؛ وإذا صورة موحدة تجمع الشاشتين يصبح **4,800 ملف**. القرار معماري ويؤثر في السعة.
- فيديو الشاشة اليومي = `الأجهزة × مرات التسجيل/اليوم × مدة المرة بالدقائق`. مثال دقيقة كل ساعة في 8 ساعات = **400 دقيقة فيديو/يوم** لـ50 جهازًا، قبل الكاميرا أو الصوت.
- حجم الفيديو بالبايت تقريبًا `seconds × bitrate_bits_per_second / 8`؛ يتغير بشدة حسب encoder/content/fps/resolution.
- لا تعتمد نسبة ثابته للصوت أو الكاميرا؛ **قِس ملفات عينات** من كل نوع على أجهزة قليلة وبنفس الجدول.
- لا تفترض 1TB usable بالكامل: OS/DB/logs/snapshots/backups/staging/retention reserves؛ والتخزين المحلي 5GB/Client **ليس جزءًا من 1TB الخادم**.
- قياس live viewer streaming في نفس وقت الفيديو المجدول شرط خاص: network + CPU + encoder + RAM + user experience. `Idle` يقاس منفصلًا.

### 8.1 جدول قياسات إلزامية

| Metric | Idle | Screenshot | Video+Mic | Mic-only | Webcam | Live 4 sources | طريقة الإثبات |
|---|---|---|---|---|---|---|---|
| CPU P50/P95 | TBD | TBD | TBD | TBD | TBD | TBD | Windows Perf counters/ETW |
| Working Set/Private Bytes | TBD | TBD | TBD | TBD | TBD | TBD | per-process counters |
| GPU/video encode | TBD | TBD | TBD | TBD | TBD | TBD | GPU counters |
| Disk write/IOPS | TBD | TBD | TBD | TBD | TBD | TBD | Disk counters |
| NIC throughput | TBD | TBD | TBD | TBD | TBD | TBD | traffic capture/counters |
| Latency start/stop/ACK | TBD | TBD | TBD | TBD | TBD | TBD | timestamped structured logs |
| Startup/upgrade time | TBD | TBD | TBD | TBD | TBD | TBD | event logs |

**Release thresholds:** تُحدّد من Benchmark baseline ومتطلبات العمل ويصادق عليها المالك بعد القياسات، ولا نخترع سقف CPU/RAM الآن.

---

## 9. البرومبت الرئيسي العام (أرسله كـSystem/Instructions قبل أي مهام)

```text
You are a skeptical Principal Software Engineer, Security Architect and Software-Maintenance Lead responsible for extending a real Remotely fork, not creating a greenfield replacement.

Repository: immense/Remotely fork. Capture-engine candidate: sskodje/ScreenRecorderLib. Read current AGENTS.md, ratified .specify/memory/constitution.md, current Spec Kit feature and the engineering audit before each phase. Verify actual git SHA and paths; old reports are evidence snapshots, not immutable truth.

NON-NEGOTIABLES:
- Preserve remote support, device identity and authenticated transport.
- Respect security release gates G1-G7. No production rollout while any critical gate is unknown or failed.
- Live View is a ViewOnly session with independent per-source toggles (Screen, SystemAudio, Microphone, Webcam). The browser is untrusted; each media request needs server and device authorization/consent. FullControl is a separate authorization mode.
- Scheduled capture requires visible revocable employee consent, a local privacy pause and fail-closed handling of private calls.
- Windows service manages jobs/storage/upload; a separate non-elevated user-session Capture Helper performs capture; do not run capture as SYSTEM or in Session 0.
- ScreenRecorderLib is unapproved until Windows PoC; use ICaptureEngine and only one engine initially.
- Clients ~50, Windows 10/11 of mixed support status, old CPUs, dual FHD, server Windows/HDD 1TB. Optimize only with measured evidence.
- All media uploaded after creation with resumable, idempotent receipts; 5GB cap of unacknowledged media; never silently delete unacknowledged files.
- Protect GPL-3.0/MIT obligations, signed updates, secrets, org/device authorization and auditability.

METHOD:
1) Read/trace relevant paths and dependencies. Label VERIFIED SOURCE, RUNTIME TESTED, INFERRED, UNKNOWN, PROPOSED; never invent code paths or results.
2) State problem, scope, threat model, affected invariants and minimal test reproducer.
3) Recommend a bounded ADR and tests-first fix. Do not make changes until the specific task authorizes it.
4) If authorized, implement the smallest patch, run commands on the correct OS, record actual outcomes and remaining unknowns; generate reviewable diff and rollback.
5) Independent adversarial review before recommending merge; no commits, pushes, production installs or unapproved network/camera/mic capture.

OUTPUT FOR EVERY TASK (Arabic):
A. Baseline SHA/Environment B. Evidence table C. Architecture impact D. 1-task plan E. Acceptance tests including negative paths F. Exact files changed or proposed G. Tests run vs not run H. Security Gate status I. Rollback J. STOP/GO verdict and one next action.

Never flatter the existing architecture or generated code. If a design is unsafe, outdated, redundant, or incompatible, say so clearly with the specific supporting evidence and an upgrade/repair plan. Ask for human approval at irreversible or production-sensitive boundaries, not as a substitute for analysis.
```

---

## 10. خريطة الحزمة الأصلية: 52 برومبتًا إلى مراحل المشروع

المرجع هو ملف المستخدم **بالضبط**: عدد 52 عنوانًا في 8 أقسام. في القائمة الآتية لم ننسب الأرقام لبرومبتات غير موجودة؛ كل رقم يطابق عنوانه الأصلي. هذه **نسخ مُكيَّفة لمشروع Remotely** وليست إعادة نشر حرفية للقوالب الأصلية.

| # | العنوان الأصلي بالعربية | استخدم في المرحلة |
|---|---|---|
| 01 | رسم خريطة لمستودع غير مألوف من شجرة الملفات | P01, P07 |
| 02 | شرح عقد الدالة والضمانات والآثار الجانبية | P01, P07, P08 |
| 03 | تتبع طلب واحد عبر قاعدة الكود | P01, P05, P09 |
| 04 | استخراج قواعد العمل المخفية في الكود القديم | P01 |
| 05 | كتابة README لمشروع موجود | P01 |
| 06 | تعلم لغة جديدة عبر الربط بلغة تعرفها | P01 |
| 07 | فرز تتبع المكدس: من الخطأ إلى السبب المحتمل | P02, P06 |
| 08 | تصحيح قائم على الفرضيات لبج لا تستطيع تفسيره | P02, P08, P12, P13 |
| 09 | بناء حالة إعادة إنتاج مصغرة | P02, P08, P13 |
| 10 | إيجاد الـ Commit الذي كسر الكود باستخدام git bisect | P04 |
| 11 | تشخيص بج متقطع أو حالة تسابق | P02, P06, P13 |
| 12 | يعمل على جهازي: مقارنة البيئتين | P02, P08 |
| 13 | الخط الزمني للحادث وتقرير ما بعد الحادث بدون لوم من السجلات | P18 |
| 14 | تحويل تذكرة غامضة إلى مواصفات قابلة للبناء | P05, P09, P10, P11, P14, P15 |
| 15 | مقارنة خيارين للتصميم بجدول مقايضات | P04, P07, P08 |
| 16 | كتابة سجل قرار معماري (ADR) | P04, P07, P08, P09 |
| 17 | تصميم عقد HTTP API قبل كتابة الكود | P05, P09, P15, P16, P17 |
| 18 | تنفيذ يبدأ بالاختبارات من المواصفات | P05, P09, P11, P12, P13, P14, P15, P16 |
| 19 | تنفيذ دالة بعقد صارم | P11 |
| 20 | نمذجة تهديدات لميزة قبل بنائها | P03, P05, P06, P09, P10, P14, P15, P16, P17 |
| 21 | مراجعة كود من مهندس أول لـ diff مع ملصقات خطورة | P03, P05, P18 |
| 22 | مراجعة أمنية لتغيير في الكود | P03, P05, P06, P10, P15, P17, P18 |
| 23 | مراجعة أداء: إيجاد الأسطر المكلفة | P04, P08, P12, P13, P14, P15, P16, P18 |
| 24 | فحص تبعية جديدة قبل إضافتها | P03, P04, P06, P08, P17 |
| 25 | مراجعة ذاتية لـ Pull Request الخاص بك قبل طلب الآخرين | P18 |
| 26 | كتابة رسالة Commit من diff | P17 |
| 27 | كتابة وصف Pull Request يمكن للمراجعين التصرف بناءً عليه | P06, P17, P18 |
| 28 | مراجعة CI Pipeline أو Dockerfile للسرعة والأمان | P02, P03, P04, P17, P18 |
| 29 | التخطيط لإعادة هيكلة تحافظ على السلوك بخطوات صغيرة | P04, P07 |
| 30 | تقسيم دالة طويلة بدون تغيير سلوكها | P07 |
| 31 | إزالة الكود الميت وأعلام الميزات القديمة بأمان | P04, P07 |
| 32 | تحسين موجه بالـ Profiling من قياسات حقيقية | P08, P12, P13, P18 |
| 33 | التخطيط لترقية إصدار رئيسي لإطار عمل أو مكتبة | P04 |
| 34 | نقل الكود إلى لغة أخرى بدون فقدان السلوك | P08 |
| 35 | اختبارات وحدة بفئات التكافؤ والحدود | P03, P05, P06, P09, P10, P11, P12, P14, P15, P16, P17 |
| 36 | صيد الحالات الحدية: المدخلات الأكثر احتمالاً لكسر الكود | P03, P06, P08, P10, P11, P12, P13, P14, P15, P17 |
| 37 | اختبارات قائمة على الخصائص لدالة | P05, P11, P16 |
| 38 | اختبارات توصيف للكود القديم غير المختبر | P02, P18 |
| 39 | اختبار كود بتبعيات خارجية باستخدام Fakes و Mocks | P03, P05, P09, P10, P11, P13, P14, P15, P16, P17 |
| 40 | مراجعة حزمة اختبارات لاختبارات لا يمكن أن تفشل | P02, P06, P18 |
| 41 | كتابة SQL من سؤال بلغة طبيعية | P16 |
| 42 | قراءة خطة EXPLAIN وإصلاح استعلام بطيء | P16 |
| 43 | تصميم مخطط قاعدة بيانات لميزة جديدة | P11, P16 |
| 44 | خطة ترحيل قاعدة بيانات بدون توقف | P16 |
| 45 | كتابة واختبار تعبير نمطي (Regex) | P16 |
| 46 | تنظيف وتحقق من ملف بيانات فوضوي بسكريبت | P16 |
| 47 | كتابة AGENTS.md أو CLAUDE.md للمستودع | P00, P01, P07 |
| 48 | تكليف وكيل برمجة بمهمة واحدة | P00 |
| 49 | مراجعة diff مولد بالذكاء الاصطناعي قبل دمجه | P00, P15, P18 |
| 50 | تحويل سير عمل متكرر إلى مهارة قابلة لإعادة الاستخدام | P18 |
| 51 | إعادة ضبط وكيل برمجة عالق في حلقة | P02 |
| 52 | تخطيط VibeCode - نسخة مختصرة | P00 |

---

## 11. مكتبة 52 برومبتًا مُكيَّفًا وجاهزًا للوكيل

**التشغيل:** انسخ البرومبت ذي الرقم المطلوب وأرسله بعد البرومبت الرئيسي، مع المسارات الواقعية/سجل الفشل/الـdiff. كل قالب مصمم للعمل منفردًا ويمنع الادعاء بإجراء اختبار لم يحدث. الأوامر ذات الأثر تتطلب تصريحًا منفصلًا.

### PR-01 — خريطة المستودع الفعلية

**الأصل #01:** رسم خريطة لمستودع غير مألوف من شجرة الملفات  
**المراحل المرتبطة:** P01, P07

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in خريطة المستودع الفعلية.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: ارسم بنية Server/Agent/Desktop.Win/Desktop.Shared/Desktop.Native/Shared/Tests، وأجب أين تعمل Session 0 وأين user session وأين يدخل browser viewer، مع تصنيف SEEN/INFERRED.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-02 — عقود الدوال الحرجة

**الأصل #02:** شرح عقد الدالة والضمانات والآثار الجانبية  
**المراحل المرتبطة:** P01, P07, P08

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in عقود الدوال الحرجة.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اشرح عقود `ViewerHub.SendDtoToClient`, `ChangeWindowsSession`, `UpdaterWin`, `CreateInteractiveSystemProcess`, و`ScriptScheduleDispatcher`: inputs/authorization/invariants/side effects/failure modes.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-03 — تتبع جلسة View Only

**الأصل #03:** تتبع طلب واحد عبر قاعدة الكود  
**المراحل المرتبطة:** P01, P05, P09

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in تتبع جلسة View Only.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: تتبع فتح جلسة View Only من Blazor/API عبر Hub إلى Agent/Desktop ثم إدخال المستخدم، وارسم sequence diagram وعلم كل بوابة أمنية لا تعمل.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-04 — قواعد العمل الضمنية

**الأصل #04:** استخراج قواعد العمل المخفية في الكود القديم  
**المراحل المرتبطة:** P01

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in قواعد العمل الضمنية.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: استخرج قواعد session cloning, API defaults, work schedules, tenant access, remote-control auth والـupdate behavior؛ اربط كل قاعدة باختبار توصيف.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-05 — README محلي صحيح

**الأصل #05:** كتابة README لمشروع موجود  
**المراحل المرتبطة:** P01

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in README محلي صحيح.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب README لتشغيل Server/Agent/Viewer واختبارات Windows مع أوامر SDK/TS/LibMan والمشاكل المعروفة؛ إثبات نظافة الإعداد على جهاز منفصل.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-06 — ترجمة مفاهيم C++/CLI إلى .NET

**الأصل #06:** تعلم لغة جديدة عبر الربط بلغة تعرفها  
**المراحل المرتبطة:** P01

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in ترجمة مفاهيم C++/CLI إلى .NET.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اشرح لفريق C# حدود C++/CLI mixed-mode و`/clr:netcore`, MSVC, Media Foundation, x64 وداخل أي process يتبع الـruntime، دون تحويل لغة المشروع.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-07 — تتبع crash build/CI

**الأصل #07:** فرز تتبع المكدس: من الخطأ إلى السبب المحتمل  
**المراحل المرتبطة:** P02, P06

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in تتبع crash build/CI.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: حلّل Stack Trace أو NETSDK1045 أو Viewer msgpack error كفرضيات مع بيئة التشغيل وأول frame من التطبيق؛ لا تحكم من الرسالة وحدها.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-08 — تصحيح بالفروض

**الأصل #08:** تصحيح قائم على الفرضيات لبج لا تستطيع تفسيره  
**المراحل المرتبطة:** P02, P08, P12, P13

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in تصحيح بالفروض.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب فرضيات مستقلة لفشل screen capture على جيل 5 أو تعارض الصوت أو timeout في Agent Hub، ثم صمّم تجارب تفصل بين OS/runtime/permission/driver/codec.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-09 — Minimal repro

**الأصل #09:** بناء حالة إعادة إنتاج مصغرة  
**المراحل المرتبطة:** P02, P08, P13

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Minimal repro.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: ابن PoC معزول لحمل ScreenRecorderLib في .NET target وللكاميرا/الميكروفون مع نشر x64، باستخدام بيانات مختبرية وحجم صغير، من دون تعديل Remotely.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-10 — Git bisect

**الأصل #10:** إيجاد الـ Commit الذي كسر الكود باستخدام git bisect  
**المراحل المرتبطة:** P04

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Git bisect.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: إذا كسر تحديث NuGet/TFM وظائف Viewer، استخدم bisect وأداة تحقق determinisitic مع reset، ولا تشغّل تغييرات DB غير قابلة للرجوع.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-11 — Race conditions

**الأصل #11:** تشخيص بج متقطع أو حالة تسابق  
**المراحل المرتبطة:** P02, P06, P13

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Race conditions.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: حلّل التقاطين يتنافسان على الميكروفون أو تبديل Session/logout أثناء التسجيل أو partial upload عند reconnect، وصمّم reproducer وstate machine موحد.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-12 — مصفوفة توافق البيئات

**الأصل #12:** يعمل على جهازي: مقارنة البيئتين  
**المراحل المرتبطة:** P02, P08

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in مصفوفة توافق البيئات.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: قارن Windows 10 Enterprise/LTSC/Pro وWindows 11 وWindows Server + arch وDPI وVC++/MF و.NET Runtime؛ استخرج أوامر inventory وخطة للبيانات غير المعروفة.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-13 — Incident postmortem

**الأصل #13:** الخط الزمني للحادث وتقرير ما بعد الحادث بدون لوم من السجلات  
**المراحل المرتبطة:** P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Incident postmortem.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: من logs لرفع غير مكتمل أو تحديث فاشل، اكتب timeline UTC للسبب وMTTD/MTTR بإثبات، وخطة Prevent/Detect/Mitigate بدون لوم.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-14 — صياغة Spec Kit feature

**الأصل #14:** تحويل تذكرة غامضة إلى مواصفات قابلة للبناء  
**المراحل المرتبطة:** P05, P09, P10, P11, P14, P15

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in صياغة Spec Kit feature.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: حوّل طلب المستخدم إلى spec سلوكي على النمط Given/When/Then، مع حالات Consent/Skip/Denied وعدم إملاء stack داخل spec.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-15 — مقارنة خيارات تصميم

**الأصل #15:** مقارنة خيارين للتصميم بجدول مقايضات  
**المراحل المرتبطة:** P04, P07, P08

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in مقارنة خيارات تصميم.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: قارن `Desktop.Win` in-process مع user Capture Helper منفصل، وSQL Server Express مع PostgreSQL أو SQLite للسيرفر، وبيّن نقاط الرجوع وPoC أرخص.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-16 — ADR

**الأصل #16:** كتابة سجل قرار معماري (ADR)  
**المراحل المرتبطة:** P04, P07, P08, P09

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in ADR.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: أنشئ ADR لقرار ترقية .NET، Capture Helper launch، HTTP upload vs SignalR، Scheme للداتا، والحفاظ على Source compatibility.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-17 — API Contract

**الأصل #17:** تصميم عقد HTTP API قبل كتابة الكود  
**المراحل المرتبطة:** P05, P09, P15, P16, P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in API Contract.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: صمم API Contracts لـAccessMode/LiveMediaRequest/CaptureSchedule/UploadStart/Chunk/Commit/Receipt/Retention؛ authorization وHTTP errors وidempotency لكل endpoint.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-18 — TDD من المواصفة

**الأصل #18:** تنفيذ يبدأ بالاختبارات من المواصفات  
**المراحل المرتبطة:** P05, P09, P11, P12, P13, P14, P15, P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in TDD من المواصفة.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: خذ معيار قبول واحد في SPEC-001 أو screenshot/queue وأكتب tests failing أولًا ثم أصغر implementation مع full regression وتوثيق ما لم يجر.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-19 — تنفيذ بعقد صارم

**الأصل #19:** تنفيذ دالة بعقد صارم  
**المراحل المرتبطة:** P11

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in تنفيذ بعقد صارم.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: نفّذ Policy evaluator أو receipt validator أو stream state transition بعقد inputs/errors/complexity وحدود وUnicode/path normalization دون speculative features.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-20 — Threat modeling

**الأصل #20:** نمذجة تهديدات لميزة قبل بنائها  
**المراحل المرتبطة:** P03, P05, P06, P09, P10, P14, P15, P16, P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Threat modeling.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: طبّق STRIDE على agent SERVICE→Helper IPC→Remotely Server→Viewer Browser، مع consent/media/updates وfile upload وخطورة SYSTEM privilege.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-21 — Diff Review

**الأصل #21:** مراجعة كود من مهندس أول لـ diff مع ملصقات خطورة  
**المراحل المرتبطة:** P03, P05, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Diff Review.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: راجع PR من Remotely: correctness, auth, privilege, migrations الثلاثة, desktop regressions, TS-generated JS, negative tests، وأصدر P0/P1/P2 verdict.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-22 — Security Review

**الأصل #22:** مراجعة أمنية لتغيير في الكود  
**المراحل المرتبطة:** P03, P05, P06, P10, P15, P17, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Security Review.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: راجع code diff خاص `ViewOnly` وupdater والتخزين وAPI auth والـtokens: unauthorized DTOs, session upgrade, replay, path traversal, privilege escalation.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-23 — Performance Review

**الأصل #23:** مراجعة أداء: إيجاد الأسطر المكلفة  
**المراحل المرتبطة:** P04, P08, P12, P13, P14, P15, P16, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Performance Review.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: ابحث عن allocations/CPU/encoder contention/Disk IOPS/SignalR oversized messages/DB N+1 في diff قبل وبعد القياس، ولا تعلن رقمًا بدون trace.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-24 — Dependency Audit

**الأصل #24:** فحص تبعية جديدة قبل إضافتها  
**المراحل المرتبطة:** P03, P04, P06, P08, P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Dependency Audit.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: افحص NuGet/npm-like LibMan/native DLL/SharpDX/SkiaSharp/Avalonia/ScreenRecorderLib: versions, licenses, CVEs, .NET10 support, runtime footprint.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-25 — PR Self Review

**الأصل #25:** مراجعة ذاتية لـ Pull Request الخاص بك قبل طلب الآخرين  
**المراحل المرتبطة:** P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in PR Self Review.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب مراجعة ذاتية لمهمة صغيرة: scope, tests run, tests skipped, security gates, rollback, screenshot/trace evidence, changed DLLs/migrations/JS.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-26 — Commit Message

**الأصل #26:** كتابة رسالة Commit من diff  
**المراحل المرتبطة:** P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Commit Message.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب commit message من actual staged diff وفق نمط Remotely مع وصف في body للـsecurity behavior وتجنّب claim اختبارات لم تنفذ.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-27 — PR Description

**الأصل #27:** كتابة وصف Pull Request يمكن للمراجعين التصرف بناءً عليه  
**المراحل المرتبطة:** P06, P17, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in PR Description.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب وصف PR actionable للمراجعين بمصفوفة before/after وP0 risks وrepro commands وnegative cases وdeployment plan.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-28 — CI & Docker Audit

**الأصل #28:** مراجعة CI Pipeline أو Dockerfile للسرعة والأمان  
**المراحل المرتبطة:** P02, P03, P04, P17, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in CI & Docker Audit.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: راجع `.github/workflows` وAzure pipelines وDocker compose/Windows hosting، SDK mismatches، generated TS/JS/LibMan، signing, SBOM, secret handling.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-29 — Refactor with behavior protection

**الأصل #29:** التخطيط لإعادة هيكلة تحافظ على السلوك بخطوات صغيرة  
**المراحل المرتبطة:** P04, P07

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Refactor with behavior protection.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: خطط Refactor تدريجيًا لـDataService God object أو AgentHubConnection أو Desktop code دون إعادة كتابة Remotely وتحت characterization tests.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-30 — Decompose function

**الأصل #30:** تقسيم دالة طويلة بدون تغيير سلوكها  
**المراحل المرتبطة:** P07

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Decompose function.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: قسّم دالة authorization أو upload كبيرة إلى policy/auth/transport/state مع الحفاظ على side effects وترتيب callbacks وفعالية audit.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-31 — Dead Code Removal

**الأصل #31:** إزالة الكود الميت وأعلام الميزات القديمة بأمان  
**المراحل المرتبطة:** P04, P07

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Dead Code Removal.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: تحقق من `Desktop.Core` وعدم الإشارة إليه من csproj/CI/RID، ثم اقترح حذفًا منفصلًا محميًا بـbuild matrix فقط عند صفر مراجع.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-32 — Profile-guided optimization

**الأصل #32:** تحسين موجه بالـ Profiling من قياسات حقيقية  
**المراحل المرتبطة:** P08, P12, P13, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Profile-guided optimization.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: استخدم ETW/dotnet-counters/PerfView أو traces فعلية من جيل5 و8GB للشاشة/الميكروفون/الكاميرا، واكتب before/after بالبيئة نفسها.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-33 — Major Upgrade Plan

**الأصل #33:** التخطيط لترقية إصدار رئيسي لإطار عمل أو مكتبة  
**المراحل المرتبطة:** P04

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Major Upgrade Plan.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: خطط .NET8→.NET10، EF/SignalR/Avalonia/SkiaSharp/NAudio/SharpDX وWindows editions، breaking changes/compat/rollback/parallel CI.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-34 — Cross-language migration

**الأصل #34:** نقل الكود إلى لغة أخرى بدون فقدان السلوك  
**المراحل المرتبطة:** P08

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Cross-language migration.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: لا تهاجر كل المشروع إلى لغة أخرى؛ استخدم هذا القالب فقط إذا فرض PoC porting C++/CLI→C++ native/managed wrapper، مع tests equivalence.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-35 — Unit boundary tests

**الأصل #35:** اختبارات وحدة بفئات التكافؤ والحدود  
**المراحل المرتبطة:** P03, P05, P06, P09, P10, P11, P12, P14, P15, P16, P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Unit boundary tests.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: صمم اختبارات ViewOnly relay/FullControl/consent/state machine/job collision/5GB queue بمجموعات تكافؤ وحدود/رفض ونشر أزمنة دقيقة.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-36 — Adversarial edge cases

**الأصل #36:** صيد الحالات الحدية: المدخلات الأكثر احتمالاً لكسر الكود  
**المراحل المرتبطة:** P03, P06, P08, P10, P11, P12, P13, P14, P15, P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Adversarial edge cases.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اختبر no user, 2 users switch, UAC secure desktop, revoked consent midstream, 5GB exact, OS camera busy, network flap, clock DST, duplicate chunk.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-37 — Property-based tests

**الأصل #37:** اختبارات قائمة على الخصائص لدالة  
**المراحل المرتبطة:** P05, P11, P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Property-based tests.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: خصائص: ViewOnly لا يتحول إلى FullControl، revoke لا ينتج active stream، `CaptureId` لا يسبب duplicate، priority scheduler يقبل عملًا واحدًا.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-38 — Characterization tests

**الأصل #38:** اختبارات توصيف للكود القديم غير المختبر  
**المراحل المرتبطة:** P02, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Characterization tests.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: ثبت السلوك الحالي لـRemotely pre-update: ViewOnly UI, FullControl legitimate, enrollment, script schedule, chat, file transfer مع baseline before/after.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-39 — Fakes and Mocks

**الأصل #39:** اختبار كود بتبعيات خارجية باستخدام Fakes و Mocks  
**المراحل المرتبطة:** P03, P05, P09, P10, P11, P13, P14, P15, P16, P17

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Fakes and Mocks.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اصنع fake ScreenRecorderLib/clock/IPC/network/server receipt/consent store to test no hardware/no real employees، ثم integration على جهاز تجريبي.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-40 — Audit test suite

**الأصل #40:** مراجعة حزمة اختبارات لاختبارات لا يمكن أن تفشل  
**المراحل المرتبطة:** P02, P06, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Audit test suite.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: تحقق هل tests تكتشف fault فعليًا، وهل ignored tests أو مشروع غير مدرج يجعل “all green” مضلِّلًا، واقترح mutation/negative tests.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-41 — SQL from requirements

**الأصل #41:** كتابة SQL من سؤال بلغة طبيعية  
**المراحل المرتبطة:** P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in SQL from requirements.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب queries لصور جهاز ونوع/وقت وretention وpending uploads وaudit events باستخدام provider المعتمد وparameterized SQL.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-42 — Query plan optimization

**الأصل #42:** قراءة خطة EXPLAIN وإصلاح استعلام بطيء  
**المراحل المرتبطة:** P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Query plan optimization.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اقرأ EXPLAIN/Actual Execution Plan لصفحة 50 جهازًا وعدد كبير من الـmedia metadata، واقترح index/pagination بعد قياس.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-43 — DB schema

**الأصل #43:** تصميم مخطط قاعدة بيانات لميزة جديدة  
**المراحل المرتبطة:** P11, P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in DB schema.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: صمم CaptureSchedule, CaptureRecord, Consent, LiveMediaGrant, AuditEvent, UploadSession مع FK/index/unique DeviceId+CaptureId/retention.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-44 — Zero-downtime migration

**الأصل #44:** خطة ترحيل قاعدة بيانات بدون توقف  
**المراحل المرتبطة:** P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Zero-downtime migration.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: خطط expand/backfill/contract لقاعدة Remotely عبر 3 EF providers أو قرار موثق لدعم واحد، backups وfallback client version.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-45 — Regex

**الأصل #45:** كتابة واختبار تعبير نمطي (Regex)  
**المراحل المرتبطة:** P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Regex.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اصنع regex ضيقًا لتحليل log IDs أو sanitization UI labels فقط، ولا تستخدم regex بدل path canonicalization أو input auth.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-46 — Data cleanup

**الأصل #46:** تنظيف وتحقق من ملف بيانات فوضوي بسكريبت  
**المراحل المرتبطة:** P16

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Data cleanup.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: اكتب سكربت تحقق يطابق media filesystem مع metadata/digests ويقترح repair report؛ لا يحذف أي unacknowledged media تلقائيًا.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-47 — AGENTS.md

**الأصل #47:** كتابة AGENTS.md أو CLAUDE.md للمستودع  
**المراحل المرتبطة:** P00, P01, P07

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in AGENTS.md.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: حدّث قواعد الوكيل بالمسارات الصحيحة وحالة .NET/Spec Kit/branch convention/Gates/No-code-change بدون معلومات عن Caddyfile أو الاختبارات خاطئة.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-48 — One Task Brief

**الأصل #48:** تكليف وكيل برمجة بمهمة واحدة  
**المراحل المرتبطة:** P00

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in One Task Brief.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: كلف الوكيل بمهمة واحدة bounded scope وlist files/acceptance/tests/rollback/do not touch؛ ارفض تغييرات إضافية غير مطلوبة.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-49 — AI-generated Diff Audit

**الأصل #49:** مراجعة diff مولد بالذكاء الاصطناعي قبل دمجه  
**المراحل المرتبطة:** P00, P15, P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in AI-generated Diff Audit.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: راجع patch من Agent لViewOnly/Updater/IPC/Upload، evidence per line، authorization regressions، misuse of SYSTEM، ويصدر merge/no-merge.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-50 — Reusable Skill

**الأصل #50:** تحويل سير عمل متكرر إلى مهارة قابلة لإعادة الاستخدام  
**المراحل المرتبطة:** P18

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Reusable Skill.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: حوّل سير عمل مراجعة pre-merge وإثبات test evidence وsecurity gates إلى `.agents/skills/.../SKILL.md` صغير، بلا أسرار أو auto-deploy.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-51 — Agent Reset

**الأصل #51:** إعادة ضبط وكيل برمجة عالق في حلقة  
**المراحل المرتبطة:** P02

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in Agent Reset.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: لو الوكيل يدور في حلقة تحديث dependencies أو اختبارات CI، اعرض confirmed facts/failed attempts والـnext experiment فقط؛ stop code churn.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

### PR-52 — VibeCoding Strategic Review

**الأصل #52:** تخطيط VibeCode - نسخة مختصرة  
**المراحل المرتبطة:** P00

```text
ROLE: Act as an independent senior .NET/Windows engineer specializing in VibeCoding Strategic Review.
PROJECT: Existing Remotely fork with Blazor/SignalR/Windows service, plus planned consent-based Capture Helper and optional ScreenRecorderLib after PoC.
TASK: راجع المنتج كاملًا بعقلية Staff Engineer: ما الذي لا يصلح هندسيًا؟ ما الإطار الناقص أو القديم؟ ما أصغر architecture viable؟ وخطة stop/go.
INPUTS: [ACTUAL_REPO_PATH], [GIT_COMMIT_SHA], [CURRENT_SPEC_OR_TICKET], [RELATED_SOURCE_FILES], [LOGS_OR_DIFF_OR_TRACE], [TARGET_WINDOWS_MATRIX].
REQUIRED STEPS: 1. Read only necessary source and cited callers; 2. Separate [SOURCE-CAPABILITY] from [RUNTIME-VERIFIED] from [PROPOSED]/[UNKNOWN]; 3. Identify invariants, threat boundaries and failure cases; 4. Derive reproducible tests and the smallest safe change; 5. Report why competing solutions were rejected.
REQUIRED OUTPUT (Arabic): Evidence with file:line/symbol; precise issue statement; prioritized observations P0/P1/P2; design decision and trade-offs; step-by-step acceptance/negative tests; affected module/API/DB migration; commands to verify; risks & rollback; status of G1-G7; next single action.
RULES: Do not invent files/APIs/version support, hide security regressions, bypass employee consent, disable privacy status, capture without authorization, or claim unrun tests passed. Do not edit production files, install, commit, push or deploy unless this exact task explicitly grants it. Stop at the requested phase gate.
```

---

## 12. Runbooks: قوالب تسليم واختبار ومراجعة PR

### 12.1 بطاقة مهمة واحدة للوكيل

```yaml
TaskId: SEC-01-subtask-01
LinkedSpec: SPEC-001
OwnerApproval: required-before-code-change
BaselineCommit: <sha>
Purpose: <single observable outcome>
AllowedPaths: [<specific files>]
ForbiddenPaths: [<files / generated / vendor>]
Preconditions: [<test evidence>]
AcceptanceCriteria:
  - <Given / When / Then>
  - <negative security test>
TestsToRun:
  - <Windows x64 command and expected output>
Rollback: <restore procedure>
ReleaseGateImpacted: [G1]
OutputArtifacts: [<diff>, <TRX>, <log>, <ADR>]
StopCondition: review-and-approval
```

### 12.2 Gate Evidence Record — لا يمر الـGate دونها

```yaml
Gate: G1
Status: OPEN # OPEN | FAIL | PASS | ACCEPTED_RISK (only allowed where policy permits)
Commit: <SHA>
Environment: <Windows edition/build + .NET runtime + browser>
Verification:
  Command: <exact command>
  ExecutedAt: <timestamp>
  Result: <PASS/FAIL + log path>
NegativeTests: [<attack simulation names, no real targets>]
Reviewer: <person or independent agent>
ResidualRisks: []
OwnerSignoff: <explicit recorded approval>
```

### 12.3 PR Ready-for-Review Checklist

- [ ] تغيير واحد واضح لا يصطدم بباقي المراحل.
- [ ] لا أسرار ولا بيانات حقيقية لموظفين في diff/tests/logs.
- [ ] الاختبارات تفشل قبل الإصلاح وتنجح بعده أو يوجد دليل مقبول لعدم إمكانية الاختبار القديم.
- [ ] اختبارات authorization والخصوصية مع حالات الرفض.
- [ ] تغييرات TS معها JS generated actual verified؛ لا vendor binaries غير مرخصة.
- [ ] لا `AnyCPU` مع mixed-mode native بلا إثبات توافق.
- [ ] كل migration على الـproviders المتفق عليها وdown/up على نسخ DB.
- [ ] توقيعات وحِزم ودورات rollback للـAgent.
- [ ] تقرير Benchmarks فقط من قياس.
- [ ] تمت مراجعة Patch مستقلة ببرومبت #21/#22/#49.
- [ ] Owner gate واضح ومازال غير Passed إن لم يُختبر.

### 12.4 قواعد التشغيل الفعلي للمشرف والموظف

- الموظف: يعرف ما يُلتقط ومتى والمدة والجهة المستلمة ومدة الاحتفاظ، يستطيع رؤية حالة البث وإيقافه وفق سياسة العمل والموافقة، ويتلقى مؤشرًا خاصًا للميكروفون والكاميرا.
- المشرف: يستطيع اختيار مصادر Live View منفصلة على جهاز مسموح به فقط؛ يمنع عنه FullControl إلا بصلاحية؛ لا يستطيع تجاوز Pause أو سحب Consent.
- مسؤول الصيانة: يحدث agent من manifest موثوق ويملك rollback دون إسقاط الملفات غير المؤكدة؛ ويعزل الأجهزة المختلفة بنطاق التنظيم/الجهاز.
- المراجعة القانونية: تشمل بلد التشغيل الفعلي، إعلام الموظفين، المحتوى الحساس، أصوات أطراف ثالثة، أوقات العمل، الصلاحيات، سياسات الحذف والوصول.

---

## 13. الدفعة التالية المسموح البدء بها فورًا

1. **P00:** أغلق تصحيحات Constitution v2.0.0، صدّقها على النسخة النهائية، وسجّل AC4 للـAPI والجلسات.
2. **P01:** احصل على **خريطة معمارية وجرد OS وخريطة التبعيات** على أحدث repo SHA وتحقق من نتائج فحص الوكيل المؤرخة 2024.
3. **P02:** اجعل **build + CI + Viewer smoke** يعملان فعليًا على Windows (إصلاح mismatch SDK وJS قبل أي upgrade كبير).
4. **P03:** افتح Security P0 register واجعل ViewOnly وUpdater/FileTransfer/API/IPC إصلاحات لا يمكن تجاوزها.
5. **P04:** نفّذ ترقية .NET والتبعيات بمسار محدود وقابل للرجوع، لا تفترض توافق Windows 10 Pro غير المدعوم.
6. **P05:** أنشئ `SPEC-001` وامنع أي ترقيع client-side-only لـViewOnly؛ اختبر server+device auth ثم FullControl regression.
7. **لا تبدأ P08/P09 وما بعدهما** قبل إغلاق P0 وتجهيز Windows PoC وخصوصية الموظفين.

### برومبت البداية الأول المُوصى بإرساله الآن

```text
You are the Remotely Maintenance Lead. Start P00 then P01 ONLY. Do not implement capture or upgrade the runtime yet.
Read the ratified constitution (if not ratified, STOP and report), the current source SHA, REVERSE-ENGINEERING-REPORT.md, AGENTS.md, and the supplied 52-prompt pack.
1) Correct the final constitution contradictions, preserve the previous version, and collect explicit owner ratification. Do not self-ratify.
2) Verify current Git remote/commit/status, upstream changes since the audited 2024 SHA, and actual application/module boundaries.
3) Produce a Windows edition/build inventory template, dependency and license SBOM baseline, and a source-evidence table for G1-G7.
4) List all build/CI blockers and high/critical security risks, ranked by reproducibility, impact and minimum safe remediation.
5) Recommend separate bounded P02 and P03 tasks with tests/rollback. No production code edit, install, commit, push or deployment without explicit new authorization.
Output all explanations in Arabic; include actual files and commands, UNKNOWN for untested claims. STOP before P02 execution.
```

---

## 14. مراجع التحقق الخارجية

- Remotely: https://github.com/immense/Remotely ؛ releases: https://github.com/immense/Remotely/releases
- ScreenRecorderLib: https://github.com/sskodje/ScreenRecorderLib
- GitHub Spec Kit: https://github.com/github/spec-kit ؛ https://github.github.com/spec-kit/quickstart
- Codex integration: https://github.github.io/spec-kit/integrations/codex.html
- .NET EOL notice: https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/
- .NET 10 Windows matrix: https://learn.microsoft.com/en-us/dotnet/core/install/windows
- Windows 10 lifecycle / ESU: https://learn.microsoft.com/en-us/windows/release-health/release-information
- NuGet vulnerability listing: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list
- ASP.NET Core security guidance: https://learn.microsoft.com/en-us/aspnet/core/security/

**حدود الوثيقة:** حقائق المستودع مستمدة من **تقرير فحص الوكيل** على الـcommit المذكور، وهي بحاجة لإعادة تحقق عبر code/build. المعلومات الزمنية الخارجية مُراجعة بتاريخ إعداد الملف. لم أُشغّل Remotely أو PoC أو CI بنفسي، ولم أُصلح الثغرات أو أعد المواصفات في المستودع. هذه خطة عمل وشروط اختبار وتعليمات للوكلاء، وليست بيانًا بأن المشروع آمن الآن.

# P00-B — تقرير خط أساس البناء والاختبار

**المشروع:** Remotely Capture Extension (fork of `immense/Remotely`)
**الشجرة المُقيَّمة:** `master @ 71b890b9` · **الأساس المُفحوص:** `df85026e` → `71b890b9` (3 commits من المالك أثناء الجلسة)
**التاريخ:** 2026-11-06 · **الفاحص:** وكيل (Principal .NET Engineer / Build Systems Specialist / Dependency Security Analyst)
**النطاق:** تشخيص فقط — **لم يُعدَّل أي ملف إنتاج، ولم يُثبَّت أي برنامج، ولم تُرقَّى أي اعتمادية، ولم يُغيَّر أي إطار عمل، ولم تُنشر أو تُعمل commit/push.**

---

## 1. الحكم التنفيذي

| البند | الحكم |
|---|---|
| **بناء المشروع** | ✅ **PASS** — 10 من 11 مشروعاً تُبنى بنجاح، بما فيها `Server` |
| **تشغيل الاختبارات على خط الأساس .NET 8** | ⛔ **BLOCKED** — لا يوجد Microsoft.NETCore.App 8.0 على الجهاز |
| **نتيجة الاختبارات** | ✅ **19/19 تنجح** على .NET 9 (تشخيص roll-forward) ⇒ الخلل **بيئي بحت**، لا فشل اختبارات |
| **جذر LIB002** | ✅ **مؤكَّد** — مزوّد unpkg في LibMan يبني `registry.npmjs.org/{name}@{version}` والسجل يقبل `/` لا `@` |
| **قاعدة الأدلة** | ⚠️ **لم تُرقَّ أي اعتمادية** — 12 ثغرة عالية/متوسطة في `Server` |
| **بوابة P00-B** | 🔶 **PASS مشروط** — خط الأساس **قابل للبناء**، لكنه **غير قابل للتوثيق على .NET 8** بعد 4 أيام من نهاية دعمه |

**ثلاث حقائق يجب أن تُقال أولاً:**

1. **لا يوجد "فشل اختبارات" هنا — يوجد "تعذّر تشغيل".** بـ`DOTNET_ROLL_FORWARD=Major` تعمل **19/19** اختباراً (TRX مُرفق). الكود سليم؛ الناقص هو runtime فقط. `[RUNTIME-VERIFIED]`
2. **‏.NET 8 يخرج عن الدعم في 2026-11-10 — بعد أربعة أيام.**أحدث runtime هو `8.0.33` والـSDK `8.0.427`. أي تثبيت الآن هو **إجراء مؤقت مُؤجَّل EOL**، لا هدف إنتاج. `[RUNTIME-VERIFIED]`
3. **المالك يعمل بالتوازي في نفس الملفات.** أثناء هذه الجلسة: commit ثالث (`71b890b9`) + **خمسة تعديلات إنتاج غير مُثبَّتة** في `Server/API/*` و`Server/Auth/*`. لم ألمسها ولم أُسقطها. (تفصيل §2.3).

---

## 2. سلامة مساحة العمل

### 2.1 الحالة عند الفتح

| القياس | القيمة |
|---|---|
| HEAD | `71b890b9 fix(build): remove libman restore and fix CI for partial test baseline` |
| الفرع | `master` — `ahead 3` عن `origin/master` |
| Staged | **0** |
| Unstaged (عند الفتح) | `M Server/libman.json` (تعديل المالك) |
| Untracked | **0** — لا ملفات temporية في الريبو |
| `.git/info/exclude` | `graphify-out/` · `.serena/` · `.specify.backup-*/` · `.specify.orig-*/` · `.scratch-init/` · `*.bak` · **`.p00b-lab/`** (أُضيف في هذه الجلسة) |
| `global.json` | **غير موجود** ⇒ لا تثبيت SDK |
| `Directory.Build.props` | `Nullable=enable` + `RootNamespace` فقط |
| `nuget.config` | **غير موجود** ⇒ افتراضي NuGet.org |

### 2.2 خلوّ الفهرس من الأسرار والملفات المؤقتة

فُحص الـindex بالكامل: **لا ملف `.bak` / `.env` / `.pfx` / `.key` / `secret` / `token` / `node_modules` / `*.log`** داخله. الـ`*.bak` مستثنى محلياً. ⇒ **PASS**.

### 2.3 ⚠️ تعارض: تعديلات مالك جارية فوق نفس Findings

**خمسة ملفات إنتاج مُعدَّلة وغير مُثبَّتة** — لم ألمسها:

| الملف | التعديل | الفجوة التي يغلقها |
|---|---|---|
| `Server/API/CustomBinariesController.cs` | `+[ServiceFilter(typeof(ApiAuthorizationFilter))]` على `GetWinX86Desktop` و`GetWinX64Desktop` + `using Remotely.Server.Auth;` | **GAP-09** (اكتشاف P00-A) |
| `Server/Auth/ApiAuthorizationFilter.cs` | `context.Result = new UnauthorizedResult();` عند خطأ التفويض + تعليق *"Fail closed"* | **FR-009** في SPEC-001 |
| `Server/API/ClientDownloadsController.cs` | تعديل مرتبط | GAP-08 |
| `Server/API/DevicesController.cs` | تعديل مرتبط | GAP-07 |
| `Server/Auth/ExpiringTokenFilter.cs` | تعديل مرتبط | PC-2 |

> **لم أُنفِّذ ولم أُلغِ شيئاً.** هذه تعديلات المالك؛ يقرّر هو اختبارها ودمجها. **ملاحظة فنية مُرسلت له:** `ApiAuthorizationFilter` كمرشّح `IAsyncAuthorizationFilter` — إضافة `UnauthorizedResult` عندFault تُغلق الفجوة، لكن `ExpiringTokenFilter` و`ApiAuthorizationFilter` يلتقطان الاستثناء **داخل المرشّح**؛ المسار الذي يتجاوز المرشّحات (مثل `DevicesController.Create` إن لم يحمل سمة) يبقى مفتوحاً. يُتحقَّق بالاختبار لا بالقراءة.

**غير مُثبَّت أيضاً:** `Server/libman.json` صار الآن part of `71b890b9` — أي أن المالك اعتمد **إزالة LibMan بالكامل** (`"libraries": []`). (§5.6)

### 2.4 آثار جانبية للبناء (انظر §2.3 — ليست ملكاً للمالك)

البناء الأول (قبل إصلاح المالك) كان يُعيد كتابة `Server/wwwroot/lib/fontawesome/**` و`Server/wwwroot/src/*.js` بسبب إدخالات cdnjs. **هذا مؤكَّد الآن:** حذفُ إدخالات cdnjs أزال الأثر. بعد إعادة البناء على الحالة المُثبَّتة: **`git status` نظيف بالنسبة لأصول `wwwroot`** ✓. ⇒ **تعديل المالك صحيح من هذه الناحية أيضاً** `[RUNTIME-VERIFIED]`.

---

## 3. جرد الأدوات بالضبط

```
dotnet --info
  SDK         : 9.0.315   (9.0.300-manifests.618366fb)
  MSBuild     : 17.14.43+2a0eb78b3
  Host        : 10.0.14
  Base Path   : C:\Program Files\dotnet\sdk\9.0.315\
  RID         : win-x64        OS: Windows 10.0.26100
  global.json : Not found

Runtimes المثبّتة
  Microsoft.NETCore.App            6.0.11 · 9.0.17 · 10.0.14
  Microsoft.AspNetCore.App         9.0.17
  Microsoft.WindowsDesktop.App     6.0.11 · 9.0.17
  ❌ Microsoft.NETCore.App 8.0.x   — غير مثبَّت
  ❌ Microsoft.AspNetCore.App 8.0.x — غير مثبَّت
  ❌ Microsoft.WindowsDesktop.App 8.0.x — غير مثبَّت
```

**إصدارات .NET المتاحة رسمياً (من `release-metadata`):**

| القناة | الطور | EOL | أحدث runtime | أحدث SDK |
|---|---|---|---|---|
| 8.0 | maintenance | **2026-11-10** | **8.0.33** | **8.0.427** |
| 9.0 | maintenance | 2026-11-10 | — | — |
| 10.0 | **active** | 2028-11-14 | 10.0.14 (مثبَّت) | — |

### 3.1 الأطر التي يطلبها كل مشروع فعلياً (من `*.runtimeconfig.json` المُولَّد)

| المخرج | Microsoft.NETCore.App | Microsoft.AspNetCore.App | Microsoft.WindowsDesktop.App |
|---|---|---|---|
| `Remotely.Shared.Tests.dll` | **8.0.0** | — | — |
| `Remotely.Server.Tests.dll` | **8.0.0** | **8.0.0** | — |
| `Remotely_Desktop.runtimeconfig.json` (Desktop.Win) | **8.0.0** | — | **8.0.0** |
| `Remotely_Agent.runtimeconfig.json` | **8.0.0** | **8.0.0** | — |
| `Remotely.Tests.LoadTester.runtimeconfig.json` | **8.0.0** | **8.0.0** | **8.0.0** |

⇒ **الحد الأدنى المطلوب لتفريغ الاختبارات:** `Microsoft.NETCore.App 8.0.x` + `Microsoft.AspNetCore.App 8.0.x`. للحفاظ على `Desktop.Win` و`LoadTester` يلزم `Microsoft.WindowsDesktop.App 8.0.x` أيضاً.

### 3.2 التثبيت المعزول المقترح (‏**لم يُنفَّذ — ينتظر التصريح**)

| البند | القيمة |
|---|---|
| **الطريقة** | `dotnet-install.ps1` الرسمي (‏`https://dot.net/v1/dotnet-install.ps1`) بـ`-Channel 8.0 -Runtime dotnet -Version 8.0.33` |
| **مجلد التثبيت** | `C:\Users\Administrator\Documents\smacc_TEST\dotnet8\` — **خارج الريبو** و**خارج `C:\Program Files\dotnet`** |
| **التنفيذ** | `& "$env:USERPROFILE\Documents\smacc_TEST\dotnet8\dotnet.exe" test ...` أو ضبط `DOTNET_ROOT` + `PATH` لجلسة واحدة |
| **لماذا معزول** | لا يمسّ تثبيت النظام ⇒ قابل للتراجع بـ`rm` ⇒ لا يلوّث `C:\Program Files\dotnet` |
| **الدليل** | `C:\Users\Administrator\Documents\smacc_TEST\dotnet8\` |
| **ملاحظة** | `-Runtime dotnet` (runtime فقط) **لا يكفي** لحزمة `Microsoft.AspNetCore.App` — يُثبَّت مع `dotnet` أو عبر `-Runtime aspnetcore`. الأمر المقترح: `-Channel 8.0 -Quality GA` (يشمل SDK + ASP.NET Core runtime) |
| **قيود** | EOL بعد **4 أيام** ⇒ لا يصلح هدفاً إنتاجياً؛ يُستخدم لإثبات خط الأساس التاريخي فقط |

**بديل مقترح للاعتماد**: حاوية Windows أو CI بـ`actions/setup-dotnet@v4` و`dotnet-version: 8.0.x` — **وهو بالضبط ما أضافه المالك في `71b890b9`** ⇒ مسار CI صار صحيحاً.

---

## 4. تحليل جذري: غياب runtime .NET 8

| السؤال | الإجابة | التصنيف |
|---|---|---|
| ما الذي يطلبه الاختبار؟ | `Microsoft.NETCore.App 8.0.0` (+ `AspNetCore 8.0.0` لـServer.Tests) | `[SOURCE-VERIFIED]` (runtimeconfig) |
| ما المتاح؟ | 6.0.11 / 9.0.17 / 10.0.14 | `[RUNTIME-VERIFIED]` |
| لماذا لا يعمل roll-forward تلقائياً؟ | `rollForward` الافتراضي لـ`net8.0` = `Minor` داخل الإصدار 8 فقط؛ **لا يقفز إلى 9** | `[SOURCE-VERIFIED]` |
| هل العطل بيئي أم في الشيفرة؟ | **بيئي بحت** — `DOTNET_ROLL_FORWARD=Major` ⇒ 19/19 تنجح | `[RUNTIME-VERIFIED]` |
| ما الذي كان سيُبلَّغ عنه لو اشتُكل فيه؟ | `Testhost process ... exited with error: You must install or update .NET to run this application. Framework: 'Microsoft.NETCore.App', version '8.0.0'` | `[RUNTIME-VERIFIED]` |

**الخلاصة:** لا يوجد عيب في الشيفرة ولا في الإعدادات. **الـruntime مفقود فقط.** `[RUNTIME-VERIFIED]`

> ⚠️ **تحذير منهجي (مُلتزم به):** نتيجة الـ19/19 على .NET 9 **تشخيص** تُثبت أن العطل بيئي، **ولا تُثبت** أن خط الأساس .NET 8 يعمل كما هو. حسب تكليف P00-B: *"Do not use major-version runtime roll-forward as proof that the historical baseline works unchanged."* ⇒ **خط الأساس .NET 8 يبقى `BLOCKED` حتى يُثبَّت runtime 8.0.33 فعلياً.**

---

## 5. تحليل جذري: LIB002

### 5.1 إعادة الإنتاج في بيئة معزولة (خارج الريبو)

أُعيد إنتاج الخطأ **داخل مختبر مؤقّت** (`.p00b-lab/`) بنفس حزمة NuGet وبنفس مهمة MSBuild (`Microsoft.Web.LibraryManager.Build.RestoreTask`) وبنفس نسخة `netstandard2.0`:

```
Restore operation started
libman-orig.json : error LIB002: The "@msgpack/msgpack@3.0.0-beta2" library
                    could not be resolved by the "unpkg" provider
Build FAILED.   1 Error(s)
```

⇒ **العطل ليس في الريبو ولا في شيفرة المشروع.** `[RUNTIME-VERIFIED]`

### 5.2 مصفوفة تجارب مُتحكَّم بها (المزوّد هو المتغيّر الوحيد)

| # | المزوّد | المكتبة | النتيجة |
|---|---|---|---|
| E1 | cdnjs | `microsoft-signalr@8.0.7` | ✅ **OK** (2 ملف) |
| E2 | unpkg | `@msgpack/msgpack@3.0.0-beta2` | ❌ LIB002 |
| E3 | unpkg | `@msgpack/msgpack@3.1.3` (latest) | ❌ LIB002 |
| E4 | unpkg | `@msgpack/msgpack` (بلا إصدار) | ❌ LIB002 |
| E5 | unpkg | `@msgpack/msgpack@2.5.187` | ❌ LIB002 |
| E6 | unpkg | `msgpack@2.5.187` | ❌ LIB002 |
| E7 | unpkg | `@microsoft/signalr-protocol-msgpack@8.0.7` | ❌ LIB002 |
| E8 | unpkg | `@microsoft/signalr-protocol-msgpack@10.0.11` (latest) | ❌ LIB002 |
| E9 | unpkg | `@microsoft/signalr-protocol-msgpack` (بلا إصدار) | ❌ LIB002 |
| E10 | **jsDelivr** | `@msgpack/msgpack@3.0.0-beta2` | ✅ **OK (4 ملف)** |
| E11 | **jsDelivr** | `@microsoft/signalr-protocol-msgpack@8.0.7` | ✅ **OK (4 ملف)** |

⇒ **كل** طلبات unpkg تفشل مهما كان الإصدار، و**كل** طلبات jsDelivr تنجح. ⇒ الخلل في **مسار المزوّد**، لا في الإصدار ولا في هوية المكتبة. `[RUNTIME-VERIFIED]`

### 5.3 تتبّع طلبات HTTP الفعلية (بتنفيذ `IWebRequestHandler` مخصّص)

هذا هو الدليل الحاسم. مُزوّد unpkg في LibMan **لا يستخدم unpkg للبيانات الوصفية** — بل **سجل npm**:

```
GetPackageInfoForScopedPackageAsync('@msgpack/msgpack@3.0.0-beta2')
  [REQ] https://registry.npmjs.org/@msgpack%2fmsgpack%403.0.0-beta2
  [RES] 404   {"error":"Not found"}
  ⇒ name='' desc='' latest='' versions=      ⇒ "could not be resolved"

GetNewPackageInfoAsync('@msgpack/msgpack')
  [REQ] https://registry.npmjs.org/@msgpack%2fmsgpack
  [RES] 200   (373 KB)   ⇒ latest=3.1.3  versions=0.9.0 … 3.1.3   ✅

GetPackageInfoForUnscopedPackageAsync('jquery@3.7.1')
  [REQ] https://registry.npmjs.org/jquery@3.7.1/latest   → 404
  [REQ] https://registry.npmjs.org/jquery@3.7.1          → 404
```

### 5.4 السبب الجذري المؤكَّد

**سجل npm يقبل فصل الإصدار بـ`/` لا بـ`@`.**

| عنوان الطلب | النتيجة |
|---|---|
| `https://registry.npmjs.org/jquery@3.7.1` | **404** |
| `https://registry.npmjs.org/jquery/3.7.1` | **200** |
| `https://registry.npmjs.org/@msgpack%2fmsgpack%403.0.0-beta2` | **404** |
| `https://registry.npmjs.org/@msgpack%2fmsgpack/3.0.0-beta2` | **200** |
| `https://registry.npmjs.org/@msgpack%2fmsgpack` (بلا إصدار) | **200** |

**السبب:** LibMan 2.1.175 يبني عنوان السجل على **نمط unpkg** (`{name}@{version}`) بينما واجهة npm تتطلّب `{name}/{version}`. النتيجة: **كل** مكتبة بإصدار مُثبَّت ترجع 404 ⇒ `LIB002`. والمكتبة بلا إصدار تنجح — لأنها لا تحتاج عنواناً مفصولاً.

**والمهم:** الملفات المطلوبة **موجودة** في الإصدار المطلوب — فليس خطأً في اختيار الإصدار:

| الحزمة | الإصدار | `dist.es5+umd/` أو `dist/browser/` |
|---|---|---|
| `@msgpack/msgpack` | 3.0.0-beta2 | **4 ملفات موجودة** ✓ |
| `@microsoft/signalr-protocol-msgpack` | 8.0.7 | **4 ملفات موجودة** ✓ |

`[RUNTIME-VERIFIED]`

### 5.5 استبعاد الفرضيات البديلة (كلها مرفوضة بالدليل)

| الفرضية | النتيجة | الدليل |
|---|---|---|
| انقطاع شبكة / بروكسي / TLS | **مرفوضة** | `NO_PROXY=localhost…` فقط · `netsh winhttp` ⇒ *Direct access* · كل روابط unpkg/npm/cdns تُرجع 200 |
| الحزمة غير موجودة على npm | **مرفوضة** | `registry.npmjs.org/@msgpack%2fmsgpack` ⇒ 200 مع 62 إصداراً |
| الإصدار المُثبَّت لا يحتوي الملفات | **مرفوضة** | `?meta` للإصدار المطلوب ⇒ الملفات موجودة |
| `Microsoft.Web.LibraryManager.Build 2.1.175` عتيق | **سبب مساهم لا جذر** | jsDelivr داخل **الحزمة نفسها** ينجح ⇒ العطل منطق المزوّد لا من عمر الحزمة |
| ذاكرة LibMan تالفة | **مرفوضة** | `~/.librarymanager/` يحوي `libman.config.json` فقط — لا ذاكرة ملفات |
| خطأ في `libman.json` | **مرفوضة** | الملف نفسه نظيف JSON ⇒ التوقيت (target) قبل `Build` |

### 5.6 ما الذي نفّذه المالك في `71b890b9` — وحكمه

```json
{
  "version": "1.0",
  "defaultProvider": "cdnjs",
  // All client libraries ... are committed under wwwroot/lib/. LibMan restore
  // was removed entirely: the unpkg entries failed with LIB002 (2.1.175),
  // and the cdnjs entries rewrote committed assets with LF endings on every
  // Windows build, leaving a dirty git tree. Regenerate them manually only
  // when bumping a library version.
  "libraries": []
}
```

**الحكم:** ✅ **صحيح ومنفَّذ ومُتحقَّق** — `dotnet build Server/Server.csproj` ⇒ **EXIT=0**، والشجرة تبقى نظيفة بعد البناء.
**لكنه يخلّف ثلاث ملاحظات:**
1. **‏`Microsoft.Web.LibraryManager.Build` ما زال مُشاراً إليه في `Server/Server.csproj`** ⇒ لا يزال ينفّذ هدف `LibraryManagerRestore` عند كل بناء (بلا فعالية، لكنه يربط البناء بـNuGet). **الاقتراح: إزالة المرجع** — يفكّ الارتماد ويُلغي خطر Dirty-Tree نهائياً.
2. **الأصول صارت "بلا مصدر"** — لم تعد `libman.json` تصفها. ⇒ **فجوة تتبّع جديدة (P1)**: لا_versions pinning ولا تحقق. المقترح: وثيقة `Server/wwwroot/lib/README.md` تسرد كل مكتبة وإصدارها وتاريخ التحميل.
3. **الفجوةGAP-15 باقية**: `Viewer.cshtml:33,38` يطلب `/lib/microsoft-signalr-protocol-msgpack/...` بينما الأصول في `/lib/microsoft/signalr-protocol-msgpack/...` ⇒ **404 وقت التشغيل لم يتغيّر**. (§10 — تصحيح D-2)

---

## 6. نتائج البناء لكل مشروع

**الأمر:** `dotnet build <project> -v:minimal -nologo` · SDK 9.0.315 · MSBuild 17.14.43

| المشروع | TFM | LIB002؟ | EXIT | النتيجة |
|---|---|---|---|---|
| `Shared` | net8.0 | — | **0** | ✅ Build succeeded |
| `Agent` | net8.0 | — | **0** | ✅ Build succeeded |
| `Desktop.Native` | net8.0 | — | **0** | ✅ Build succeeded |
| `Desktop.Shared` | net8.0 | — | **0** | ✅ Build succeeded |
| `Desktop.UI` | net8.0 | — | **0** | ✅ Build succeeded |
| `Desktop.Win` | net8.0-windows | — | **0** | ✅ Build succeeded |
| `Desktop.Linux` | net8.0 | — | **0** | ✅ Build succeeded |
| **`Server`** | net8.0 | ❌ (بعد `71b890b9`) | **0** | ✅ **Build succeeded** |
| `Tests/Shared.Tests` | net8.0 | — | **0** | ✅ Build succeeded |
| `Tests/Server.Tests` | net8.0 | — | **0** | ✅ Build succeeded |
| `Tests/Desktop.Win.Tests` | net8.0-windows | — | **1** | ❌ **Build FAILED** |

### 6.1 `Tests/Desktop.Win.Tests` — عطب حقيقي في المصدر

```
ManualTests.cs(4,24):  error CS0234: The type or namespace name '…' does not exist
ManualTests.cs(12,30): error CS0234: The type or namespace name '…' does not exist
ManualTests.cs(42,17): error CS0122: 'ScreenCaster' is inaccessible due to its protection level
```

⇒ **مشروع الاختبارات الوحيد خارج الحل لا يُترجم أصلاً.** `[RUNTIME-VERIFIED]`
**الأثر:** لا أثر على بناء الحل (المشروع خارج `Remotely.sln`)، لكن ⇒ **لا يمكن أن تعمل تغطية الاختبارات الخاصة بـ Windows** ما دام هذا المشروع لا يُبنى. القرار: **إصلاحه** (يعيد تغطية `Desktop.Win`) **أو حذفه صراحة** — لا يمكن بقاؤه "مُقفلاً" بصمت.

---

## 7. اكتشاف وتشغيل الاختبارات

### 7.1 على خط الأساس (.NET 8) — ⛔ BLOCKED

```
dotnet test Tests/Shared.Tests --no-build
dotnet test Tests/Server.Tests --no-build
  → Testhost process for source(s) '…Remotely.Shared.Tests.dll' exited with error:
    You must install or update .NET to run this application.
    Framework: 'Microsoft.NETCore.App', version '8.0.0' (x64)
    The following frameworks were found:  6.0.11 / 9.0.17 / 10.0.14
  → Test Run Aborted.
```

**لا يُصنَّف هذا "فشل اختبارات" ولا "نجاح" — بل `BLOCKED`.** `[RUNTIME-VERIFIED]`

### 7.2 تشخيص roll-forward (‏**ليس إثباتاً لخط الأساس**)

`DOTNET_ROLL_FORWARD=Major dotnet test --no-build --logger trx`

| المشروع | مُكتشَف | مُنفَّذ | ناجح | فاشل | مُتخطّى |
|---|---|---|---|---|---|
| `Shared.Tests` | 1 | **1** | **1** | 0 | 0 |
| `Server.Tests` | 18 | **18** | **18** | 0 | 0 |
| **المجموع** | **19** | **19** | **19** | 0 | 0 |

**أدلة TRX (آلية):** `.p00b-lab/TestResults/Tests_Server_Tests_Server_Tests_csproj.trx` (2,071,990 بايت) · `Tests_Shared_Tests_Shared_Tests_csproj.trx` (2,479 بايت) — كلاهما `total/executed/passed = 18/18/18` و`1/1/1`.

### 7.3 خارج الحل والمُتخطّى

| البند | القيمة |
|---|---|
| `Tests/Desktop.Win.Tests` | **خارج `Remotely.sln`** · 7 `[TestMethod]` كلها `[Ignore("Manual test.")]` · **لا تُترجم** (§6.1) ⇒ 0 منها يمكن أن تعمل |
| `Tests/LoadTester` | أداة console، ليست مجموعة اختبارات |
| النتيجة | 19 سطر اختبار في الحل، 7 خارج الحل ⇒ **26 سطراً في المصدر** (مطابق لـE-34 في P00-A) |

### 7.4 ما الذي لم يُنفَّذ

| # | ما لم يُنفَّذ | السبب |
|---|---|---|
| N-01 | **تشغيل الاختبارات على .NET 8.0.33** | runtime غير مثبَّت (يحتاج تصريح §3.2) |
| N-02 | `Desktop.Win.Tests` | خارج الحل + لا يُترجم + كله `[Ignore]` |
| N-03 | أي اختبار على Windows فعلي (سلوك، صلاحيات، سطح مكتب) | لا جهاز Windows تمثيلي في هذه البيئة |
| N-04 | تشغيل CI على GitHub | لا صلاحية push ولا remote |

---

## 8. مصفوفة ثغرات الاعتماديات (NU1903/NU1902)

**الأداة:** `dotnet list <project> package --vulnerable --include-transitive`

### 8.1 `Server` — 12 حزمة متأثرة

| الحزمة | النسبة | الإصدار المُحل | الخطورة | النطاق المتأثر | المعرّف | المكوّن القابل للوصول | التعرّض التشغيلي | مُصلَح في |
|---|---|---|---|---|---|---|---|---|
| `MessagePack` | **مباشرة** (عبر `Shared`) | 2.5.187 | **High** | < 3.0.0 | GHSA-hv8m-jj95-wg3x | **تسلسل MessagePack على الخادم والوكيل** | **شبكي** — مدخلات غير موثوقة من العميل تُفكّ | 2.5.190+ / 3.x |
| `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` | **مباشرة** | 8.0.10 | **High** | ≤ 8.0.x | GHSA-f8h2-vmm9-qhj6 | **نقل SignalR + MessagePack (متصفح ↔ سيرفر)** | **شبكي** | 8.0.11+ |
| `Microsoft.AspNetCore.SignalR.Common` | **مباشرة** | 8.0.10 | **High** | ≤ 8.0.x | GHSA-334f-vvp4-7g6p | **نقل SignalR نفسه** | **شبكي** | 8.0.11+ |
| `System.Security.Cryptography.Pkcs` | شفافة | 8.0.0 | **High** | < 8.0.x | GHSA-846f-f764-76mx | توقيع/تحقّق PKCS#12 (مصادقة API) | **شبكي عند استخدام مفاتيح** | 8.0.1+ |
| `SQLitePCLRaw.lib.e_sqlite3` | شفافة | 2.1.6 | **High** | < 2.1.7 | GHSA-2m69-gcr7-jv3q | **مكتبة SQLite الأصلية** (SQLite provider) | محلي/قاعدة بيانات | 2.1.7+ |
| `Microsoft.Build` | شفافة (**بناء فقط**) | 17.8.3 | **High** | < 17.11.31 | GHSA-w3q9-fxm7-j8fq | `Microsoft.EntityFrameworkCore.Tools` → **وقت البناء فقط** | **غير مُعرَّض زمن التشغيل** | 17.11.31+ |
| `MailKit` | **مباشرة** | 4.8.0 | Moderate | ≤ 4.9.0 | GHSA-9j88-vvj5-vhgr | إرسال بريد SMTP (تنبيهات) | شبكي (خادم SMTP خارجي) | 4.14.0+ |
| `MimeKit` | شفافة (MailKit) | 4.8.0 | Moderate | ≤ 4.9.0 | GHSA-g7hc-96xr-gvvx | تحليل MIME | شبكي | 4.14.0+ |
| `Azure.Identity` | شفافة | 1.10.3 | Moderate | < 1.11.4 | GHSA-wvxc-855f-jvrv | معرّفات Azure (مصادقة) | شبكي عند الاستخدام | 1.11.4+ |
| `Microsoft.Identity.Client` | شفافة | 4.56.0 | Low | < 4.66.2 | GHSA-x674-v45j-fwxw | MSAL | شبكي | 4.66.2+ |
| `NuGet.Packaging` / `NuGet.Protocol` | شفافة (**بناء فقط**) | 6.11.0 | Low | < 6.11.1 | GHSA-g4vj-cjjj-v7hg | `EF Core Tools` / `Containers.Tools` | **غير مُعرَّض زمن التشغيل** | 6.11.1+ |

### 8.2 بقية المشاريع

| المشروع | الحزم المتأثرة (الخطورة) |
|---|---|
| `Shared` | `MessagePack 2.5.187` — **High** (GHSA-hv8m-jj95-wg3x) |
| `Agent` | `SignalR.Protocols.MessagePack 8.0.10` **High** · `SignalR.Common 8.0.10` **High** · `MessagePack 2.5.187` **High** · `System.Security.Cryptography.Pkcs 8.0.0` **High** · `System.IO.Packaging 8.0.0` **High** (GHSA-f32c-w444-8ppv) · `System.Security.Cryptography.Xml 8.0.1` **High** (GHSA-37gx-xxp4-5rgx) |
| `Desktop.Shared` | `SignalR.Protocols.MessagePack 8.0.10` **High** · `SignalR.Common 8.0.10` **High** · `MessagePack 2.5.187` **High** |
| `Desktop.Win` | `MessagePack 2.5.187` **High** · `SignalR.Common 8.0.10` **High** · `SignalR.Protocols.MessagePack 8.0.10` **High** · `System.Net.Http 4.3.0` **High** (GHSA-7jgj-8wvc-jh57) · `System.Text.RegularExpressions 4.3.0` **High** (GHSA-cmhx-cq75-c4mj) · `Tmds.DBus.Protocol 0.16.0` **High** (GHSA-xrw6-gwf8-vvr9) |

### 8.3 التوصية — حسب **الاستغلال والتعرّض** لا البطاقة وحدها

| الأولوية | الحزمة | السبب | الإجراء |
|---|---|---|---|
| **P0 — عاجل** | `MessagePack 2.5.187` | **الاستغلال الفعلي**: فكّ MessagePack لبيانات **يصلها مهاجم** على مقبس WebSocket المفتوح. لا يوجد عزل ⇒ فعلاً قابل للاستغلال | ترقية إلى `3.x` أو `2.5.190+` — **يتطلّب فحص توافق ترميز** (الرسائل تُفكّ/تُرمَّز بين الخادم والمتصفح) |
| **P0 — عاجل** | `SignalR.Common` + `SignalR.Protocols.MessagePack` 8.0.10 | **نقل مفتوح على الإنترنت**؛ الخلل في طبقة النقل نفسها | ترقية `8.0.11+` — ترقية تصحيحية داخل نفس الإصدار الرئيسي |
| **P1** | `System.Security.Cryptography.Pkcs` · `System.Security.Cryptography.Xml` · `System.IO.Packaging` 8.0.0 | مصادقة/توقيع — التعرّض مشروط باستخدام ميزات OpenSSL على ويندوز | ترقية `8.0.x` الأحدث |
| **P1** | `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 | مكتبة أصلية في مسار قاعدة البيانات | ترقية `2.1.7+` |
| **P2** | `MailKit`/`MimeKit` 4.8.0 | Moderate + تعرّض SMTP | ترقية `4.14+` |
| **P2** | `System.Net.Http 4.3.0` · `System.Text.RegularExpressions 4.3.0` · `Tmds.DBus.Protocol 0.16.0` (Desktop.Win) | شفافة قديمة على Windows؛ لا مسار إشعارات موثّق مستخدَم | ترقية أو تبرير |
| **P3 — لا إجراء** | `Microsoft.Build` · `NuGet.*` | **وقت بناء فقط** — ليست في الصورة المنشورة | ترقية مع ترقية EF Tools |

> ⚠️ **لم أُرقِّ أي حزمة** (قاعدة صريحة). الأرقام أعلاه **أدلة**، وليستDirective. كما هو مطلوب: **لا إخفاء ولا تخفيض للخطورة**.
> **ملاحظة توافق:** ترقية `MessagePack` إلى `3.x` في **.NET** ليست نفس حزمة npm — لكنها **تغيير_major في ترميز الرسالة** ⇒ يلزم اختبار ذهاب وإياب (round-trip) بين `Server` والمتصفح. لا يجوز رفعها كـ"ترقية بسيطة".

---

## 9. جاهزية الترحيل إلى .NET 10 (تقييم فقط — **لم يُنفَّذ أي ترحيل**)

**حالة .NET 10:** طور `active` · EOL 2028-11-14 · runtime `10.0.14` **مثبَّت بالفعل** على الجهاز · SDK 10 غير مثبَّت (SDK المثبَّت 9.0.315 ⇒ `net10.0` غير قابل للبناء الآن).

| المجال | الحالة في المستودع | جاهزية .NET 10 | الفجوة |
|---|---|---|---|
| **ASP.NET Core + Blazor** | `net8.0` · `AddRazorComponents()` + `AddInteractiveServerComponents()` + `AddInteractiveServerRenderMode()` (Blazor Server تفاعلي) · `Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.10` · `Swashbuckle 6.9.0` | 🟡 متوسطة | حزمة ASP.NET Core 10 + Identity 10 + Swashbuckle متوافق؛ **Blazor Server تفاعلي** يحتاج مراجعة `InteractiveServerRenderMode` + `@rendermode` |
| **SignalR + MessagePack** | `SignalR.Client 8.0.10` · `SignalR.Protocols.MessagePack 8.0.10` · `MessagePack 2.5.187` | 🔴 منخفضة | ترقية `10.x` + `MessagePack 3.x` معاً ⇒ **تغيير_major في ترميز السلك** |
| **EF Core + 3 مزوّدات** | `EFCore 8.0.10` · `Sqlite`/`SqlServer`/`Npgsql` `8.0.10` · `Tools 8.0.10` · 3 مجلدات ترحيل | 🟡 متوسطة | حزمة EF Core 10 + ثلاث حزم مزوّدات؛ **المزوّدات الثلاثة** يجب أن ترقى معاً |
| **Windows Service** | `AspNetCore.Hosting.WindowsServices 8.0.10` · `Extensions.Hosting.WindowsServices 8.0.1` · `System.ServiceProcess.ServiceController 8.0.1` · `Agent/Program.cs:29` `.UseWindowsService()` | 🟢 عالية | متوافق API؛ **Session 0 يبقى كما هو** (لا تغيير في هوية العملية) |
| **Avalonia** | `Avalonia 11.1.4` (+Desktop/Themes/Fonts) | 🔴 منخفضة | Avalonia 11.1 **لا يدعم `net10.0`** ⇒ يلزم Avalonia ≥ 11.3 |
| **Windows desktop APIs** | `System.IO.FileSystem.AccessControl 5.0.0` (في `Desktop.Core` الميت فقط) · `NAudio 2.2.1` · `SharpDX 4.2.0` + `Direct3D11`/`DXGI` · `SkiaSharp 2.88.8` | 🟡 متوسطة | `System.IO.FileSystem.AccessControl 5.0.0` **حزمة Important-vulnerable**؛ SharpDX/NAudio قد تحتاج بدائل مُحدَّثة |
| **PowerShell SDK** | `Microsoft.PowerShell.SDK 7.4.5` + `Microsoft.WSMan.*` 7.4.5 + `System.Management.Automation 7.4.5` | 🟡 متوسطة | PowerShell SDK 7.4 targeting `net8.0`؛ لـ`net10.0` يلزم 7.5.x — **وWSMan يتطلّب WinRM/WSMan** |
| **اعتماديات أصلية** | `SkiaSharp 2.88.8` (SkiaSharp native) · `SharpDX` (D3D11) · `NAudio` (WASAPI/MMDevice) | 🔴 منخفضة | هذه **أصول أصلية وقت التشغيل**؛ لا توجد طبقة توافق مُدارة — كلٌّ منها يحتاج فحص أصل x64 على Windows 10/11 |
| **Windows 10/11 editions** | `net8.0-windows` بلا `SupportedOSPlatformVersion` ⇒ افتراضي 7.0 | 🟢 عالية | لا `UseWindowsForms`/`UseWPF` (Avalonia) ⇒ لا اعتماد على WPF/WinForms |
| **Windows Server hosting** | `Serilog.AspNetCore 8.0.3` · `Microsoft.VisualStudio.Azure.Containers.Tools.Targets 1.21.0` (نشر حاوية) · `docker-compose` | 🟡 متوسطة | حاوية الخادم على `aspnet:8.0`؛ عند الترحيل ⇒ `aspnet:10.0` + إعادة بناء الصورة |
| **CI + مُشغّلات الاختبار** | `setup-dotnet@v3` بـSDK 7 (سابقاً) ⇒ **مُصحَّح في `71b890b9` إلى `@v4` بـ`8.0.x`** · `Microsoft.NET.Test.Sdk 17.11.1` (Server/Shared) و`17.3.1` (Desktop.Win.Tests) · `MSTest 3.6.1` و`2.2.10` | 🟡 متوسطة | على .NET 10 يلزم `Microsoft.NET.Test.Sdk` ≥ 17.12 / `MSTest` ≥ 3.8؛ **`Desktop.Win.Tests` بـ`MSTest 2.2.10` قادم من .NET 6** |

### 9.1 فصل: عيوب البناء القائمة **≠** مخاطر الترحيل

| العيب القائم | هل هو عيب ترحيل؟ | ملاحظة |
|---|---|---|
| غياب runtime 8.0 | ❌ **لا** | بيئي بحت |
| `LIB002` | ❌ **لا** | منطق مزوّد LibMan + سجل npm؛ **لا علاقة له بـ .NET 10** — لكن، تنبيه: نفس الخلل سيحدث على .NET 10 لو بقي المزوّد |
| `Desktop.Win.Tests` لا يُترجم | ❌ **لا** | عيب مصدر قائم |
| `NU1903` × 12 | ❌ **لا** | عيوب اعتماديات قائمة — الترحيل لا يحلّها، ويزيدها |
| CI بـSDK 7 | ❌ **لا** | مُصحَّح في `71b890b9` |
| `MessagePack 2.5.187` | ⚠️ **نعم** | الترحيل إلى SignalR 10 يجرّ `MessagePack 3.x` ⇒ فرصة Kill-Two-Birds: يُغلق فجوة `NU1903` ويُزيح مقصد الترحيل |

### 9.2 تسلسل ترحيل مقترح (بعد تثبيت خط الأساس)

| # | الخطوة | لماذا أول |
|---|---|---|
| 0 | تثبيت خط الأساس على .NET 8.0.33 (§3.2) | بلا خط أساس، لا معنى للترحيل |
| 1 | إغلاق `NU1903` P0/P1 (§8.3) | أمان قبل масс |
| 2 | `Server` + `Shared` + `Agent` → `net10.0` | قلّة الحزم |
| 3 | `Desktop.*` + Avalonia ≥ 11.3 | أكبر اعتماد أصلي |
| 4 | حاوية `aspnet:10.0` + CI على `10.0.x` | نشر |
| 5 | `Windows` 10/11 × أجهزة الاختبار + Serilog + WSMan | تحقق ميداني |

---

## 10. الإصلاحات الدنيا المقترحة مع فروق الملفات بالضبط

**لا شيء ممّا يلي مُطبَّق.** كل فِرق точّي ومُراجَع. `D-1` مُنفَّذ من المالك بالفعل ⇒ مُوثَّق للمراجعة فقط.

### D-0 — مُنفَّذ من المالك في `71b890b9` (للمراجعة)

```diff
--- a/Server/libman.json
+++ b/Server/libman.json
@@
   "version": "1.0",
   "defaultProvider": "cdnjs",
+  // All client libraries (microsoft-signalr, msgpack, signalr-protocol-msgpack,
+  // font-awesome) are committed under wwwroot/lib/.  LibMan restore was removed
+  // entirely: the unpkg entries failed with LIB002 (2.1.175), and the cdnjs
+  // entries rewrote committed assets with LF endings on every Windows build,
+  // leaving a dirty git tree.  Regenerate them manually only when bumping a
+  // library version.
   "libraries": []
```
```diff
--- a/.github/workflows/run_tests.yml
+++ b/.github/workflows/run_tests.yml
@@
-    - name: Install .NET
-      uses: actions/setup-dotnet@v3.0.3
-      with:
-        dotnet-version: 7
+    # Install the .NET workload (projects target net8.0)
+    - name: Install .NET
+      uses: actions/setup-dotnet@v4
+      with:
+        dotnet-version: 8.0.x
```
✅ **تصحيح**: `NETSDK1045` مُغلق · Dirty-Tree مُغلق · `LIB002` مُغلق.

### D-1 — إزالة ارتباط LibMan من البناء (مقترح)

```diff
--- a/Server/Server.csproj
+++ b/Server/Server.csproj
@@
-    <PackageReference Include="Microsoft.Web.LibraryManager.Build" Version="2.1.175" />
```
**السبب:** الحزمة لم تعد تفعل شيئاً (`"libraries": []`) لكنها ما زالت تُشدّ هدف `LibraryManagerRestore` في كل بناء وتربطه بـNuGet. إزالتها تُنهي الاعتماد على `Microsoft.Web.LibraryManager.Build` (حزمة متروكة منذ 2019) نهائياً.
**المخاطرة:** لو أُعيد أي إدخال لـ`libman.json` فلن يُسترجَع تلقائياً ⇒ يُقابله D-2 (توثيق الأصول).
**التراجع:** `git checkout -- Server/Server.csproj`.

### D-2 — إغلاق فجوة المسار GAP-15 (مقترح — الأهم Remaining)

```diff
--- a/Server/Pages/Viewer.cshtml
+++ b/Server/Pages/Viewer.cshtml
@@
-                src="/lib/microsoft-signalr-protocol-msgpack/dist/browser/signalr-protocol-msgpack.js"></script>
+                src="/lib/microsoft/signalr-protocol-msgpack/dist/browser/signalr-protocol-msgpack.js"></script>
@@
-                src="/lib/microsoft-signalr-protocol-msgpack/dist/browser/signalr-protocol-msgpack.min.js"></script>
+                src="/lib/microsoft/signalr-protocol-msgpack/dist/browser/signalr-protocol-msgpack.min.js"></script>
```
**الأساس:** الأصول المُلتزَمة في `Server/wwwroot/lib/microsoft/signalr-protocol-msgpack/dist/browser/` (4 ملفات، مُتحقَّق منها بـ`git ls-files`) ⇒ الطلب الحالي `/lib/microsoft-signalr-protocol-msgpack/...` ⇒ **404**.
**بديل مكافئ:** `git mv Server/wwwroot/lib/microsoft Server/wwwroot/lib/microsoft-signalr-protocol-msgpack` (تصحيح في المصدر لا في الطلب).
**التراجع:** `git checkout -- Server/Pages/Viewer.cshtml`.
**لم يُطبَّق:** لأن `Viewer.cshtml` ملف إنتاج.

### D-3 — توثيق الأصول المُلتزَمة (مقترح — يعالج فجوة التتبّع P1)

```diff
--- /dev/null
+++ b/Server/wwwroot/lib/README.md
@@
+# Committed client libraries
+
+These assets are committed and are NOT restored by LibMan (see Server/libman.json).
+Regenerate manually when bumping a version.
+
+| Path | Package | Version | Downloaded |
+|---|---|---|---|
+| `msgpack/dist.es5+umd/` | `@msgpack/msgpack` | 3.0.0-beta2 | 2024-xx-xx |
+| `microsoft/signalr-protocol-msgpack/dist/browser/` | `@microsoft/signalr-protocol-msgpack` | 8.0.7 | 2024-xx-xx |
+| `microsoft-signalr/` | `microsoft-signalr` | 8.0.7 | cdnjs |
+| `fontawesome/` | `font-awesome` | 6.5.2 | cdnjs |
```
**السبب:** بعد D-0/‏`71b890b9` لم تعد `libman.json` تصف هذه الأصول ⇒ لا pinning ولا تاريخ. ⇒ `PROPOSED`.

### D-4 — حسم مصير `Tests/Desktop.Win.Tests` (مقترح — قرار لا patch)

```diff
--- a/Tests/Desktop.Win.Tests/ManualTests.cs
+++ b/Tests/Desktop.Win.Tests/ManualTests.cs
@@
-    [TestMethod]
-    public void ScreenCaster_...()
+    [TestMethod]
+    public void ScreenCaster_...()   // requires a fix to the missing type
```
**الخيارات:** (أ) إصلاح المراجع (`ScreenCaster` غير متاح في `Desktop.Shared` الحالي) **و** إدراج المشروع في `Remotely.sln` **و** إزالة `[Ignore]`؛ (ب) حذف المشروع بصراحة. ⇒ **قرار للمالك**؛ لا يُترك كما هو لأن "خارج الحل + لا يُترجم + مُتخطّى" يعني تغطية Windows = صفر.

### D-5 — تثبيت runtime 8.0.33 معزولاً (مقترح — **يحتاج تصريح**)

```powershell
$ErrorActionPreference = 'Stop'
$dir = 'C:\Users\Administrator\Documents\smacc_TEST\dotnet8'
New-Item -ItemType Directory -Path $dir -Force | Out-Null
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile "$dir\install.ps1"
& "$dir\install.ps1" -Channel 8.0 -Version 8.0.33 -InstallDir $dir -Quality GA
& "$dir\dotnet.exe" --list-runtimes
& "$dir\dotnet.exe" test "$PWD\Tests\Server.Tests\Server.Tests.csproj" --logger trx
```
**لم يُنفَّذ** — تثبيت برنامج يحتاج تصريحاً صريحاً، وEOL بعد 4 أيام ⇒ **مؤقّت**.

---

## 11. المخاطر وADR والتراجع

### 11.1 ADR

| ID | القرار | البدائل المرفوضة | السبب |
|---|---|---|---|
| **ADR-P00B-01** | **إبقاء LibMan مُعطَّلاً عبر `"libraries": []`** بدل إعادة تفعيله بمزوّد jsDelivr | (أ) jsDelivr — *مُختبَر وناجح* (E10/E11) · (ب) تثبيت الإصدار إلى `latest` — *لا يعمل* (E4/E9) | jsDelivr يعمل لكنه **يغيّر بايتات الأصول المُسلَّمة** (§5.6) ⇒ تغيير مرئي في الإنتاج مع كل بناء ⇒ يحتاج قرار المالك |
| **ADR-P00B-02** | **عدم ترقية أي اعتمادية في هذه المرحلة** | ترقية `MessagePack` فوراً | الترقية Major في ترميز السلك ⇒ تحتاج اختبار round-trip؛ **خارج نطاق P00-B** |
| **ADR-P00B-03** | **عدم تثبيت runtime 8 دون تصريح** | `winget install Microsoft.DotNet.Runtime.8` | تغيير نظامي + EOL خلال 4 أيام ⇒ مسار معزول ومؤقّت هو الخيار الأقل أثراً |
| **ADR-P00B-04** | **عدم لمسّ تعديلات المالك الخمسة** | "تنظيف" الشجرة | قاعدة صريحة: لا تُسقط تغييرات المالك؛ هي في طور التعديل والتحقق |
| **ADR-P00B-05** | **تسجيل نتيجة roll-forward كتشخيص لا كإثبات** | إعلان "الاختبارات ناجحة" | صريحة في التكليف؛ و`[TEST-EVIDENCE]` على .NET 9 ≠ خط أساس .NET 8 |

### 11.2 المخاطر

| ID | الخطر | الاحتمال | الأثر | التخفيف |
|---|---|---|---|---|
| RB-1 | **تثبيت .NET 8 ثم اكتشاف أن EOL خلال 4 أيام** ⇒ إعادة العمل مرتين | عالٍ | متوسط | ADR-P00B-03: تثبيت معزول ومؤقّت + CI على 8.0.x أولاً (وهو المُصحَّح فعلاً) |
| RB-2 | **‏`MessagePack 2.5.187` قابل للاستغلال على مقبس مفتوح** | متوسط | **عالٍ** | P0 في §8.3 — يُعالَج بترقية مُنسَّقة مع اختبار round-trip |
| RB-3 | **إعادة إدخال LibMan** لاحقاً ⇒ Dirty-Tree من جديد | متوسط | متوسط | D-1 (إزالة المرجع) + D-3 (توثيق) |
| RB-4 | **تعديلات المالك الخمسة تدخل بلا اختبار** ⇒ كسر المصادقة | متوسط | عالٍ | اقتراح: اختبار 401/403 لكل نقطة قبل الدمج |
| RB-5 | **‏GAP-15 (404) يبقى** ⇒ العميل MessagePack لا يعمل في الإنتاج | **عالٍ** | عالٍ | D-2 — تصحيح سطرين |
| RB-6 | **‏`Desktop.Win.Tests` يبقى متروكاً** ⇒ تغطية Windows = صفر | عالٍ | متوسط | D-4 — قرار |

### 11.3 التراجع

| التغيير | التراجع |
|---|---|
| D-1 (إزالة مرجع LibMan) | `git checkout -- Server/Server.csproj` |
| D-2 (مسار Viewer.cshtml) | `git checkout -- Server/Pages/Viewer.cshtml` |
| D-3 (README) | `git rm Server/wwwroot/lib/README.md` |
| D-5 (runtime معزول) | `rm -- C:\Users\Administrator\Documents\smacc_TEST\dotnet8` (إلى سلة المحذوفات) |
| كل تعديلات المالك | **لا تراجع** — لا يملكها الوكيل |

---

## 12. حالة البوابات

| البوابة | الحالة | السبب |
|---|---|---|
| **بناء الحل** | ✅ **PASS** | 10/11 مشروعاً · `Server` PASS على الحالة المُثبَّتة |
| **اختبارات على خط الأساس .NET 8** | ⛔ **BLOCKED** | لا Microsoft.NETCore.App 8.0.x |
| **اكتشاف الاختبارات** | ✅ **PASS** | 19 مُكتشَف في الحل · 7 خارج الحل (لا تُترجم) |
| **نتيجة الاختبارات (تشخيص .NET 9)** | ✅ **PASS** (تشخيص) | 19/19 ناجحة · TRX مُرفق |
| **قاعدة الأدلة** | ⛔ **BLOCKED** | 12 حزمة متأثرة (6 High) في `Server` |
| **LIB002** | ✅ **PASS** (بعد `71b890b9`) | الجذر مؤكَّد (§5.4) والإصلاح مُنفَّذ ومُتحقَّق |
| **CI** | ✅ **PASS** | `setup-dotnet@v4` بـ`8.0.x` ⇒ `NETSDK1045` مُغلق |
| **Dirty-Tree من البناء** | ✅ **PASS** | شجرة نظيفة بعد البناء |
| **جاهزية .NET 10** | ⛔ **UNKNOWN** | تقييم فقط (§9) — لا SDK 10، لا ترحيل |
| **سلوك Windows الحقيقي** | ⛔ **UNKNOWN** | لا جهاز Windows ممثّل في هذه البيئة |

### 12.1 بوابة مرحلة P00-B

| الشرط | الحالة |
|---|---|
| فصل أعطال البيئة / الاستعادة / البناء / التشغيل / الاختبارات | ✅ **PASS** |
| جذر مؤكَّد أو موسوم `UNKNOWN` | ✅ **PASS** — LIB002 **مؤكَّد** (§5.4) |
| خط أساس بناء ناجح | ✅ **PASS** |
| أدلة اختبار آلية (TRX) | ✅ **PASS** — 19/19 على .NET 9 (تشخيص) |
| خط أساس .NET 8 قابل للتنفيذ | ⛔ **BLOCKED** — يحتاج تثبيتاً (D-5) |
| لا كود إنتاج مُعدَّل | ✅ **PASS** — تعديلات المالك الخمسة لم تُلمس |

### 🔶 **حالة بوابة P00-B: PASS مشروط**

> **ما لا يقوله هذا PASS:** البناء يعمل، والاختبارات تنجح عند roll-forward — لكن **على .NET 9، لا .NET 8**. و**لا** يمكن اعتماد "الاختبارات ناجحة" بوصفها إثباتاً لخط الأساس التاريخي. و**‏12 ثغرة عالية الخطورة ما زالت مفتوحة**. البوابات المستقلة (G1–G7 في `GATE-REGISTER.md`) لم تتغيّر.

---

## 13. الإجراء التالي الموصى به (واحد)

> **إغلاق فجوة مسار الأصل `signalr-protocol-msgpack` (D-2): تصحيح سطرين في `Server/Pages/Viewer.cshtml`.**
>
> **لماذا هو الواحد:** هو العيب الوحيد في القائمة الذي (أ) **مُثبَت بالدليل** — الأصول في `wwwroot/lib/microsoft/signalr-protocol-msgpack/…` والطلب في `/lib/microsoft-signalr-protocol-msgpack/…` ⇒ 404 مؤكد؛ (ب) **لا يحتاج تثبيتاً ولا شبكة ولا اعتماديات** — لا `.NET 8` ولا تفاعل مع MALL؛ (ج) **يُغلق مساراً وظيفياً مكسوراً الآن** (بروتوكول MessagePack لا يُحمَّل في المتصفح)؛ (د) **خطره صفري** (سطران، تراجع بـ`git checkout`).
>
> **بقية القائمة تحتاج ب-owner قراراً أو تصريحاً** — تثبيت runtime (D-5) وانتظار التصريح · ترقيات `NU1903` (تحتاج اختبار round-trip) · مصير `Desktop.Win.Tests` (قرار) · توثيق الأصول (D-3، بعد تثبيت D-1).

---

*لم يُعدَّل أي ملف إنتاج · لم يُثبَّت أي برنامج · لم تُرقَ أي اعتمادية · لم يُغيَّر أي إطار عمل · لم تُعدَّل الـconstitution · لم يبدأ تنفيذ SPEC-001 · لا نشر ولا commit ولا push · لم يُستخدم `git add -A` · كل تعديلات المالك محفوظة.*

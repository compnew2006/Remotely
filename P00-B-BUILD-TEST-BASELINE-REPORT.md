# P00-B — تقرير خط أساس البناء والاختبار

**المشروع:** Remotely Capture Extension (fork of `immense/Remotely`)
**الشجرة المُقيَّمة:** `master @ e67900d6` + **ترقية اعتماديات غير مُثبَّتة من المالك** (5 ملفات `.csproj`) · **الأساس المُفحوص:** `df85026e` → `e67900d6` (4 commits من المالك أثناء الجلسة)
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
| **قاعدة الأدلة** | ✅ **مُغلقة في `Server`** بعد ترقية المالك — **صفر** ثغرة وقت تشغيل · المتبقّي: 3 حزم بناء فقط + 1 High في `Agent` + 3 شفافة في `Desktop.Win` · **وأصول المتصفح 8.0.7 لم تُرقَّ** (F-02) |
| **بوابة P00-B** | 🔶 **PASS مشروط** — خط الأساس **قابل للبناء**، لكنه **غير قابل للتوثيق على .NET 8** بعد 4 أيام من نهاية دعمه |

**ثلاث حقائق يجب أن تُقال أولاً:**

1. **لا يوجد "فشل اختبارات" هنا — يوجد "تعذّر تشغيل".** بـ`DOTNET_ROLL_FORWARD=Major` تعمل **19/19** اختباراً (TRX مُرفق). الكود سليم؛ الناقص هو runtime فقط. `[RUNTIME-VERIFIED]`
2. **‏.NET 8 يخرج عن الدعم في 2026-11-10 — بعد أربعة أيام.**أحدث runtime هو `8.0.33` والـSDK `8.0.427`. أي تثبيت الآن هو **إجراء مؤقت مُؤجَّل EOL**، لا هدف إنتاج. `[RUNTIME-VERIFIED]`
3. **المالك يعمل بالتوازي في نفس الملفات.** أثناء هذه الجلسة: commit ثالث (`71b890b9`) + commit رابع (`e67900d6`) **ضمّن فيه تعديلات الإنتاج الخمسة** في `Server/API/*` و`Server/Auth/*`. لم ألمسها ولم أُسقطها. (تفصيل §2.3).

### 1.1 سجلّ التصحيحات بعد الإصدار الأول (Correction Log)

| ID | التاريخ | الادّعاء المُصحَّح | التصحيح | الدليل |
|---|---|---|---|---|
| **C-09** | 2026-11-06 | «‏GAP-15: مسار `signalr-protocol-msgpack` مكسور ⇒ 404» — الوارد في النسخة الأولى من هذا التقرير (وتعزى إليه D-2 وRB-5) | **سالب — الفرضية ملغاة.** الطلب `/lib/microsoft/signalr-protocol-msgpack/...` **مطابق** للوجهة `wwwroot/lib/microsoft/signalr-protocol-msgpack/` ⇒ لا 404 | `Server/Pages/Viewer.cshtml:33,38` · `Server/wwwroot/lib/microsoft/signalr-protocol-msgpack/dist/browser/` (4 ملفات مُلتزَمة) · **كل** مراجع `/lib/` التسعة في `Server/` تُحَلّ إلى ملف قائم `[SOURCE-VERIFIED]` |
| **C-10** | 2026-11-06 | «‏`Server` = 12 حزمة متأثرة (6 High) ما زالت مفتوحة» — الوارد في النسخة الأولى من هذا التقرير | **محدَّث بعد ترقية المالك للاعتماديات:** صفر High/Moderate/Low تمسّ وقت التشغيل في `Server`؛ المتبقّي 3 حزم **بناء فقط**. وأُضيف F-02 (انحراف إصدار أصول المتصفح عن الخادم) | `dotnet list package --vulnerable --include-transitive` بعد الترقية · §8.1 · §8.2 · §8.4 · §8.5 |

> **جذر الخطأ الذي وقعت فيه:** بحثتُ عن وجود `wwwroot/lib/microsoft-signalr-protocol-msgpack/` — وهو **مسار لا يطلبه أي ملف** — ثم استنتجتُ 404 من غيابه. الغياب لا يُثبت عيباً حين لا يوجد طلب منه. الفحص الصحيح هو مطابقة **كل** مرجع `/lib/` بالملف الفعلي؛ وقد فُحص الآن ⇒ **تسعة من تسعة مطابقة**، و`D-2` ملغى.
>
> **الأثر:** سقط المرشَّح الوحيد لـ«الإجراء الواحد» (§13). (§5 · §10 · §11.2 · §11.3 · §12 · §13 مُحدَّثة في هذه النسخة.)
>
> **قاعدة إجرائية مُستفادة:** ادّعاء «المسار مكسور» يحتاج **جدول مطابقة** بين كل طلب وكل ملف — لا بحثاً عن مجلّد باسم مفترض.

---

## 2. سلامة مساحة العمل

### 2.1 الحالة عند الفتح

| القياس | القيمة |
|---|---|
| HEAD | `e67900d6 fix(server): protect API endpoints and fix fail-open authorization filters` |
| الفرع | `master` — `ahead 4` عن `origin/master` |
| Staged | **0** |
| Unstaged (الآن) | 5 ملفات `.csproj` (ترقية اعتماديات المالك) + `P00-B-BUILD-TEST-BASELINE-REPORT.md` (تصحيحات هذا التقرير) — **الـcsproj لم ألمسها**؛ والتقرير تعديل وثائق فقط |
| Untracked | **0** — لا ملفات temporية في الريبو (مختبر `.p00b-lab/` مستثنى في `.git/info/exclude`) |
| `.git/info/exclude` | `graphify-out/` · `.serena/` · `.specify.backup-*/` · `.specify.orig-*/` · `.scratch-init/` · `*.bak` · **`.p00b-lab/`** (أُضيف في هذه الجلسة) |
| `global.json` | **غير موجود** ⇒ لا تثبيت SDK |
| `Directory.Build.props` | `Nullable=enable` + `RootNamespace` فقط |
| `nuget.config` | **غير موجود** ⇒ افتراضي NuGet.org |

### 2.2 خلوّ الفهرس من الأسرار والملفات المؤقتة

فُحص الـindex بالكامل: **لا ملف `.bak` / `.env` / `.pfx` / `.key` / `secret` / `token` / `node_modules` / `*.log`** داخله. الـ`*.bak` مستثنى محلياً. ⇒ **PASS**.

### 2.3 ✅ تعديلات المالك فوق نفس الـFindings (ضُمّنت في `e67900d6`)

**خمسة ملفات إنتاج ضمّنها المالك في `e67900d6`** — لم ألمسها:

| الملف | التعديل | الفجوة التي يغلقها |
|---|---|---|
| `Server/API/CustomBinariesController.cs` | `+[ServiceFilter(typeof(ApiAuthorizationFilter))]` على `GetWinX86Desktop` و`GetWinX64Desktop` + `using Remotely.Server.Auth;` | **GAP-09** (اكتشاف P00-A) |
| `Server/Auth/ApiAuthorizationFilter.cs` | `context.Result = new UnauthorizedResult();` عند خطأ التفويض + تعليق *"Fail closed"* | **FR-009** في SPEC-001 |
| `Server/API/ClientDownloadsController.cs` | تعديل مرتبط | GAP-08 |
| `Server/API/DevicesController.cs` | تعديل مرتبط | GAP-07 |
| `Server/Auth/ExpiringTokenFilter.cs` | تعديل مرتبط | PC-2 |

> **لم أُنفِّذ ولم أُلغِ شيئاً.** هذه تعديلات المالك؛ وقد ضُمّنت في `e67900d6`. **ملاحظة فنية مُرسلت له (تبقى سارية):** `ApiAuthorizationFilter` كمرشّح `IAsyncAuthorizationFilter` — إضافة `UnauthorizedResult` عند Fault تُغلق الفجوة، لكن `ExpiringTokenFilter` و`ApiAuthorizationFilter` يلتقطان الاستثناء **داخل المرشّح**؛ المسار الذي يتجاوز المرشّحات (مثل `DevicesController.Create` إن لم يحمل سمة) يبقى مفتوحاً. يُتحقَّق بالاختبار لا بالقراءة ⇒ **مطلوب اختبار سلبي 401/403 لكل نقطة قبل إقفال G1**.

**منذ ذلك الحين:** `Server/libman.json` صار part من `71b890b9` — أي أن المالك اعتمد **إزالة LibMan بالكامل** (`"libraries": []`). (§5.6)

### 2.4 آثار جانبية للبناء (انظر §2.3 — ليست ملكاً للمالك)

البناء الأول (قبل إصلاح المالك) كان يُعيد كتابة `Server/wwwroot/lib/fontawesome/**` و`Server/wwwroot/src/*.js` بسبب إدخالات cdnjs. **هذا مؤكَّد الآن:** حذفُ إدخالات cdnjs أزال الأثر. بعد إعادة البناء على الحالة المُثبَّتة: **`git status` نظيف بالنسبة لأصول `wwwroot`** ✓. ⇒ **تعديل المالك صحيح من هذه الناحية أيضاً** `[RUNTIME-VERIFIED]`.

### 2.5 ⚠️ وجهة الدفع هي المستودع الأصلي — لا fork المالك (F-01)

| القياس | القيمة |
|---|---|
| `remote.origin.url` | `https://github.com/immense/Remotely.git` ← **المستودع الأصلي (upstream)** |
| `remote.origin.pushurl` | **غير معرَّف** ⇒ `git push` يذهب إلى `origin` افتراضياً |
| `branch.master` | يتتبّع `origin/master` |
| remotes أخرى | **لا شيء** — لا يوجد remote باسم `fork` |
| `gh` CLI | **غير مثبَّت** على هذا الجهاز |

**الأثر — ثلاثة نتائج قابلة للتحقق:**

1. **‏`git push` الافتراضي يذهب إلى `immense/Remotely`** (المستودع الأصلي) لا إلى fork المالك. المالك من ينفّذ الـcommits، لكن يجب أن يكون هدف الـpush **مقصوداً** لا افتراضياً.
2. **‏CI لا يعمل على fork المالك قبل أول push صحيح** — `run_tests.yml` يستمع إلى `push`/`pull_request` على `master` في المستودع الذي يقع فيه الـpush. ⇒ **إصلاح CI في `71b890b9` (`setup-dotnet@v4` بـ`8.0.x`) لم يُشغَّل بعد** ⇒ لا يوجد حتى الآن أي دليل اختبار على .NET 8.
3. **‏CI نفسه هو مصدر الدليل البديل على .NET 8** — `setup-dotnet@v4` يُنزّل .NET 8 SDK على الـrunner، فتُنفَّذ الاختبارات على 8.0.x **بلا تثبيت أي شيء على هذا الجهاز** ⇒ D-5 ليس المسار الوحيد (§13).

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
3. **~~الفجوة GAP-15 باقية~~ — مُلغاة (تصحيح C-09)**: طلب `Viewer.cshtml:33,38` هو `/lib/microsoft/signalr-protocol-msgpack/...` والأصول على القرص في `wwwroot/lib/microsoft/signalr-protocol-msgpack/dist/browser/` ⇒ **المسار مطابق ولا يوجد 404**. (§10 — D-2 ملغى)

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

## 8. مصفوفة ثغرات الاعتماديات (NU1903/NU1902) — قبل وبعد ترقية المالك

**الأداة:** `dotnet list <project> package --vulnerable --include-transitive`
**الحالة المُقيَّمة:** بعد ترقية المالك للاعتماديات (غير مُثبَّتة في git بعد ⇒ قِيست على القرص) ⇒ `Server` من **12 حزمة متأثرة** إلى **3**، **وكلها وقت بناء**.

### 8.1 `Server` — 12 ⇒ 3 (صفر ثغرة تمسّ وقت التشغيل)

| الحزمة | النسبة | قبل | بعد | الخطورة | المعرّف | التعرّض التشغيلي | الحالة |
|---|---|---|---|---|---|---|---|
| `MessagePack` | مباشرة (عبر `Shared`) | 2.5.187 | **2.5.307** | High | GHSA-hv8m-jj95-wg3x | **شبكي** — فكّ MessagePack لمدخلات غير موثوقة على مقبس مفتوح | ✅ **مُغلق** |
| `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` | مباشرة | 8.0.10 | **8.0.33** | High | GHSA-f8h2-vmm9-qhj6 | **شبكي** — نقل SignalR+MessagePack | ✅ **مُغلق** |
| `Microsoft.AspNetCore.SignalR.Common` | شفافة | 8.0.10 | **8.0.33** | High | GHSA-334f-vvp4-7g6p | **شبكي** — نقل SignalR | ✅ **مُغلق** |
| `System.Security.Cryptography.Pkcs` | شفافة | 8.0.0 | — | High | GHSA-846f-f764-76mx | توقيع/تحقّق PKCS#12 | ✅ **مُغلق** (لم يعد مُبلَّغاً عنه) |
| `SQLitePCLRaw.lib.e_sqlite3` | شفافة | 2.1.6 | — | High | GHSA-2m69-gcr7-jv3q | مكتبة SQLite الأصلية | ✅ **مُغلق** (لم يعد مُبلَّغاً عنه) |
| `MailKit` | مباشرة | 4.8.0 | **4.18.2** | Moderate | GHSA-9j88-vvj5-vhgr | إرسال SMTP | ✅ **مُغلق** |
| `MimeKit` | شفافة (MailKit) | 4.8.0 | — | Moderate | GHSA-g7hc-96xr-gvvx | تحليل MIME | ✅ **مُغلق** (لم يعد مُبلَّغاً عنه) |
| `Azure.Identity` | شفافة | 1.10.3 | — | Moderate | GHSA-wvxc-855f-jvrv | معرّفات Azure | ✅ **مُغلق** (لم يعد مُبلَّغاً عنه) |
| `Microsoft.Identity.Client` | شفافة | 4.56.0 | — | Low | GHSA-x674-v45j-fwxw | MSAL | ✅ **مُغلق** (لم يعد مُبلَّغاً عنه) |
| `Microsoft.Build` | شفافة (**بناء فقط**) | 17.8.3 | 17.8.3 | High | GHSA-w3q9-fxm7-j8fq | `EF Core Tools` | ⛔ **متبقٍّ — خارج الصورة المنشورة** |
| `NuGet.Packaging` / `NuGet.Protocol` | شفافة (**بناء فقط**) | 6.11.0 | 6.11.0 | Low | GHSA-g4vj-cjjj-v7hg | `EF Tools` / `Containers.Tools` | ⛔ **متبقٍّ — خارج الصورة المنشورة** |

**الحكم على `Server`:** ✅ **صفر** High/Moderate/Low تمسّ وقت التشغيل. المتبقّي 3 حزم **أدوات بناء** (`Microsoft.Build` من `EF Core Tools`، و`NuGet.*` من `EF Tools`/`Containers.Tools`) — **لا تدخل الصورة المنشورة** ⇒ **لا إجراء عاجل**؛ تُعالَج مع ترقية أدوات EF.

> **الأثر على ADR-P00B-02 (عدم ترقية الاعتماديات):** القرار لم يُعدّل — **المالك** هو من رقّى، وهو صاحب الصلاحية. وأهمّ ما في هذه الترقية أنها **patch داخل majors قائمة** (`2.5.187→2.5.307` داخل 2.x · `8.0.10→8.0.33` داخل 8.0.x) ⇒ **لا تغيير في ترميز السلك** ⇒ خطر round-trip المتوقَّع في §8.3 لم ينشأ. `[RUNTIME-VERIFIED]`

### 8.2 بقية المشاريع — بعد الترقية

| المشروع | قبل | بعد | المتبقّي |
|---|---|---|---|
| `Shared` | `MessagePack 2.5.187` **High** | ✅ **لا حزم متأثرة** | — |
| `Desktop.Shared` | 3 × **High** (SignalR ×2 + MessagePack) | ✅ **لا حزم متأثرة** | — |
| `Agent` | 6 × **High** | 1 × **High** | `System.Security.Cryptography.Xml 8.0.3` — GHSA-cvvh-rhrc-wg4q (مُعلَن مباشرة في `Agent.csproj`) |
| `Desktop.Win` | 6 × **High** | 3 × **High** | `System.Net.Http 4.3.0` · `System.Text.RegularExpressions 4.3.0` · `Tmds.DBus.Protocol 0.16.0` (شفافة قديمة؛ `Tmds.DBus` من `Microsoft.PowerShell.SDK 7.4.5`) |

### 8.3 التوثيق بعد الترقية

| # | البند | الحالة | الإجراء المتبقّي |
|---|---|---|---|
| 1 | `MessagePack` (P0 — شبكي) | ✅ مُغلق | — |
| 2 | `SignalR.Common` + `SignalR.Protocols.MessagePack` (P0 — شبكي) | ✅ مُغلق | — |
| 3 | `System.Security.Cryptography.Pkcs` · `SQLitePCLRaw.lib.e_sqlite3` (P1) | ✅ مُغلق | — |
| 4 | `MailKit`/`MimeKit` (P2) | ✅ مُغلق | — |
| 5 | `Microsoft.Build` · `NuGet.*` (بناء فقط) | ⛔ متبقٍّ | ترقية أدوات EF — **بلا إجراء عاجل** |
| 6 | `Agent`: `System.Security.Cryptography.Xml 8.0.3` (High) | ⚠️ متبقٍّ | ترقية إلى `8.0.4+` — **قرار مالك** |
| 7 | `Desktop.Win`: 3 حزم شفافة قديمة | ⚠️ متبقٍّ | تبرير أو ترقية — لا مسار إنتاجي موثّق |

### 8.4 ⚠️ F-02 — أصول المتصفح المُلتزَمة خارج مدى ترقية NuGet

| الأصل المُلتزَم | الإصدار في المستودع | مقابل الخادم (بعد الترقية) | المصدر |
|---|---|---|---|
| `wwwroot/lib/microsoft/signalr-protocol-msgpack/` (`@microsoft/signalr-protocol-msgpack`) | **8.0.7** (نُشر 2024-07-09) | `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` = **8.0.33** | `git show 71b890b9~1:Server/libman.json` |
| `wwwroot/lib/microsoft-signalr/` (`@microsoft/signalr`) | **8.0.7** | `Microsoft.AspNetCore.SignalR.Client` = **8.0.33** | نفس المصدر |
| `wwwroot/lib/msgpack/` (`@msgpack/msgpack`) | **3.0.0-beta2** | `MessagePack` (C#) = **2.5.307** | نفس المصدر |

**الفرضية:** `dotnet list package --vulnerable` **لا يرى** أصول JavaScript — فهي ليست حزم NuGet. ⇒ **ترقية الخادم لا تُغطّي عميل المتصفح** الذي يفكّ MessagePack بنفسه في المتصفح.

**المرجع:** GHSA-f8h2-vmm9-qhj6 = **CVE-2026-45591** «ASP.NET Core Denial of Service Vulnerability» · High · CVSS 7.5 · `AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:H` · CWE-400 (Uncontrolled Resource Consumption) + **CWE-787 (Out-of-bounds Write)** · مصدرها `dotnet/aspnetcore` · نُشرت 2026-06-15.

**المتاح في خط 8.0.x على npm:** `8.0.7` → `8.0.17` (2025-07-31) → **`8.0.29` (2026-08-03)** ⇒ أحدث خط 8.0.x على npm هو **8.0.29** لا 8.0.33.

> **التوصية (قرار مالك):** محاذاة الأصلين إلى **8.0.29** ⇒ `[PROPOSED]`. **لم يُنفَّذ** — تغيير ثنائيات مُلتزَمة في `wwwroot/` تغيير إنتاجي مرئي يحتاج قرار المالك (نفس منطق ADR-P00B-01).
> **ما لم أستطع إثباته:** هل تضمّني الاستشارة حزمة npm نفسها (JS)؟ مصدر الاستشارة `dotnet/aspnetcore`، والحزمة المُبلَّغ عنها في التدقيق هي حزمة .NET ⇒ **تغطية JS = `[UNKNOWN]`**. ⇒ يُعالَج **تجديداً** لا ترقيعاً.

### 8.5 ما تحقّق فعلياً بعد الترقية

| الفحص | الأمر | النتيجة |
|---|---|---|
| بناء 10 مشاريع | `dotnet build <proj> -v:minimal` | **10/10 EXIT=0** · **صفر** سطر `NU1903` في السجل |
| الاختبارات | `dotnet test` بـ`DOTNET_ROLL_FORWARD=Major` | **19/19 ناجحة** (Shared 1/1 · Server 18/18) · 0 failed · 0 skipped |
| التدقيق | `dotnet list <proj> package --vulnerable --include-transitive` | `Server` 3 (بناء فقط) · `Shared` 0 · `Desktop.Shared` 0 · `Agent` 1 · `Desktop.Win` 3 |
| الشجرة | `git status --porcelain` قبل الفحص وبعده | **الفرق نفسه بالضبط** ⇒ أداة التدقيق والبناء لم تلمس أي ملف متتبَّع |

> **حدود هذا الدليل:** الاختبارات نجحت **على .NET 9** (تشخيص roll-forward) — لا على .NET 8. و**الـ19 اختباراً لا تغطّي** مسار MessagePack بين المتصفح والخادم ⇒ **لا يوجد اختبار round-trip للترميز**، ولا جهاز Windows للسلوك الفعلي. `[TEST-EVIDENCE]` على .NET 9 فقط.

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
| `NU1903` — كان × 12 | ❌ **لا** | **أُغلق 2026-11-06**: صفر ثغرة وقت تشغيل في `Server` بعد ترقية المالك (§8.1) — الترحيل لا يرثها؛ المتبقّي أدوات بناء فقط |
| CI بـSDK 7 | ❌ **لا** | مُصحَّح في `71b890b9` |
| `MessagePack` | ⚠️ **لا** | **أُغلق** على `2.5.307` **داخل 2.x** ⇒ **لا تغيّر في ترميز السلك** ولا حاجة للقفزة إلى `3.x`. الترحيل إلى SignalR 10 ما زال يجرّ `3.x` — قرار منفصل |

### 9.2 تسلسل ترحيل مقترح (بعد تثبيت خط الأساس)

| # | الخطوة | لماذا أول |
|---|---|---|
| 0 | تثبيت خط الأساس على .NET 8.0.33 (§3.2) | بلا خط أساس، لا معنى للترحيل |
| 1 | **إغلاق `NU1903` P0/P1 — أُنجز جزئياً 2026-11-06**: صفر ثغرة وقت تشغيل في `Server`؛ المتبقّي بناء فقط (§8.1) | الأمان قبل الكتلة |
| 2 | `Server` + `Shared` + `Agent` → `net10.0` | قلّة الحزم |
| 3 | `Desktop.*` + Avalonia ≥ 11.3 | أكبر اعتماد أصلي |
| 4 | حاوية `aspnet:10.0` + CI على `10.0.x` | نشر |
| 5 | `Windows` 10/11 × أجهزة الاختبار + Serilog + WSMan | تحقق ميداني |

---

## 10. الإصلاحات الدنيا المقترحة مع فروق الملفات بالضبط

**لا شيء ممّا يلي مُطبَّق.** كل فِرق точّي ومُراجَع. `D-0` مُنفَّذ من المالك (`71b890b9` + `e67900d6`) ⇒ مُوثَّق للمراجعة فقط. `D-2` **ملغى** بتصحيح C-09.

### D-0 — مُنفَّذ من المالك في `71b890b9` و`e67900d6` (للمراجعة)

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
**المخاطرة:** لو أُعيد أي إدخال لـ`libman.json` فلن يُسترجَع تلقائياً ⇒ يُقابله D-3 (توثيق الأصول).
**التراجع:** `git checkout -- Server/Server.csproj`.

### D-2 — ~~إغلاق فجوة المسار GAP-15~~ **ملغى (تصحيح C-09)**

**لم يُطبَّق ولن يُطبَّق.** الفرضية التي بُني عليها هذا التصحيح — «‏`Viewer.cshtml` يطلب مساراً غير موجود ⇒ 404» — **سالبة**.

| الفحص | النتيجة |
|---|---|
| الطلب في `Server/Pages/Viewer.cshtml:33,38` | `/lib/microsoft/signalr-protocol-msgpack/dist/browser/signalr-protocol-msgpack{,.min}.js` |
| الموجود على القرص | `Server/wwwroot/lib/microsoft/signalr-protocol-msgpack/dist/browser/` — 4 ملفات (`.js` · `.min.js` · `.js.map` · `.min.js.map`) |
| المطابقة | ✅ **مطابقة تامة** |
| كل مراجع `/lib/` في `Server/` (تسعة) | ✅ **تسعة من تسعة تُحَلّ إلى ملف قائم** |

> **الخطر من تطبيق تصحيحٍ خاطئ:** لو طُبِّق هذا التصحيح لكان **كسر مساراً صحيحاً** — يُخرج `Viewer.cshtml` عن `microsoft/signalr-protocol-msgpack/` ويحوّله إلى `microsoft-signalr-protocol-msgpack/` غير الموجود ⇒ 404 **حقيقي** في المتصفح ⇒ كسر بروتوكول MessagePack عند العميل. أخطاء التصحيح هنا ليست تجميلية؛ كانت ستُنشئ عطلاً جديداً.

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
| RB-2 | ~~**‏`MessagePack 2.5.187` قابل للاستغلال على مقبس مفتوح**~~ | — | — | ✅ **مُغلق 2026-11-06** — ترقية المالك إلى `2.5.307` (patch **داخل 2.x** ⇒ لا تغيير في ترميز السلك) · 10/10 بناء و19/19 اختبار بعد الترقية (§8.1) |
| RB-3 | **إعادة إدخال LibMan** لاحقاً ⇒ Dirty-Tree من جديد | متوسط | متوسط | D-1 (إزالة المرجع) + D-3 (توثيق) |
| RB-4 | **تعديلات المالك الخمسة ضُمّنت في `e67900d6` بلا دليل اختبار سلبي** ⇒ كسر محتمل في المصادقة | متوسط | **عالٍ** | مطلوب: اختبار 401/403 لكل نقطة نهاية قبل إقفال G1 |
| RB-5 | **‏`origin` يشير إلى المستودع الأصلي `immense/Remotely` لا إلى fork المالك** ⇒ الـpush الافتراضي يذهب إلى الأصلي، وCI لا يعمل على fork | متوسط | **عالٍ** | F-01 — إضافة remote باسم `fork` قبل أول push (§2.5) |
| RB-6 | **‏`Desktop.Win.Tests` يبقى متروكاً** ⇒ تغطية Windows = صفر | عالٍ | متوسط | D-4 — قرار |
| RB-7 | **أصول المتصفح المُلتزَمة ما زالت 8.0.7** ⇒ إصلاح الخادم وحده لا يغطي عميل MessagePack في المتصفح | متوسط | **عالٍ** | F-02 (§8.4) — محاذاة الأصل إلى 8.0.29 (أحدث 8.0.x على npm) |

### 11.3 التراجع

| التغيير | التراجع |
|---|---|
| D-1 (إزالة مرجع LibMan) | `git checkout -- Server/Server.csproj` |
| ~~D-2 (مسار Viewer.cshtml)~~ | **ملغى** — فرضية C-09 سالبة ⇒ لا تعديل على `Viewer.cshtml` |
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
| **قاعدة الأدلة — `Server`** | ✅ **PASS** | بعد ترقية المالك: **صفر** High/Moderate/Low تمسّ وقت التشغيل · المتبقّي 3 حزم **بناء فقط** (§8.1) |
| **قاعدة الأدلة — باقي المشاريع** | ⚠️ **متبقٍّ** | `Agent`: `System.Security.Cryptography.Xml 8.0.3` High · `Desktop.Win`: 3 High شفافة قديمة (§8.2) |
| **أصول المتصفح المُلتزَمة** | ⛔ **UNKNOWN** | أصل JS `signalr-protocol-msgpack` ما زال **8.0.7** مقابل خادم **8.0.33** — تدقيق NuGet لا يرى أصول JS (§8.4 · F-02) |
| **LIB002** | ✅ **PASS** (بعد `71b890b9`) | الجذر مؤكَّد (§5.4) والإصلاح مُنفَّذ ومُتحقَّق |
| **‏CI — إصلاح المصدر** | ✅ **PASS** | `setup-dotnet@v4` بـ`8.0.x` ⇒ `NETSDK1045` مُغلق (التشغيل الفعلي: ⛔ UNKNOWN — انظر «CI على .NET 8» أدناه) |
| **Dirty-Tree من البناء** | ✅ **PASS** | شجرة نظيفة بعد البناء |
| **جاهزية .NET 10** | ⛔ **UNKNOWN** | تقييم فقط (§9) — لا SDK 10، لا ترحيل |
| **سلوك Windows الحقيقي** | ⛔ **UNKNOWN** | لا جهاز Windows ممثّل في هذه البيئة |
| **‏CI على .NET 8 (تشغيل فعلي)** | ⛔ **UNKNOWN** | إصلاح المصدر مُنفَّذ (`71b890b9`) ولم يُشغَّل بعد؛ و`origin` = المستودع الأصلي ⇒ يلزم remote للـfork (F-01 · §2.5) |
| **‏GAP-15 / مسار `signalr-protocol-msgpack`** | ✅ **PASS** | **false positive** — مطابقة تامة (تصحيح C-09) |

### 12.1 بوابة مرحلة P00-B

| الشرط | الحالة |
|---|---|
| فصل أعطال البيئة / الاستعادة / البناء / التشغيل / الاختبارات | ✅ **PASS** |
| جذر مؤكَّد أو موسوم `UNKNOWN` | ✅ **PASS** — LIB002 **مؤكَّد** (§5.4) |
| خط أساس بناء ناجح | ✅ **PASS** |
| أدلة اختبار آلية (TRX) | ✅ **PASS** — 19/19 على .NET 9 (تشخيص) |
| أدلة بناء واختبار **بعد** ترقية الاعتماديات | ✅ **PASS** — 10/10 مشروعاً · 19/19 اختباراً · صفر `NU1903` (§8.5) |
| خط أساس .NET 8 قابل للتنفيذ | ⛔ **BLOCKED** — لا runtime 8 محلياً؛ **ومساران**: CI (§13) أو تثبيت معزول (D-5) |
| لا كود إنتاج مُعدَّل **بواسطة الوكيل** | ✅ **PASS** — تعديلات المالك الخمسة لم تُلمس (ضمّنها المالك في `e67900d6`) |

### 🔶 **حالة بوابة P00-B: PASS مشروط**

> **ما لا يقوله هذا PASS:** البناء يعمل، والاختبارات تنجح عند roll-forward — لكن **على .NET 9، لا .NET 8**. و**لا** يمكن اعتماد "الاختبارات ناجحة" بوصفها إثباتاً لخط الأساس التاريخي. و**صفر** ثغرة وقت تشغيل في `Server` بعد ترقية المالك — لكن **أصول المتصفح المُلتزَمة ما زالت 8.0.7** (F-02 · خارج مدى ترقية NuGet) و`Agent` ما زال فيه High واحد. البوابات المستقلة (G1–G7 في `GATE-REGISTER.md`) لم تتغيّر.

---

## 13. الإجراء التالي الموصى به (واحد)

> **إضافة remote للـfork المالك ثم push واحد مقصود إلى CI — لتوليد أول دليل اختبار على خط أساس .NET 8.**
>
> **الشكل المحدد:** remote جديد باسم `fork` يشير إلى fork المالك على GitHub ⇒ push الـcommit `e67900d6` (+ تصحيحات هذا التقرير) إليه ⇒ `run_tests.yml` يعمل على `windows-latest` بـ`setup-dotnet@v4` و`8.0.x` ⇒ `dotnet test` على الحل (19 اختباراً) ⇒ **ملف TRX على .NET 8** ⇒ يُرفق بـ`EVIDENCE-MAP.md` ويُغلق GAP-06 جزئياً.
>
> **لماذا هو الواحد — وليس D-5 (تثبيت runtime معزول):**
> - **لا يحتاج تثبيت أي برنامج** ⇒ لا تصريح تثبيت، ولا تغيير نظامي، ولا مسار EOL على هذا الجهاز (ADR-P00B-03 يبقى نافذاً بلا استثناء).
> - **يُنتج الدليل نفسه** (اختبارات على 8.0.x) ⇒ D-5 وCI طريقان لنفس النتيجة، والطريق الذي لا يلمس الجهاز هو الأصح.
> - **يُغلق أخطر ادّعاء متبقٍّ**: إصلاح CI في `71b890b9` مُصنَّف في `EVIDENCE-MAP.md` (GAP-19) بأنّه **مُصلَّح في المصدر ولم يُشغَّل بعد** ⇒ أول push هو ما يحوّله من ادّعاء إلى دليل.
> - **يكشف F-01 قبل أن يكلّف**: أي push بلا remote صحيح يذهب إلى المستودع الأصلي — خطأ حقيقي ومكلف.
> - **لا يمسّ كود الإنتاج** · قابل للتراجع بـ`git remote remove fork`.
>
> **ما يبقى خلفه ويحتاج قراراً لاحقاً (ليس جزءاً من هذا الإجراء):** محاذاة أصول المتصفح مع الخادم — أصل JS `signalr-protocol-msgpack` ما زال **8.0.7** مقابل خادم **8.0.33** (F-02 · §8.4) · `Agent`: `System.Security.Cryptography.Xml 8.0.3` High (§8.2) · مصير `Tests/Desktop.Win.Tests` (D-4 · قرار) · D-1 وD-3 (إزالة ارتباط LibMan وتوثيق الأصول) · قرار وجهة خط الأساس بعد EOL.

---

*لم يُعدَّل أي ملف إنتاج · لم يُثبَّت أي برنامج · **لم أُرقِ أي اعتمادية** (ترقية الاعتماديات نفّذها المالك بنفسه) · لم يُغيَّر أي إطار عمل · لم تُعدَّل الـconstitution · لم يبدأ تنفيذ SPEC-001 · لا نشر ولا commit ولا push · لم يُستخدم `git add -A` · كل تعديلات المالك محفوظة.*

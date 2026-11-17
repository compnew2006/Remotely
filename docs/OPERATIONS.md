# OPERATIONS — running and operating the server

**المشروع:** Remotely Capture Extension (fork of `immense/Remotely`)
**تاريخ:** 2026-11-16 · **الحالة:** يوثّق السلوك المُتحقَّق منه محليًا على Docker 29.9.3

---

## 1. الحاوية

```bash
# 1) نشر الخادم
dotnet publish Server/Server.csproj -c Release -r linux-x64 --self-contained false -o Server/bin/publish

# 2) بناء الصورة
docker build -f Server/Dockerfile -t remotely:local .

# 3) تشغيل
docker run -d --name remotely \
  -e ASPNETCORE_HTTP_PORTS=5000 \
  -e Remotely_ApplicationOptions__DbProvider=SQLite \
  -e "Remotely_ConnectionStrings__SQLite=Data Source=/app/AppData/Remotely.db" \
  -v remotely-data:/app/AppData \
  -p 5000:5000 \
  remotely:local
```

- ملف `.dockerignore` يقصر سياق البناء على `Server/bin/publish`، فلا تُنقل مجلدات `bin`/`obj` إلى الـdaemon.
- `Server/Dockerfile` ينشئ `/app/AppData`، فيعمل `docker run` **بدون** volume أيضًا؛ الـvolume مطلوب فقط لاست بقاء البيانات.

## 2. مجلد البيانات الدائم (`AppData`)

| المسار | المحتوى | ملاحظة |
|---|---|---|
| `AppData/Remotely.db` | قاعدة SQLite | أو `AppData` في SQL Server/PostgreSQL حسب المزوّد |
| `AppData/Logs/` | سجلات Serilog | تدوير يومي + احتفاظ `DataRetentionInDays` |
| `AppData/Screenshots/<DeviceId>/` | لقطات الشاشة | الاحتفاظ 200/24 ساعة افتراضيًا |
| `AppData/Camera/<DeviceId>/` | مقاطع الكاميرا | الاحتفاظ 200/24 ساعة افتراضيًا |
| `AppData/Audio/<DeviceId>/` | مقاطع الصوت | الاحتفاظ 200/24 ساعة افتراضيًا |
| `AppData/keys/update-signing.key` | مفتاح توقيع حزم العميل | DPAPI على Windows، **نص صريح على Linux** |
| `AppData/DataProtection-Keys/` | مفاتيح حماية الكوكيز | بقاؤها هنا يمنع إخراج المستخدمين عند كل نشر |

## 3. فحص الجاهزية

```bash
# من داخل الحاوية — الفحص مقصور على المستدعي المحلي
docker exec remotely curl -fsS http://localhost:5000/api/healthcheck
```

- المسار `/api/healthcheck` (Server/API/HealthCheckController.cs) محمي بـ`LocalOnlyFilter` ⇒ يقبل الـloopback فقط.
- يعيد **200** عندما تكون قاعدة البيانات متاحة، و**503** عندما لا يمكن الوصول إليها (وهو ما يجعل `HEALTHCHECK` في الصورة يعلّم الحاوية غير سليمة).
- نقطة `/health` العامة **غير موجودة عمدًا**: أي فحص خارجي يجب أن يمر من داخل الحاوية أو عبر وكيل على نفس الجهاز.
- الفحص يستخدم `DatabaseHealthCheck` (Server/HealthChecks/DatabaseHealthCheck.cs) عبر `HealthCheckService`، فيتحقق من الاتصال **ومن** وجود جداول التطبيق (قاعدة بلا migrations تتصل بنجاح أيضًا).

## 4. المقاييس وCorrelation ID

```bash
# من داخل الحاوية — نفس قيد المستدعي المحلي
docker exec remotely curl -fsS http://localhost:5000/metrics
```

- `/metrics` يعرض Prometheus text: عدد الطلبات حسب الطريقة والحالة، نتائج فحص الجاهزية، مدة التشغيل، إصدار البناء، و`remotely_instance_conflict`.
- كل طلب يحمل `X-Correlation-Id` في الاستجابة وفي سجلّ Serilog (خاصية `CorrelationId`). القيمة القادمة من العميل تُقبل فقط إذا كانت آمنة (حروف وأرقام و`-_.` وأطول من 64 تُرفض وتُستبدل).
- من خارج الجهاز: `/metrics` يرجع **404** و`/api/healthcheck` يرجع **401** (كلاهما مقصور على الـloopback). القيد يتبع عنوان العميل بعد معالجة الـforwarded headers، فطلب يصل عبر وكيل مُهيَّأ يحمل عنوانًا مُزوَّرًا في `X-Forwarded-For` قد يمرّ. انظر البند الأخير في «حدود معروفة».

## 4.1 سياسة المحتوى (CSP) والخطوط

- سمات Bootswatch تستورد خطوطها من Google Fonts، لذلك تسمح السياسة بمصدرَي الخطوط فقط:
  `style-src ... https://fonts.googleapis.com` و`font-src ... https://fonts.gstatic.com`.
- `script-src` يبقى `'self'` فقط: لا سكربت مضمّن ولا `eval`، وأي زر يجب أن يستخدم نموذج أحداث Blazor (`@onclick`) لا `onclick` مضمّن (المضمّن محجوب ويتعطّل الزر بصمت).
- مستند الجذر يحمل `frame-ancestors 'self'` إضافية من إطار العمل؛ الأول في القائمة هو الأقوى (`'none'`) فالنتيجة مُشدَّدة.

## 5. سجلات Startup

- إعدادات `ApplicationOptions` تُتحقق عند الإقلاع: مزوّد غير مدعوم أو `DockerGatewayIp` غير صالح ⇒ يرفض العملية البدء برسالة واضحة.
- فشل تجهيز قاعدة البيانات يطبع سببًا قابلًا للتنفيذ قبل إعادة رمي الاستثناء (المسار المتوقع لملف SQLite، أو تنبيه عام لبقية المزوّدات). **لا تُطبع سلسلة الاتصال** لأنها قد تحوي كلمة مرور.

## 6. النسخ الاحتياطي

```bash
# SQLite: أوقف الخدمة أو فرّغ WAL قبل نسخ الملف
docker exec remotely sh -c "ls -l /app/AppData/Remotely.db*"
```

- انسخ مجلد `AppData` كاملًا (قاعدة + وسائط + مفاتيح).
- **مفاتيح `DataProtection-Keys` جزء من النسخة**: بدونها يخرج كل المستخدمين من الجلسات بعد الاسترجاع.
- **مفتاح `update-signing.key` جزء من النسخة**: بدونه لن يوقّع السيرفر حزم العميل، وتتوقف التحديثات التلقائية.

## 7. بوابات CI

| الوظيفة | ما تتحقق منه |
|---|---|
| `server` | بناء السيرفر + `Server.Tests` (بما فيها اختبارات SQLite الحقيقية) |
| `desktop` | بناء الحل + `Shared.Tests` + نشر الوكيل `win-x64` بختم إصدار |
| `migrations` | تطبيق migrations على PostgreSQL وSQL Server (قاعدتان مؤقتتان) |
| `container` | بناء الصورة وتشغيلها وفحص `/api/healthcheck` من داخلها |

⇒ CI أخضر **ليس** اجتياز بوابة إطلاق. إجراء اجتياز البوابات في `RELEASE-CHECKLIST.md`.

## 9. هوية الأجهزة (credential)

كل جهاز يحمل **بيانات اعتماد (credential)** تُخزَّن في `ConnectionInfo.json` على الجهاز نفسه، والسيرفر لا يحفظ منها إلا **بصمتها (SHA-256)**. معرّفة الجهاز ومنظمة الجهاز وحدهما لا يكفيان: من يعرفهما يستطيع ادّعاء أنه هذا الجهاز.

- الوكيل يقدّم الـcredential في ترويسة `X-Remotely-Device-Token` عند كل تسجيل، والترويسة تُقرأ في كل اتصال تلقائي.
- أول تسجيل يربط الـcredential بجهاز جديد؛ بعده لا يُقبل إلا نفس الـcredential (أو credential مطابق لبصمته).
- الـcredential يتجدّد تلقائيًا بعد 30 يومًا، فـcredential مسرَّب لا يبقى صالحًا للأبد.
- **التسجيل ما زال مفتوحًا**: جهاز جديد يُقبل مع أي منظمة معروفة ⇒，这条 الفجوة (نصف G4) ما زالت مفتوحة.

### كم جهازًا بلا credential؟

المقياس `remotely_devices_without_credential` في `/metrics`، والسجلّ ينبّه عند تغيّر العدد. الجهاز الذي بلا credential ما زال قابلًا للادّعاء من أول من يسجّله.

الاستعلام لكل مزوّد (نفس النص لكل الثلاثة):

```sql
SELECT ID, DeviceName, OrganizationID
FROM Devices
WHERE ServerVerificationToken IS NULL OR ServerVerificationToken = '';
```

- **SQLite**: الملف في `AppData/Remotely.db` — `sqlite3 Remotely.db "SELECT ..."`.
- **SQL Server**: `sqlcmd -S <server> -d Remotely -Q "SELECT ..."`.
- **PostgreSQL**: `psql -d Remotely -c "SELECT ..."`.

### جهاز رفضه السيرفر

لو رفض السيرفر تسجيل جهاز فالـcredential المخزَّن لا يطابق ما في السجل (مثلًا بعد استعادة نسخة احتياطية). الحل: امسح `ServerVerificationToken` من `ConnectionInfo.json` على الجهاز وأعد تشغيل الوكيل — سيُسجَّل من جديد وتُربط بصمته.

## 10. حدود معروفة

- **نسخة واحدة فقط**: لا يوجد SignalR backplane (Redis/Azure SignalR)، فتوسيع الخادم إلى أكثر من نسخة لا يوزّع الجلسات. الخادم يأخذ **عُقدة (lease)** على قاعدة البيانات، ويكتب في السجل وفي `remotely_instance_conflict` عندما يجد نسخة أخرى على نفس القاعدة — تحذير لا يمنع الإقلاع.
- **مفتاح التوقيع على Linux غير مشفّر**: DPAPI متاح على Windows فقط؛ على Linux يُخزَّن المفتاح بلا حماية على مستوى الملف.
- **فحص الجاهزية محلي فقط**: لا يمكن مراقبته من خارج الجهاز دون وكيل.
- **الفحص المحلي trusts عنوان العميل المُزوَّر**: القيد يطبَّق على `RemoteIpAddress` بعد معالجة الـforwarded headers. أي طلب يصل من عنوان موثوق (البوابة في الحاوية مثلًا) يحمل `X-Forwarded-For` يتحكم في العنوان الذي يُفحص ⇒ يمكن لمُتصلٍ خارج الجهاز أن يمرّ على `/metrics` و`/api/healthcheck` بترويسة مزوَّرة. هذا حد معروف في الترويسات الموثوقة، وهو مذكور في سجل البوابات.
- **التحديثات التلقائية**: التحقق من التوقيع في وكيل Windows فقط (لا يوجد مسار مكافئ لـ Linux/macOS).

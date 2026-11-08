## Server/Services/README.md

- **Drift — عمر `DataService`:** يصرّح الـ README (Invariants) بـ "One shared `DataService` lifetime per request scope". الكود في `Server/Program.cs:262` يسجّله Transient (`services.AddTransient<IDataService, DataService>()`) — أي مدة عمر قصيرة من كل مرة واحدة، لا نطاق طلب مشترك.
- **Drift — وصفة غير دقيقة:** الـ Overview يصنّف `PascalCasePolicy` ضمن "timezone display" ضمن قائمة أدوات UI. الكود يوضح أن `PascalCasePolicy` سياسة تسمية JSON (`ConvertName` ترفع الحرف الأول فقط) — لا علاقة له بمنطقة زمنية أو عرض موضوعي.
- **تغطية — `CameraDevicesStore` غير مذكور:** الملف `CameraDevicesStore.cs` موجود (`ICameraDevicesStore` + `CameraDevicesStore`) لكنه غير مذكور إطلاقًا في README.
- **تغطية — `ScreenshotService.BrowseDirectory` غير مذكور:** واجهة `IScreenshotService` تكشف `BrowseDirectory(string? path)` — سلسلة ملفات الخادم للاختيار مجلدًا — غير مذكور في README.

## Server/Services/Stores/README.md

- No drift detected.

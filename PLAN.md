# DOCX to PDF Conversion API (.NET 10 / ASP.NET Core)
## Texnik Reja va Arxitektura Hujjati (PLAN.md)

Ushbu hujjat 10 yillik tajribaga ega Senior .NET dasturchi yondashuvi asosida tayyorlangan. Unda **.NET 10 (C# 13)** va **MiniPdf** kutubxonasiga asoslangan yuqori unumdor, xavfsiz va kengaytiriluvchan DOCX -> PDF konvertatsiya API xizmatining arxitekturasi va bosqichma-bosqich amalga oshirish rejasi bayon etilgan.

---

## 1. Loyiha Haqida va Asosiy Maqsadlar
- **Maqsad:** Foydalanuvchilar tomonidan yuborilgan `.docx` (Word) hujjatlarini qisqa vaqt ichida va original formatini saqlagan holda `.pdf` formatiga aylantirib beruvchi RESTful Web API yaratish.
- **Asosiy kutubxona:** [MiniPdf](https://github.com/mini-software/MiniPdf) (Microsoft Office, LibreOffice yoki COM komponentlarisiz toza va tezkor ishlovchi native Office-to-PDF kutubxona).
- **Muhit / Framework:** .NET 10 (ASP.NET Core Web API), C# 13 eng so'nggi imkoniyatlari.
- **Prinsiplar:** SOLID, Clean Code, Fail-fast validatsiya, Resurslarni tejamkor boshqarish (MemoryStream oqishi oldini olish), Asinxron va Concurrency-safe arxitektura.

---

## 2. Loyiha Strukturasi (Vertical Slice & Clean Architecture uyg'unligi)

```
MiniPdf.DocxConverter/
│
├── src/
│   └── MiniPdf.DocxConverter.Api/
│       ├── Common/
│       │   ├── Constants/
│       │   │   └── FileConstants.cs          # Ruxsat etilgan MIME typelar, hajm limitlari, magic byte'lar
│       │   ├── Exceptions/
│       │   │   ├── ConversionException.cs     # Konvertatsiya xatoliklari uchun domain exception
│       │   │   └── InvalidFileException.cs    # Noto'g'ri fayl formati xatosi
│       │   └── Middlewares/
│       │       └── GlobalExceptionHandler.cs  # IExceptionHandler orqali ProblemDetails RFC 7807 standarti
│       │
│       ├── Contracts/
│       │   ├── Requests/
│       │   │   └── ConvertDocxRequest.cs      # IFormFile va konvertatsiya opsiyalari
│       │   └── Responses/
│       │       └── ConversionErrorResponse.cs # Xatoliklar strukturasi
│       │
│       ├── Services/
│       │   ├── Abstractions/
│       │   │   ├── IDocxToPdfConverter.cs     # Konvertatsiya interfeysi (DIP)
│       │   │   └── IFontManagerService.cs     # Fontlarni yuklash va ro'yxatga olish interfeysi
│       │   └── Implementations/
│       │       ├── MiniPdfDocxConverter.cs    # MiniPdf adapteri (Semaphore va xotirani boshqaruvchi)
│       │       └── FontManagerService.cs      # TTF/OTF fontlarni MiniPdf ga registratsiya qiluvchi xizmat
│       │
│       ├── Controllers/
│       │   └── ConverterController.cs         # REST API endpointlari (multipart/form-data)
│       │
│       ├── Fonts/                             # Serverdagi standart va milliy (kirill/lotin) fontlar
│       │   └── .gitkeep
│       │
│       ├── appsettings.json                   # Konvertatsiya cheklovlari (MaxFileSize, MaxConcurrency)
│       ├── Program.cs                         # DI konteyner, Pipeline konfiguratsiyasi, OpenAPI
│       └── MiniPdf.DocxConverter.Api.csproj
│
└── tests/
    └── MiniPdf.DocxConverter.Tests/
        └── MiniPdfDocxConverterTests.cs       # Unit & Integration testlar
```

---

## 3. Asosiy Texnik Talablar va Arxitektura Yechimlari

### 3.1. MiniPdf Kutubxonasi Integratsiyasi
- **Bog'lanish usuli:**
  1. `dotnet add package MiniPdf` (rasmiy NuGet paketi orqali);
  2. Yoki agar mahalliy o'zgartirishlar/maxsus build talab etilsa, `MiniPdf.dll` reference / submodule orqali.
- **Font muammosini hal qilish (Critical):**
  - Linux serverlarda (Docker, Azure, AWS, Ubuntu) standart Windows fontlari (Calibri, Arial, Times New Roman) bo'lmaydi.
  - Hujjatdagi shriftlar buzilmasligi uchun ilova ishga tushishida (`FontManagerService` orqali) `Fonts/` papkasidagi yoki OS tizimidagi TTF/OTF fayllar `MiniPdf.RegisterFont(name, bytes)` orqali avtomatik ro'yxatdan o'tkaziladi.

### 3.2. Resurslar va Xotirani Boshqarish (Senior Level Best Practices)
- **Fayl Validatsiyasi (Security & Reliability):**
  - Faqat fayl kengaytmasi (`.docx`) emas, balki faylning **Magic Bytes** (ZIP arxivi ekanligi: `PK\x03\x04`) va MIME-type (`application/vnd.openxmlformats-officedocument.wordprocessingml.document`) tekshiriladi.
  - Maksimal fayl hajmi: Standart 25 MB (konfiguratsiya orqali boshqariladi).
- **CPU & RAM Himoyasi (Concurrency Control):**
  - PDF ga konvertatsiya qilish og'ir CPU va xotira talab qiluvchi amaliyot.
  - Server o'z-o'zidan yiqilmasligi (OOM yoki CPU 100%) uchun `SemaphoreSlim` orqali bir vaqtning o'zida maksimal `N` ta (masalan, CPU yadrolar soniga mos 4–8 ta) konvertatsiya cheklovi qo'yiladi.
- **Xotira boshqaruvi:**
  - Katta fayllar bilan ishlashda LOH (Large Object Heap) fragmentatsiyasini kamaytirish uchun `RecyclableMemoryStream` yoki xavfsiz `using` bloklari orqali streamlar to'liq tozalanadi.

### 3.3. API Dizayni va Endpointlar

#### 1) Asosiy Konvertatsiya Endpointi
- **Yo'nalish:** `POST /api/v1/converter/docx-to-pdf`
- **Content-Type:** `multipart/form-data`
- **Parametrlar:**
  - `file` (IFormFile, Majburiy) — DOCX fayl;
  - `compress` (bool, Ixtiyoriy, default: true) — PDF FlateDecode siqish;
  - `pageSize` (string, Ixtiyoriy, default: "A4") — "A4" yoki "Letter".
- **Javob:**
  - `200 OK`: `application/pdf` (stream qilingan fayl, `Content-Disposition: inline; filename="document.pdf"`);
  - `400 Bad Request`: Noto'g'ri fayl formati yoki hajm limiti oshganda (RFC 7807 ProblemDetails);
  - `422 Unprocessable Entity`: Fayl buzilgan yoki konvertatsiya qilib bo'lmaganda;
  - `429 Too Many Requests` yoki `503 Service Unavailable`: Serverda konvertatsiya slotlari to'lganda.

#### 2) Health & Diagnostics Endpoint
- **Yo'nalish:** `GET /api/v1/converter/health`
- **Vazifasi:** Tizim holati, o'rnatilgan fontlar ro'yxati va servis ishga yaroqliligini tekshirish.

---

## 4. Bosqichma-bosqich Amalga Oshirish Rejasi (Implementation Roadmap)

| Bosqich | Vazifa | Tavsif |
| :--- | :--- | :--- |
| **1-Bosqich** | Loyiha fundamentini yaratish | `.NET 10` Web API loyihasini yaratish, `MiniPdf` bog'liqligini o'rnatish, papkalar strukturasini shakllantirish. |
| **2-Bosqich** | Domain va Kontraktlar | Modellarni yaratish, MIME & Magic bytes validatsiya qoidalarini yozish, `ProblemDetails` sozlash. |
| **3-Bosqich** | Shriftlar tizimi (`FontManagerService`) | Linux va Docker muhitlarida o'zbek lotin/kirill va xalqaro shriftlarni qo'llab-quvvatlovchi avtomatik font loader'ni yozish. |
| **4-Bosqich** | Konvertatsiya Servisi (`MiniPdfDocxConverter`) | `IDocxToPdfConverter` interfeysini amalga oshirish, Semaphore orqali parallelizmni cheklash, xatoliklarni ushlash va loglash. |
| **5-Bosqich** | REST API Controller | `multipart/form-data` qabul qiluvchi va PDF stream qaytaruvchi API endpointni yaratish, Swagger/OpenAPI hujjatlash. |
| **6-Bosqich** | Docker & Deployment tayyorgarligi | Alpine/Debian asosidagi multi-stage Dockerfile yaratish, server font paketlarini o'rnatish (msttcorefonts). |
| **7-Bosqich** | Testlash va Tekshirish | Real DOCX hujjatlari orqali konvertatsiya sifatini va tezligini tekshirish. |

---

## 5. Xavfsizlik va Production-Ready Sozlamalari
1. **Fayl xavfsizligi:** Hujjatlar diskda doimiy saqlanmaydi — faqat xotirada (in-memory) yoki vaqtinchalik xavfsiz stream orqali konvertatsiya qilinadi va darhol yo'qotiladi.
2. **Timeout va Cancellation:** Har bir so'rov uchun `CancellationToken` qo'llab-quvvatlanadi, klient ulanishni uzsa resurslar darhol bo'shatiladi.
3. **Structured Logging:** Microsoft.Extensions.Logging orqali har bir konvertatsiya vaqti (ms), fayl hajmi va natijasi log qilinadi.

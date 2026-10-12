# MiniPdf DOCX to PDF Converter API (.NET 10)

Lightweight, high-performance, and serverless-ready REST API for converting `.docx` (Word) documents to `.pdf` based on **.NET 10 (C# 13)** and [MiniPdf](https://github.com/mini-software/MiniPdf).

> **Eslatma:** Ushbu yechim Microsoft Office, LibreOffice, Adobe Acrobat yoki COM automationsiz to'liq native ishlaydi.

---

## Xususiyatlari
- **Native va Yengil:** Tashqi og'ir ofis dasturlariga bog'liqlik yo'q.
- **Fail-Fast Xavfsizlik:** Fayl hajmi, kengaytmasi va **Magic Bytes (ZIP Header)** tekshiruvi.
- **Resurslarni Tejamkor Boshqarish:** `SemaphoreSlim` orqali parallel konvertatsiyalarni tartibga solish va xotirani tezkor tozalash.
- **Fontlarni Boshqarish:** Linux / Docker muhitlarida shriftlarni to'g'ri render qilish uchun avtomatik tizim va lokal fontlar registratsiyasi.
- **Global Error Handling:** RFC 7807 `ProblemDetails` standarti asosida xatoliklar javobi.
- **Production-Ready Testlar:** xUnit va `WebApplicationFactory` orqali to'liq qamrab olingan testlar to'plami.

---

## Ishga tushirish (Local)

```bash
# Loyihani tiklash va yig'ish
dotnet build

# Testlarni yurgazish
dotnet test

# API ni ishga tushirish
dotnet run --project src/MiniPdf.DocxConverter.Api
```

Standart port: `http://localhost:5000`

---

## API Endpointlari

### 1. DOCX ni PDF ga konvertatsiya qilish
- **URL:** `POST /api/v1/converter/docx-to-pdf`
- **Content-Type:** `multipart/form-data`

#### Parametrlar:
| Parametr | Turi | Majburiy | Tavsif |
| :--- | :--- | :--- | :--- |
| `File` | File (`.docx`) | Ha | Konvertatsiya qilinishi kerak bo'lgan Word hujjati |
| `Compress` | boolean | Yo'q | PDF oqimlarini FlateDecode bilan siqish (default: `true`) |
| `PageSize` | string | Yo'q | Sahifa o'lchami: `"A4"` yoki `"Letter"` (default: `"A4"`) |

#### Namuna (cURL):
```bash
curl -X POST http://localhost:5000/api/v1/converter/docx-to-pdf \
  -F "File=@/path/to/document.docx" \
  -F "Compress=true" \
  -F "PageSize=A4" \
  --output result.pdf
```

### 2. Tizim holati va fontlar ro'yxati (Health Check)
- **URL:** `GET /api/v1/converter/health`

#### Namuna javob:
```json
{
  "status": "Healthy",
  "engine": "MiniPdf .NET Native (No Office/COM required)",
  "registeredFontsCount": 42,
  "registeredFonts": ["Arial", "Calibri", "DejaVuSans", "TimesNewRoman"],
  "availableConcurrencySlots": 4,
  "timestampUtc": "2026-10-12T10:45:00Z"
}
```

---

## Docker orqali ishga tushirish

```bash
# Docker image yaratish
docker build -t minipdf-docx-api:latest .

# Konteynerni ishga tushirish
docker run -d -p 5000:5000 --name minipdf-api minipdf-docx-api:latest
```

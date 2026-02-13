# Refahi Backend

Backend سیستم **Refahi** بر پایه‌ی .NET 10 و معماری Modular Monolith طراحی شده است.  
این ریپو مسئول پیاده‌سازی منطق دامنه، APIها، پرداخت، رزرو و یکپارچگی با تامین‌کنندگان است.

---

## Tech Stack
- **.NET 10 (LTS)**
- ASP.NET Core Minimal APIs
- Modular Monolith
- Clean Architecture + DDD
- CQRS (MediatR)
- FluentValidation
- Entity Framework Core 10
- Dapper (Read models)
- PostgreSQL
- Redis (در مراحل بعدی)
- JWT Authentication

---

## ساختار کلی Solution

```
Refahi.Backend
├─ BuildingBlocks/        # Cross-cutting concerns (Result, Validation, Transactions)
├─ Host/                  # Bootstrapper و Program.cs
├─ Modules/
│  ├─ Identity/
│  ├─ Wallets/
│  ├─ Providers/
│  ├─ Hotels/
│  ├─ Orders/
│  └─ Catalog/
└─ tests/ (در مراحل بعدی)
```

هر ماژول شامل لایه‌های زیر است:
```
ModuleName
├─ ModuleName.Api
├─ ModuleName.Application
├─ ModuleName.Application.Contracts
├─ ModuleName.Domain
└─ ModuleName.Infrastructure
```

---

## اصول معماری مهم (خط قرمزها)
- هر ماژول **Owner داده‌های خودش** است
- ارتباط بین ماژول‌ها فقط از طریق `Application.Contracts`
- Wallet تنها Owner داده‌های مالی است
- Ledger در Wallet **append-only** است
- DbContext مشترک نداریم؛ هر ماژول DbContext خودش را دارد
- Use-caseهای بین‌ماژولی با **TransactionScope** مدیریت می‌شوند

مرجع رسمی این تصمیمات:
👉 ریپوی `Refahi-Docs`

---

## اجرای پروژه (Local Development)

### پیش‌نیازها
- .NET SDK 10
- PostgreSQL
- (اختیاری) Docker

### تنظیمات
1) یک دیتابیس PostgreSQL بسازید
2) ConnectionString را در `appsettings.Development.json` قرار دهید

### اجرا
```bash
dotnet restore
dotnet build
dotnet run --project Refahi.Host
```

### Health Check
```http
GET /health
```

---

## وضعیت فعلی پروژه
- ✅ Solution Skeleton
- ✅ DI و Packageها
- ✅ Health Endpoint
- 🚧 Identity (در حال انجام)
- 🚧 Wallet Ledger (Sprint 01)
- ⏳ Hotels Booking Flow

Sprint فعال:
👉 **Sprint 01 – Hotel B2C MVP**

---

## نحوه کار با Copilot
این پروژه به‌شدت **Prompt-driven** توسعه داده می‌شود.

ترتیب پیشنهادی:
1) `Refahi-Docs/prompts/copilot/00-context.md`
2) `RUNBOOK.md`
3) Prompt مربوط به Sprint فعال

> ⚠️ بدون خواندن Docs و Promptها کدنویسی نکنید.

---

## Contribution Rules
- Feature خارج از Scope Sprint ممنوع
- Refactor بدون نیاز بیزینسی ممنوع
- هر تصمیم معماری باید مستند شود
- Build باید همیشه سبز بماند

---

## License
Proprietary – All rights reserved.

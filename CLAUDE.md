# CLAUDE.md — Dashboard AI Assistant (RAG)

## Ishlash qoidalari (foydalanuvchi talabi)

- Muloqot **o'zbek tilida**.
- **Build, restore, run, docker, model yuklashni foydalanuvchi o'zi qiladi.** `dotnet build/restore/ef`, `docker` buyruqlarini ishga tushirma. Paketlarni `.csproj` ga qo'lda yoz. Oxirida qaysi buyruqlarni ishga tushirish kerakligini ayt.
- Kod hech qachon to'liq build qilinmagan — foydalanuvchi build xatolarini yuboradi, ularni tuzatasan.
- Clean Code + SOLID, ortiqcha abstraksiya/paket qo'shma, faqat so'ralganini qil, javoblar qisqa.

## Manba

Talablar va arxitektura: `/home/t_ergashev/Downloads/RAG_Analysis.md` (v2). Kod hujjati (endpoint → handler → infra oqimlari): `docs/ARCHITECTURE.md`. Bo'lim raqamlari (5.3 SQL, 7.x flow, 8 chunking, 9 qidiruv, 15 bosqichlar) kodda shu hujjatga ishora qiladi.

Qisqacha: dashboard foydalanuvchilari PDF hujjatlar (matn + API JSON namunalari, o'zbek/rus) bo'yicha savol beradi. Dashboard DB siga aloqa yo'q, Text-to-SQL yo'q, JWT yo'q, bitta umumiy hujjat to'plami.

## Stack

.NET 8, Clean Architecture, Minimal API (controller emas), EF Core 8 + Npgsql + `Pgvector.EntityFrameworkCore`, `EFCore.NamingConventions` (snake_case), PostgreSQL 16 + pgvector, MinIO (`Minio` 6.0.5), PdfPig 0.1.10, Ollama `bge-m3` (1024 o'lcham), OpenAI-compatible LLM (OpenRouter → keyin lokal Qwen, faqat config), Serilog, Swashbuckle. MediatR/FluentValidation **ishlatilmaydi** — har use-case oddiy `XxxHandler` klassi (`HandleAsync`), `AddApplication()` da qo'lda ro'yxatdan o'tadi.

## Tuzilma

```
Rag.sln
src/Rag.Domain/          Entity lar (behavior ichida), enum lar, DomainException. Hech narsaga bog'liq emas.
src/Rag.Application/     Abstractions/ (interfeyslar), Documents/, Chat/, Ingestion/IngestionPipeline, Options/RagOptions,
                         Common/Exceptions (NotFound, Conflict, Validation, Unauthorized, ServiceUnavailable)
src/Rag.Infrastructure/  Persistence/ (AppDbContext, Configurations, Migrations), VectorStore/PgVectorStore (raw Npgsql SQL),
                         Embedding/ (Ollama), Llm/ (OpenAI-compatible), Pdf/ (extractor + chunker), Storage/ (MinIO),
                         Background/ (Channel queue + IngestionWorker), Options/, DependencyInjection.cs
src/Rag.Api/             Program.cs, Endpoints/ (Documents, Chat), Security/ (ApiKey filter, HttpCurrentUser, Swagger),
                         Middleware/ExceptionHandler (exception → ProblemDetails), Dockerfile
docker-compose.yml, .env.example, README.md (foydalanuvchi uchun ishga tushirish qo'llanmasi)
```

Application `Microsoft.EntityFrameworkCore.Relational` ga bog'liq (`IAppDbContext` DbSet lar, `ExecuteDeleteAsync`). Infrastructure `FrameworkReference Microsoft.AspNetCore.App` ga ega.

## Muhim dizayn qarorlari

- **Versiyalash:** `documents.current_version` — qidiruvdagi faol versiya. Yangi fayl → yangi `DocumentVersion` (Pending). Worker: chunk INSERT + `Activate(v)` + `MarkReady` + eski versiya chunk larini DELETE — bitta tranzaksiyada. Keyin eski MinIO fayllari o'chiriladi (joriy storage_key bilan bir xil bo'lmasa).
- **Status:** `Pending → Processing → Ready/Failed`. Vaqtinchalik xato → `ScheduleRetry` + `EnqueueDelayed` (30s × attempt), `MaxRetries` dan keyin `MarkFailed`. `IngestionException` (skaner PDF, chunk yo'q) — darhol Failed. Server restartida `IngestionWorker` Pending/Processing larni qayta navbatga qo'yadi (alohida job jadvali yo'q).
- **Delete:** chunk lar qattiq o'chadi, `documents` soft delete, keyin MinIO fayllari (xato bo'lsa faqat log).
- **Reindex** (`/api/admin/reindex`): har faol hujjat uchun joriy fayldan (o'sha storage_key) yangi versiya — PDF qayta o'qiladi (hujjatdan farq: content dan qayta embedding emas).
- **Id lar** domen ichida `Guid.NewGuid()`; `AppDbContext` da barcha `BaseEntity.Id` uchun `ValueGeneratedNever()` — navigatsiya orqali qo'shilgan yangi entity lar Added bo'lishi uchun. (Bu snapshot ga hali migratsiya qilinmagan; sxema o'zgarmaydi.)
- **Embedding matni:** `section_title + "\n" + content`; DB ga `content` alohida.
- **Qidiruv:** `PgVectorStore` — vector (cosine, `MinScore` C# da filtr) yoki `Hybrid=true` bo'lsa vector + tsvector (`'simple'`, so'zlar OR) RRF (k=60). Vector parametri matn literal `'[..]'::vector` sifatida uzatiladi.
- **Chunking** (`Pdf/`): `PdfPigTextExtractor` (qatorlar: matn, shrift o'lchami, bold, paragraf boshi) → `TextNormalizer` → `HeaderFooterFilter` → `StructureAwareChunker` (sarlavha darajasi shrift o'lchamidan; JSON `BraceScanner` balansi bilan) → `SectionChunkBuilder` → `TextSplitter` (~1000 belgi, gap overlap) / `JsonChunkSplitter` (≤3000 bitta; katta — top-level kalit/element bo'yicha, "(i/n qism)" sarlavha bilan).
- **Chat:** savol LLM dan oldin saqlanadi; topilmasa LLM chaqirilmaydi, "Ma'lumot topilmadi"/"Информация не найдена" (til: kirill > lotin → ru, lekin ў қ ғ ҳ → uz). `message_sources` — snapshot, chunk ga FK yo'q. LLM: 2 retry (429/5xx/timeout), keyin 503.
- **Xavfsizlik:** chat — `X-User-Id` header (`ICurrentUser`), har so'rovda `session.user_id` tekshiriladi; hujjat o'zgartiruvchi endpointlar va reindex — `X-Api-Key` (`Security:AdminApiKey`, bo'sh bo'lsa tekshirilmaydi); chat rate limit (`Security:ChatRequestsPerMinute`, user bo'yicha).
- **Health:** `/health` (postgres, minio, ollama), `/health/live` (Docker healthcheck).
- Migratsiya startda: `Database:MigrateOnStartup`.

## Docker

`docker-compose.yml`: `postgres` (pgvector:pg16, host 5433), `minio` (9000/9001), `minio-init` (bucket yaratadi), `ollama` (profil `ollama`, GPU, ixtiyoriy — `api` da `required: false`), `api` (`API_IMAGE`, 8080). Sirlar `.env` da; compose ularni `ConnectionStrings__Db`, `Minio__*`, `Embedding__*`, `Llm__*`, `Security__*` env larga aylantiradi. Ollama hostda bo'lsa: `COMPOSE_PROFILES=` va `EMBEDDING_BASE_URL=http://host.docker.internal:11434`.

## Holat (2026-10-14)

Bajarilgan (hujjat 15-bo'lim): 1 skelet, 2 upload+ingestion, 3 chat, 4 update/delete/download/retry, 5 chat tarixi, 7 X-Api-Key / rate limit / health check, hybrid search (flag bilan).

Build xatolari tuzatilgan: Application da `ExecuteDeleteAsync` uchun EF Relational paketi.

2026-10-17: Ollama/LLM xato body lari logga va xabarga chiqadi; `/health` embedding modeli pull qilinganini tekshiradi (`api/tags`); system prompt ga [n] iqtibos, JSON code block, qisman javob qoidalari qo'shildi.

Xotiradan yozilgan, build da tekshirilishi kerak bo'lgan API lar: MinIO `GetObjectArgs.WithCallbackStream(Func<Stream, CancellationToken, Task>)`, PdfPig `Letter.Font.IsBold`.

Qilinmagan: streaming (SSE), query rewriting, reranker, qo'shni chunk lar, eval to'plami, yetim MinIO fayllar tozalovchi job, testlar (`tests/` yo'q), OCR.

Ochiq savollar (hujjat 18): userId formati/uzatilishi, hujjat boshqaruviga kim kiradi, chat tarixi muddati, PDF da JSON qanday yozilgan (namuna sahifa).

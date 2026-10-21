# Dashboard AI Assistant (RAG) — arxitektura hujjati

Dashboard foydalanuvchilari PDF hujjatlar (matn + API JSON namunalari, o'zbek/rus) bo'yicha savol beradi.
Tizim savolga mos hujjat bo'laklarini (chunk) topadi va LLM shu bo'laklar asosida javob yozadi, manbalarni ko'rsatadi.

- Talablar: `RAG_Analysis.md` (v2)
- Ishga tushirish: [README.md](../README.md)

## Mundarija

1. [Umumiy sxema](#1-umumiy-sxema)
2. [Qatlamlar va bog'liqlik qoidalari](#2-qatlamlar-va-bogliqlik-qoidalari)
3. [Papka tuzilmasi](#3-papka-tuzilmasi)
4. [Integratsiyalar — qayerda va qanday](#4-integratsiyalar--qayerda-va-qanday)
5. [Handler katalogi](#5-handler-katalogi)
6. [Endpoint oqimlari](#6-endpoint-oqimlari)
7. [Ingestion — fon jarayoni](#7-ingestion--fon-jarayoni)
8. [Ma'lumotlar bazasi](#8-malumotlar-bazasi)
9. [Domen modeli](#9-domen-modeli)
10. [Xatolar va HTTP kodlar](#10-xatolar-va-http-kodlar)
11. [Konfiguratsiya](#11-konfiguratsiya)
12. [DI ro'yxati — nima qayerda ro'yxatdan o'tadi](#12-di-royxati--nima-qayerda-royxatdan-otadi)
13. [Docker](#13-docker)
14. [Yangi funksiya qo'shish](#14-yangi-funksiya-qoshish)
15. [Qilinmagan](#15-qilinmagan)

---

## 1. Umumiy sxema

```
 Dashboard backend / Swagger
          │  HTTP (X-Api-Key, X-User-Id)
          ▼
 ┌────────────────────────────── Rag.Api ───────────────────────────────┐
 │ Program.cs → Endpoints/ → filter (ApiKey, RateLimit) → Handler       │
 │ Middleware/ExceptionHandler: exception → ProblemDetails              │
 └───────────────────────────────┬──────────────────────────────────────┘
                                 ▼
 ┌─────────────────────────── Rag.Application ──────────────────────────┐
 │ Documents/, Chat/  — XxxHandler.HandleAsync (use-case)               │
 │ Ingestion/IngestionPipeline — PDF → chunk → embedding → DB           │
 │ Abstractions/ — faqat interfeyslar                                   │
 └───────────────────────────────┬──────────────────────────────────────┘
                                 ▼              (Domain: entity + qoidalar)
 ┌────────────────────────── Rag.Infrastructure ────────────────────────┐
 │ Persistence/AppDbContext ────────────────▶ PostgreSQL 16             │
 │ VectorStore/PgVectorStore ───────────────▶ pgvector + tsvector       │
 │ Storage/MinioFileStorage ────────────────▶ MinIO (PDF fayllar)       │
 │ Embedding/OllamaEmbeddingService ────────▶ Ollama bge-m3 /api/embed  │
 │ Llm/OpenAiCompatibleLlmClient ───────────▶ OpenRouter /chat/compl.   │
 │ Pdf/ (PdfPig + chunker)                                              │
 │ Background/ChannelIngestionQueue + IngestionWorker (fon)             │
 └──────────────────────────────────────────────────────────────────────┘
```

Ikki asosiy oqim:

| Oqim | Qisqacha |
|---|---|
| **Indekslash** | PDF yuklanadi → MinIO → fon worker: matn → chunk → embedding (Ollama) → `document_chunks` |
| **Savol-javob** | savol → embedding (Ollama) → pgvector qidiruv → prompt → OpenRouter → javob + manbalar |

---

## 2. Qatlamlar va bog'liqlik qoidalari

```
Rag.Api ──▶ Rag.Application ──▶ Rag.Domain
   │              ▲
   └──▶ Rag.Infrastructure (Application interfeyslarini implement qiladi)
```

| Loyiha | Nimaga bog'liq | Nima bor |
|---|---|---|
| `Rag.Domain` | hech narsaga | Entity lar va ularning qoidalari (metodlar), enum lar, `DomainException` |
| `Rag.Application` | Domain, `EF Core Relational` (faqat `DbSet` / `ExecuteDeleteAsync` uchun) | Handler lar, interfeyslar, DTO, options, ingestion pipeline, prompt |
| `Rag.Infrastructure` | Application | Interfeyslarning implementatsiyasi: DB, MinIO, Ollama, OpenRouter, PDF, fon worker |
| `Rag.Api` | Application, Infrastructure | HTTP: endpointlar, xavfsizlik, xato formatlash, DI yig'ish |

### "Service" va "Repository" qayerda?

Alohida `XxxService` / `XxxRepository` klasslari **yo'q** — ortiqcha abstraksiya qilinmagan. MediatR ham yo'q.

| Klassik qatlam | Bu loyihada |
|---|---|
| Controller | `Rag.Api/Endpoints/*Endpoints.cs` (Minimal API) |
| Service (biznes logika) | `Rag.Application/**/XxxHandler` — bitta use-case = bitta klass, `HandleAsync` |
| Domen qoidalari | `Rag.Domain/Entities/*` metodlari (`AddVersion`, `Activate`, `MarkReady`, ...) |
| Repository + Unit of Work | `IAppDbContext` (EF Core `DbSet` lar + `SaveChangesAsync`) |
| Tashqi servis klientlari | `Abstractions/I*` → `Rag.Infrastructure/*` implementatsiyalari |

Zanjir: **Endpoint → Handler → Domain → Interfeys → Implementatsiya → DB / MinIO / Ollama / OpenRouter**.

---

## 3. Papka tuzilmasi

```
src/
├── Rag.Domain/
│   ├── Common/          BaseEntity (Guid Id = Guid.NewGuid()), DomainException
│   ├── Entities/        Document, DocumentVersion, DocumentChunk, ChatSession, ChatMessage, MessageSource
│   └── Enums/           VersionStatus (Pending/Processing/Ready/Failed), ChunkType (Text/Json), MessageRole
│
├── Rag.Application/
│   ├── Abstractions/    IAppDbContext, IVectorStore, IFileStorage, IEmbeddingService, ILlmClient,
│   │                    IPdfTextExtractor, IChunker, IIngestionQueue, ICurrentUser
│   ├── Documents/
│   │   ├── Commands/    UploadDocument, ReplaceDocumentFile, UpdateDocumentInfo, DeleteDocument,
│   │   │                RetryIndexing, ReindexAll
│   │   ├── Queries/     GetDocuments, GetDocument, GetDocumentStatus, DownloadDocument
│   │   ├── PdfFile.cs           PDF validatsiya + SHA-256 + storage key
│   │   ├── DocumentQueries.cs   GetActiveWithVersionsAsync (umumiy so'rov)
│   │   └── DocumentDtos.cs
│   ├── Chat/
│   │   ├── Commands/    AskQuestion, DeleteSession
│   │   ├── Queries/     GetMySessions, GetSessionMessages
│   │   ├── PromptBuilder.cs     system prompt + kontekst + tarix, til aniqlash
│   │   └── ChatDtos.cs
│   ├── Ingestion/       IngestionPipeline, IngestionException
│   ├── Options/         RagOptions (Chunking, Retrieval, Ingestion)
│   ├── Common/          Exceptions/AppExceptions.cs, CurrentUserExtensions (RequireUserId)
│   └── DependencyInjection.cs   AddApplication()
│
├── Rag.Infrastructure/
│   ├── Persistence/     AppDbContext, Configurations/ (har entity), Migrations/
│   ├── VectorStore/     PgVectorStore
│   ├── Embedding/       OllamaEmbeddingService, OllamaHealthCheck
│   ├── Llm/             OpenAiCompatibleLlmClient
│   ├── Pdf/             PdfPigTextExtractor, TextNormalizer, HeaderFooterFilter, StructureAwareChunker,
│   │                    SectionChunkBuilder, TextSplitter, JsonBlockDetector, BraceScanner, JsonChunkSplitter
│   ├── Storage/         MinioFileStorage, MinioHealthCheck
│   ├── Background/      ChannelIngestionQueue, IngestionWorker
│   ├── Options/         EmbeddingOptions, LlmOptions, MinioOptions
│   └── DependencyInjection.cs   AddInfrastructure()
│
└── Rag.Api/
    ├── Program.cs       DI, Serilog, upload limiti, rate limit, migratsiya, Swagger, health, endpointlar
    ├── Endpoints/       DocumentsEndpoints, ChatEndpoints
    ├── Security/        ApiKeyEndpointFilter, HttpCurrentUser, SecurityOptions, SwaggerSetup
    ├── Middleware/      ExceptionHandler
    └── Dockerfile
```

---

## 4. Integratsiyalar — qayerda va qanday

Har bir tashqi tizim Application dagi **interfeys** orqali ishlatiladi; implementatsiya faqat Infrastructure da.
Handler lar tashqi tizim haqida hech narsa bilmaydi — almashtirish faqat DI va config da.

### Umumiy xarita

| Tizim | Interfeys (Application) | Implementatsiya (Infrastructure) | Config | Kim ishlatadi |
|---|---|---|---|---|
| PostgreSQL (EF Core) | `IAppDbContext` | `Persistence/AppDbContext.cs` | `ConnectionStrings:Db` | Barcha handler lar, `IngestionPipeline`, `IngestionWorker` |
| pgvector qidiruv | `IVectorStore` | `VectorStore/PgVectorStore.cs` | `Rag:Retrieval` | `AskQuestionHandler` |
| MinIO | `IFileStorage` | `Storage/MinioFileStorage.cs` | `Minio` | Upload, Replace, Delete, Download handler lar, `IngestionPipeline` |
| Ollama (embedding) | `IEmbeddingService` | `Embedding/OllamaEmbeddingService.cs` | `Embedding` | `AskQuestionHandler`, `IngestionPipeline` |
| OpenRouter (LLM) | `ILlmClient` | `Llm/OpenAiCompatibleLlmClient.cs` | `Llm` | `AskQuestionHandler` |
| PdfPig | `IPdfTextExtractor` | `Pdf/PdfPigTextExtractor.cs` | — | `IngestionPipeline` |
| Chunking | `IChunker` | `Pdf/StructureAwareChunker.cs` (+ yordamchilar) | `Rag:Chunking` | `IngestionPipeline` |
| Fon navbat | `IIngestionQueue` | `Background/ChannelIngestionQueue.cs` + `IngestionWorker.cs` | `Rag:Ingestion` | Upload, Replace, Retry, ReindexAll, `IngestionPipeline` (retry) |
| Joriy foydalanuvchi | `ICurrentUser` | `Rag.Api/Security/HttpCurrentUser.cs` | — | Chat handler lar |

### 4.1 PostgreSQL — `AppDbContext`

| | |
|---|---|
| Fayl | `Rag.Infrastructure/Persistence/AppDbContext.cs`, `Configurations/*Configuration.cs` |
| Ulanish | `UseNpgsql(connectionString, npgsql => npgsql.UseVector()).UseSnakeCaseNamingConvention()` |
| Xususiyatlar | `vector` extension; barcha `Id` lar `ValueGeneratedNever` (Id domen ichida yaratiladi); enum lar `string` sifatida |
| Migratsiya | `Persistence/Migrations/`; startda `Database:MigrateOnStartup=true` bo'lsa `Program.cs` qo'llaydi |
| Tranzaksiyalar | `IngestionPipeline.SaveAsync` (chunk almashtirish), `DeleteDocumentHandler` |
| Health | `AddNpgSql(connectionString, name: "postgres")` |

### 4.2 pgvector qidiruv — `PgVectorStore`

| | |
|---|---|
| Fayl | `Rag.Infrastructure/VectorStore/PgVectorStore.cs` |
| Usul | Raw `NpgsqlCommand` (EF ulanishi orqali). Vektor matn literal sifatida: `'[0.1,...]'::vector` |
| Vector rejim (`Hybrid=false`) | `ORDER BY embedding <=> @embedding LIMIT @top_k`, `score = 1 - cosine masofa`, `MinScore` C# da filtrlanadi |
| Hybrid rejim (`Hybrid=true`) | vector top-20 + `tsvector('simple')` kalit so'z top-20 (so'zlar OR) → RRF `1/(60+rank)` yig'indisi; `similarity ≥ MinScore` yoki kalit so'z mos kelgan |
| Filtr | faqat `c.document_version = d.current_version` va `d.deleted_at IS NULL` |
| Indekslar | HNSW `vector_cosine_ops` (`embedding`), GIN (`search_vector`) |

### 4.3 MinIO — `MinioFileStorage`

| | |
|---|---|
| Fayl | `Rag.Infrastructure/Storage/MinioFileStorage.cs` |
| Klient | `IMinioClient` singleton, `DependencyInjection.cs` da `MinioOptions` dan yasaladi (`Minio` 6.0.5) |
| Metodlar | `SaveAsync` (PutObject), `OpenReadAsync` (GetObject → `MemoryStream`), `DeleteAsync` (RemoveObject) |
| Kalit formati | `{documentId}/v{version}/{fileName}` (`PdfFile.StorageKey`) |
| Xato | `ObjectNotFoundException` → `NotFoundException` (404) |
| Health | `MinioHealthCheck` — bucket mavjudligi |
| Bucket | Docker da `minio-init` yaratadi (`mc mb --ignore-existing`) |

### 4.4 Ollama (embedding) — `OllamaEmbeddingService`

| | |
|---|---|
| Fayl | `Rag.Infrastructure/Embedding/OllamaEmbeddingService.cs` |
| HTTP | `POST {Embedding:BaseUrl}/api/embed`  body `{"model":"bge-m3","input":["...", "..."]}` → `{"embeddings":[[1024 float], ...]}` |
| HttpClient | Typed client, `BaseAddress` va `Timeout` (`Embedding:TimeoutSeconds`, 300s) — `DependencyInjection.ConfigureOllama` |
| Tekshiruv | embedding soni = matnlar soni; har vektor o'lchami = `Embedding:Dimensions` (1024) |
| Xato | non-2xx → `HttpRequestException` (Ollama xato matni bilan) → chatda 503, ingestion da retry |
| Matn | ingestion: `section_title + "\n" + content`; chat: savolning o'zi |
| Batch | `Rag:Ingestion:EmbeddingBatchSize` (16) |
| Health | `OllamaHealthCheck` — `GET /api/tags` va ro'yxatda `Embedding:Model` borligi (`bge-m3` = `bge-m3:latest`) |
| Diqqat | Model o'zgarsa va o'lcham boshqa bo'lsa — yangi migratsiya (`vector(N)`) + `/api/admin/reindex` |

### 4.5 OpenRouter (LLM) — `OpenAiCompatibleLlmClient`

| | |
|---|---|
| Fayl | `Rag.Infrastructure/Llm/OpenAiCompatibleLlmClient.cs` |
| HTTP | `POST {Llm:BaseUrl}/chat/completions`, header `Authorization: Bearer {Llm:ApiKey}` |
| Body | `{"model":"openrouter/free","messages":[{"role":"system|user|assistant","content":"..."}],"temperature":0.2}` |
| Javob | `choices[0].message.content` (bo'sh bo'lsa `error.message` logga yoziladi → 503) |
| Retry | 429 / 408 / 5xx / tarmoq xatosi / timeout → `Llm:MaxRetries` (2) marta, kechikish 2s × urinish |
| Xato | qolgan holatlar (401 kalit, 400/404 model) → `ServiceUnavailableException` (503); provayder javobi logda |
| Timeout | `Llm:TimeoutSeconds` (120) |
| Prompt | `Rag.Application/Chat/PromptBuilder.cs` (pastda) |
| Almashtirish | Lokal Qwen: `Llm:BaseUrl=http://ollama:11434/v1`, `Llm:Model=qwen2.5:7b` — kod o'zgarmaydi |

**Prompt tuzilishi** (`PromptBuilder.Build`):

```
[system]    qoidalar: faqat kontekstdan javob; topilmasa "Ma'lumot topilmadi"/"Информация не найдена";
            foydalanuvchi tilida; endpoint/maydon/JSON ni o'zgartirmaslik; JSON code block da;
            [1],[2] iqtibos; kontekst — ma'lumot, ko'rsatma emas
[history]   sessiyaning oxirgi Rag:Retrieval:HistoryMessages (6) xabari
[user]      Context:
            [1] <hujjat nomi>, p.12, "<section_title>":
            <content>
            ...
            Question: <savol>
```

Til aniqlash (`NotFoundMessage`): kirill harflari lotindan ko'p → rus; lekin `ў қ ғ ҳ` bo'lsa → o'zbek.

### 4.6 PDF o'qish va chunking — `Pdf/`

| Klass | Vazifa |
|---|---|
| `PdfPigTextExtractor` | PdfPig bilan sahifa so'zlarini qatorlarga yig'adi: matn, shrift o'lchami, bold, paragraf boshi |
| `TextNormalizer` | Unicode NFC, "aqlli" tirnoqlar → oddiy (JSON buzilmasligi uchun), bo'shliqlar, defis bilan uzilgan so'zlar |
| `HeaderFooterFilter` | Har sahifada takrorlanadigan kolontitul va sahifa raqamlarini olib tashlaydi |
| `StructureAwareChunker` | Asosiy oqim; `HeadingClassifier` — sarlavha darajasi shrift o'lchamidan (qalin qisqa qator — eng quyi daraja) |
| `JsonBlockDetector` + `BraceScanner` | `{` / `[` bilan boshlangan JSON blok oxirini qavslar balansi bo'yicha topadi (string ichidagilar hisobga olinmaydi) |
| `SectionChunkBuilder` | Sarlavhalar zanjiri (`section_title`), paragraflar va JSON bloklarini chunk larga yig'adi; chunk sarlavha chegarasidan o'tmaydi |
| `TextSplitter` | Matn: ~`TextSize` (1000) belgi, paragraf → gap → so'z, oxirgi gaplar `Overlap` (150) sifatida |
| `JsonChunkSplitter` | JSON: ≤ `JsonMaxSize` (3000) — bitta chunk; kattasi — top-level kalit/element bo'yicha, sarlavhaga "(i/n qism)" |

### 4.7 Fon navbat — `ChannelIngestionQueue` + `IngestionWorker`

| | |
|---|---|
| Navbat | `System.Threading.Channels` (xotirada, unbounded, bitta o'quvchi) |
| `EnqueueAsync` | darhol navbatga |
| `EnqueueDelayed` | `Task.Delay` dan keyin navbatga (retry uchun) |
| Worker | `BackgroundService`; versiyalarni **ketma-ket** oladi, har biri uchun yangi DI scope va `IngestionPipeline.ProcessAsync` |
| Restart | Startda `Pending`/`Processing` versiyalarni DB dan qayta navbatga qo'yadi (alohida job jadvali yo'q) |

### 4.8 Xavfsizlik — `Rag.Api/Security/`

| Mexanizm | Fayl | Qo'llaniladi |
|---|---|---|
| `X-Api-Key` | `ApiKeyEndpointFilter` — `FixedTimeEquals`, `Security:AdminApiKey` bo'sh bo'lsa tekshirilmaydi | Hujjat o'zgartiruvchi endpointlar, `/api/admin/reindex` |
| `X-User-Id` | `HttpCurrentUser` (`ICurrentUser`) → `RequireUserId()` | Chat handler lar; sessiya `user_id` bilan solishtiriladi |
| Rate limit | `Program.cs` — fixed window, `X-User-Id` (yo'q bo'lsa IP) bo'yicha, `Security:ChatRequestsPerMinute` | `POST /api/chat` |
| Upload limiti | `Program.cs` — Kestrel `MaxRequestBodySize` va multipart = `MaxFileSizeMb + 1` MB | Fayl yuklash |
| Swagger | `SwaggerSetup` — Authorize oynasida ikkala header | `/swagger` |

### 4.9 Log va kuzatuv

Serilog (`appsettings.json → Serilog`), `UseSerilogRequestLogging`. Muhim loglar: indekslash natijasi
(sahifa/chunk soni), ingestion xatolari va retry, LLM/Ollama provayder xato matnlari. `chat_messages.latency_ms`, `llm_model`.

---

## 5. Handler katalogi

Barchasi `Rag.Application/DependencyInjection.cs` da `AddScoped` bilan ro'yxatdan o'tadi.

### 5.1 Hujjatlar — Commands

| Handler | Bog'liqliklar | Nima qiladi | DB | Tashqi | Xatolar |
|---|---|---|---|---|---|
| `UploadDocumentHandler`<br>`Documents/Commands/UploadDocument.cs` | `IAppDbContext`, `IFileStorage`, `IIngestionQueue`, `RagOptions` | PDF ni tekshiradi, dublikatni (hash) rad etadi, `Document` + v1 yaratadi, faylni saqlaydi, navbatga qo'yadi | INSERT `documents`, `document_versions` | MinIO PutObject | 400 (PDF emas/katta/bo'sh), 409 (dublikat) |
| `ReplaceDocumentFileHandler`<br>`ReplaceDocumentFile.cs` | `IAppDbContext`, `IFileStorage`, `IIngestionQueue`, `RagOptions` | Yangi fayl → yangi versiya (Pending). Hash oxirgi versiyaniki bilan bir xil bo'lsa hech narsa qilmaydi | INSERT `document_versions` | MinIO PutObject | 404, 400 |
| `UpdateDocumentInfoHandler`<br>`UpdateDocumentInfo.cs` | `IAppDbContext` | `title`, `description` ni o'zgartiradi (`Document.SetInfo`) | UPDATE `documents` | — | 404, 400 (bo'sh nom) |
| `DeleteDocumentHandler`<br>`DeleteDocument.cs` | `IAppDbContext`, `IFileStorage`, `ILogger` | Tranzaksiya: chunk larni o'chiradi + soft delete; keyin MinIO fayllari | DELETE `document_chunks`, UPDATE `documents.deleted_at` | MinIO RemoveObject (xato — faqat log) | 404 |
| `RetryIndexingHandler`<br>`RetryIndexing.cs` | `IAppDbContext`, `IIngestionQueue` | Oxirgi versiya `Failed` bo'lsa → `Retry()` (Pending, attempts=0) va navbat | UPDATE `document_versions` | — | 404, 409 (Failed emas) |
| `ReindexAllHandler`<br>`ReindexAll.cs` | `IAppDbContext`, `IIngestionQueue` | Har faol hujjat uchun joriy fayldan (o'sha `storage_key`) yangi versiya va navbat | INSERT `document_versions` | — | — |

### 5.2 Hujjatlar — Queries

| Handler | Bog'liqliklar | Nima qaytaradi | DB | Tashqi |
|---|---|---|---|---|
| `GetDocumentsHandler` | `IAppDbContext` | Faol hujjatlar ro'yxati + oxirgi versiya statusi (`updated_at DESC`) | SELECT `documents`, `document_versions` | — |
| `GetDocumentHandler` | `IAppDbContext` | Hujjat + barcha versiyalari (yangisi birinchi) | SELECT | — |
| `GetDocumentStatusHandler` | `IAppDbContext` | `currentVersion`, oxirgi versiya `status`, `attempts`, `errorMessage` | SELECT | — |
| `DownloadDocumentHandler` | `IAppDbContext`, `IFileStorage` | Faol (yo'q bo'lsa oxirgi) versiya PDF fayli | SELECT | MinIO GetObject |

Umumiy yordamchilar: `DocumentQueries.GetActiveWithVersionsAsync` (hujjat + versiyalar, o'chirilgan bo'lsa 404),
`PdfFile.ReadAsync` (`.pdf`, bo'sh emas, ≤ `MaxFileSizeMb`, `%PDF-` magic bytes, SHA-256, xavfsiz fayl nomi).

### 5.3 Chat

| Handler | Bog'liqliklar | Nima qiladi | DB | Tashqi | Xatolar |
|---|---|---|---|---|---|
| `AskQuestionHandler`<br>`Chat/Commands/AskQuestion.cs` | `IAppDbContext`, `ICurrentUser`, `IEmbeddingService`, `IVectorStore`, `ILlmClient`, `RagOptions` | Sessiya topadi/yaratadi, savolni saqlaydi, embedding → qidiruv → prompt → LLM, javob va manbalarni saqlaydi | SELECT/INSERT `chat_sessions`, `chat_messages`, `message_source`; qidiruv `document_chunks` | Ollama `/api/embed`, OpenRouter `/chat/completions` | 400, 401, 404 (sessiya), 429, 503 |
| `GetMySessionsHandler`<br>`Chat/Queries/GetMySessions.cs` | `IAppDbContext`, `ICurrentUser` | Foydalanuvchi sessiyalari (`updated_at DESC`) | SELECT `chat_sessions` | — | 401 |
| `GetSessionMessagesHandler`<br>`GetSessionMessages.cs` | `IAppDbContext`, `ICurrentUser` | Sessiya xabarlari + manbalari (vaqt bo'yicha) | SELECT `chat_messages`, `message_source` | — | 401, 404 |
| `DeleteSessionHandler`<br>`Chat/Commands/DeleteSession.cs` | `IAppDbContext`, `ICurrentUser` | Sessiyani o'chiradi (cascade: xabarlar, manbalar) | DELETE `chat_sessions` | — | 401, 404 |

### 5.4 Ingestion

| Klass | Bog'liqliklar | Nima qiladi | DB | Tashqi |
|---|---|---|---|---|
| `IngestionPipeline`<br>`Ingestion/IngestionPipeline.cs` | `IAppDbContext`, `IFileStorage`, `IPdfTextExtractor`, `IChunker`, `IEmbeddingService`, `IIngestionQueue`, `RagOptions`, `ILogger` | Versiyani indekslaydi: PDF → sahifalar → chunk → embedding → atomik almashtirish; xatoda retry/Failed | UPDATE `document_versions`, `documents`; INSERT/DELETE `document_chunks` | MinIO GetObject/RemoveObject, Ollama `/api/embed` |

---

## 6. Endpoint oqimlari

`[X-Api-Key]` — admin kaliti, `[X-User-Id]` — foydalanuvchi.

### 6.1 `POST /api/documents` — PDF yuklash

```
POST /api/documents (multipart: file, title, description?)   [X-Api-Key]
 └─ ApiKeyEndpointFilter                                       noto'g'ri → 401
 └─ DocumentsEndpoints → UploadDocumentHandler.HandleAsync
     ├─ PdfFile.ReadAsync                    validatsiya, SHA-256           → 400
     ├─ IAppDbContext.Documents.AnyAsync     shu hash bormi?                 → 409
     ├─ new Document(title, description)     (Domain)
     ├─ IFileStorage.SaveAsync ────────────▶ MinioFileStorage ──▶ MinIO {docId}/v1/{file}
     ├─ document.AddVersion(...)             (Domain) v1, Pending
     ├─ SaveChangesAsync ──────────────────▶ AppDbContext ──▶ INSERT documents, document_versions
     └─ IIngestionQueue.EnqueueAsync ──────▶ ChannelIngestionQueue ──▶ IngestionWorker (§7)
 ◀─ 202 { documentId, version: 1, status: "Pending" }   Location: /api/documents/{id}/status
```

### 6.2 `GET /api/documents` — ro'yxat

```
GET /api/documents
 └─ GetDocumentsHandler ──▶ IAppDbContext (AsNoTracking) ──▶ SELECT documents + oxirgi versiya statusi
 ◀─ 200 [ { id, title, description, currentVersion, latestStatus, updatedAt } ]
```

### 6.3 `GET /api/documents/{id}` — hujjat va versiyalari

```
GET /api/documents/{id}
 └─ GetDocumentHandler ──▶ GetActiveWithVersionsAsync ──▶ documents JOIN document_versions   → 404
 ◀─ 200 { id, title, description, currentVersion, createdAt, updatedAt,
          versions: [ { version, fileName, fileSize, pageCount, chunkCount, status, attempts, errorMessage, ... } ] }
```

### 6.4 `GET /api/documents/{id}/status` — indekslash holati

```
GET /api/documents/{id}/status
 └─ GetDocumentStatusHandler ──▶ GetActiveWithVersionsAsync → LatestVersion
 ◀─ 200 { documentId, currentVersion, latestVersion, status, attempts, errorMessage }
```

### 6.5 `GET /api/documents/{id}/download` — asl PDF

```
GET /api/documents/{id}/download
 └─ DownloadDocumentHandler
     ├─ GetActiveWithVersionsAsync → ActiveVersion ?? LatestVersion
     └─ IFileStorage.OpenReadAsync ──▶ MinioFileStorage ──▶ MinIO GetObject              → 404
 ◀─ 200 application/pdf
```

### 6.6 `PUT /api/documents/{id}` — nom/izoh

```
PUT /api/documents/{id}  { title, description }   [X-Api-Key]
 └─ UpdateDocumentInfoHandler
     ├─ db.Documents.FirstOrDefaultAsync                                                  → 404
     ├─ document.SetInfo(...)  (Domain)                                                   → 400
     └─ SaveChangesAsync ──▶ UPDATE documents
 ◀─ 200
```

### 6.7 `PUT /api/documents/{id}/file` — faylni almashtirish

```
PUT /api/documents/{id}/file (multipart: file)   [X-Api-Key]
 └─ ReplaceDocumentFileHandler
     ├─ GetActiveWithVersionsAsync, PdfFile.ReadAsync
     ├─ hash == oxirgi versiya? ─────────────▶ 200 { changed: false }
     ├─ IFileStorage.SaveAsync ──▶ MinIO {docId}/v{N}/{file}
     ├─ document.AddVersion → v{N} Pending, SaveChangesAsync
     └─ IIngestionQueue.EnqueueAsync
 ◀─ 202 { documentId, version, status }
```

Yangi versiya `Ready` bo'lguncha qidiruv eski versiyada ishlaydi.

### 6.8 `POST /api/documents/{id}/retry`

```
POST /api/documents/{id}/retry   [X-Api-Key]
 └─ RetryIndexingHandler
     ├─ LatestVersion.Status != Failed                                                   → 409
     ├─ version.Retry() (Domain), SaveChangesAsync
     └─ IIngestionQueue.EnqueueAsync
 ◀─ 202 { documentId, version, status: "Pending" }
```

### 6.9 `DELETE /api/documents/{id}`

```
DELETE /api/documents/{id}   [X-Api-Key]
 └─ DeleteDocumentHandler
     ├─ GetActiveWithVersionsAsync                                                        → 404
     ├─ TRANZAKSIYA: DELETE document_chunks; document.Delete() → deleted_at; COMMIT
     └─ har versiya: IFileStorage.DeleteAsync ──▶ MinIO (xato → faqat log)
 ◀─ 204
```

### 6.10 `POST /api/admin/reindex`

```
POST /api/admin/reindex   [X-Api-Key]
 └─ ReindexAllHandler
     ├─ faol hujjatlar (current_version > 0) → AddVersion(joriy fayl) har biriga
     ├─ SaveChangesAsync
     └─ har versiya → IIngestionQueue.EnqueueAsync
 ◀─ 202 { queued: N }
```

### 6.11 `POST /api/chat` — savol (asosiy RAG oqimi)

```
POST /api/chat { sessionId?, question }   [X-User-Id]
 └─ RateLimiter (user bo'yicha)                                                          → 429
 └─ ChatEndpoints → AskQuestionHandler.HandleAsync
     ├─ ICurrentUser.RequireUserId()                                                      → 401
     ├─ validatsiya: bo'sh emas, ≤ 2000 belgi                                             → 400
     ├─ sessiya: sessionId bor → db.ChatSessions (id + user_id)                           → 404
     │           yo'q        → new ChatSession(userId, question)   (title = savol)
     ├─ tarix: oxirgi 6 xabar
     ├─ session.AddUserMessage + SaveChangesAsync ──▶ INSERT chat_messages   (LLM dan OLDIN)
     │
     ├─ IEmbeddingService.EmbedAsync([question]) ──▶ Ollama POST /api/embed → float[1024]   → 503
     ├─ IVectorStore.SearchAsync(TopK 8, MinScore 0.35, Hybrid) ──▶ PgVectorStore ──▶ SQL
     │
     ├─ 0 ta natija → "Ma'lumot topilmadi." / "Информация не найдена."  (LLM chaqirilmaydi)
     │  bor        → PromptBuilder.Build(hits, history, question)
     │              → ILlmClient.CompleteAsync ──▶ OpenRouter POST /chat/completions (retry 2)   → 503
     │
     ├─ MessageSource snapshot lar (documentId, fileName, page, section, snippet ≤300, score)
     └─ session.AddAssistantMessage + SaveChangesAsync ──▶ INSERT chat_messages, message_source
 ◀─ 200 { sessionId, messageId, answer, sources: [ { documentId, fileName, pageNumber, sectionTitle, snippet, score } ] }
```

Sessiya alohida yaratilmaydi: `sessionId` siz birinchi savol yangi sessiya ochadi, keyingilar shu `sessionId` bilan yuboriladi.
Javobdagi `[1]`, `[2]` — `sources` massivi tartibi.

### 6.12 Chat sessiyalari

```
GET    /api/chat/sessions                 [X-User-Id]
 └─ GetMySessionsHandler      ──▶ SELECT chat_sessions WHERE user_id ORDER BY updated_at DESC
 ◀─ 200 [ { id, title, createdAt, updatedAt } ]

GET    /api/chat/sessions/{id}/messages   [X-User-Id]
 └─ GetSessionMessagesHandler ──▶ sessiya user niki? (→ 404) ──▶ chat_messages + message_source
 ◀─ 200 [ { id, role, content, createdAt, sources } ]

DELETE /api/chat/sessions/{id}            [X-User-Id]
 └─ DeleteSessionHandler      ──▶ DELETE chat_sessions WHERE id AND user_id (cascade)      0 qator → 404
 ◀─ 204
```

### 6.13 Texnik

| Endpoint | Nima qiladi |
|---|---|
| `GET /health` | `postgres`, `minio` (bucket bor), `ollama` (ishlaydi + embedding modeli yuklangan) |
| `GET /health/live` | Faqat jarayon tirikligi (Docker healthcheck) |
| `GET /swagger` | Swagger UI (`Swagger:Enabled`) |

---

## 7. Ingestion — fon jarayoni

```
IIngestionQueue.EnqueueAsync(versionId)
 └─ ChannelIngestionQueue
     └─ IngestionWorker (ketma-ket, har versiya — yangi DI scope)
         └─ IngestionPipeline.ProcessAsync(versionId)
             ├─ LoadAsync: hujjat o'chirilgan yoki versiya Pending/Processing emas → o'tkazib yuboriladi
             ├─ version.StartProcessing()   → Processing, attempts++     SaveChanges
             │
             ├─ IFileStorage.OpenReadAsync ──────▶ MinIO
             ├─ IPdfTextExtractor.Extract ───────▶ PdfPigTextExtractor
             │     matn yo'q → IngestionException("skaner PDF, OCR kerak") → darhol Failed
             ├─ IChunker.Chunk ──────────────────▶ StructureAwareChunker (§4.6)
             │     chunk yo'q → IngestionException → Failed
             ├─ IEmbeddingService.EmbedAsync ────▶ Ollama /api/embed (16 tadan)
             │
             └─ SaveAsync
                 ├─ qayta o'qish: hujjat o'chirilgan / yangiroq versiya faol → MarkFailed
                 ├─ TRANZAKSIYA
                 │   ├─ INSERT document_chunks (content, embedding, section_title, page, type, model)
                 │   ├─ document.Activate(v)        → documents.current_version = v
                 │   ├─ version.MarkReady(pages, chunks)
                 │   ├─ DELETE document_chunks WHERE document_version < v
                 │   └─ COMMIT
                 └─ eski versiya MinIO fayllari o'chiriladi (storage_key boshqa bo'lsa)

 Xato (HandleFailureAsync):
   IngestionException yoki attempts ≥ MaxRetries (3) → MarkFailed(xabar)
   aks holda (Ollama/MinIO vaqtincha xato)           → ScheduleRetry (Pending) + EnqueueDelayed(30s × attempt)
 Server to'xtasa: versiya Processing da qoladi → keyingi startda qayta navbatga.
```

Tranzaksiya tufayli qidiruv hech qachon yarim tayyor yoki ikki versiya aralash holatni ko'rmaydi.

Holatlar: `Pending → Processing → Ready | Failed`; `Failed → (retry endpoint) → Pending`.

---

## 8. Ma'lumotlar bazasi

PostgreSQL 16 + pgvector, nomlar snake_case.

```
documents ──1:N── document_versions
    └──1:N── document_chunks          (embedding vector(1024), search_vector tsvector)

chat_sessions ──1:N── chat_messages ──1:N── message_source   (chunk ga FK yo'q — snapshot)
```

| Jadval | Ustunlar | Izoh |
|---|---|---|
| `documents` | `id`, `title`, `description`, `current_version`, `created_at`, `updated_at`, `deleted_at` | `current_version` — qidiruvdagi faol versiya (0 — hali yo'q); soft delete |
| `document_versions` | `document_id`, `version`, `file_name`, `storage_key`, `content_hash`, `file_size`, `page_count`, `chunk_count`, `status`, `attempts`, `error_message`, `created_at`, `indexed_at` | Har yuklangan fayl — yangi qator; indeks `(document_id, version)`, `status` |
| `document_chunks` | `document_id`, `document_version`, `chunk_index`, `page_number`, `section_title`, `chunk_type`, `content`, `char_count`, `embedding`, `embedding_model`, `search_vector` | HNSW cosine (`embedding`), GIN (`search_vector`), `(document_id, document_version)` |
| `chat_sessions` | `user_id`, `title`, `created_at`, `updated_at` | indeks `(user_id, updated_at)` |
| `chat_messages` | `session_id`, `role`, `content`, `llm_model`, `latency_ms`, `created_at` | indeks `(session_id, created_at)` |
| `message_source` | `message_id`, `document_id`, `file_name`, `page_number`, `section_title`, `snippet`, `score` | Javob manbalari snapshot i |

`search_vector` — `to_tsvector('simple', section_title || ' ' || content)`, Postgres o'zi hisoblaydi (stored).

---

## 9. Domen modeli

| Entity | Metodlar | Qoidalar |
|---|---|---|
| `Document` | `SetInfo`, `AddVersion`, `Activate(v)`, `Delete`; `LatestVersion`, `ActiveVersion`, `NextVersionNumber` | Nom bo'sh emas; o'chirilgan hujjatga versiya qo'shib/faollashtirib bo'lmaydi; `Activate` faqat joriydan katta versiyaga |
| `DocumentVersion` | `StartProcessing`, `MarkReady`, `ScheduleRetry`, `MarkFailed`, `Retry` | `StartProcessing` faqat Pending/Processing dan; `Ready` faqat Processing dan; `Retry` faqat Failed dan |
| `DocumentChunk` | konstruktor | `char_count` avtomatik |
| `ChatSession` | `AddUserMessage`, `AddAssistantMessage`, `BelongsTo` | `user_id` bo'sh emas; `title` ≤ 100 belgi |
| `ChatMessage` | `FromUser`, `FromAssistant` (internal) | Bo'sh xabar mumkin emas |
| `MessageSource` | konstruktor | Snapshot |

Qoida buzilsa `DomainException` → 400.

---

## 10. Xatolar va HTTP kodlar

`Rag.Api/Middleware/ExceptionHandler.cs` — barcha exception lar `ProblemDetails` ga aylanadi. Exception klasslari: `Rag.Application/Common/Exceptions/AppExceptions.cs`.

| Exception / holat | HTTP | Qayerda chiqadi |
|---|---|---|
| `ValidationException`, `DomainException` | 400 | `PdfFile`, `AskQuestionHandler`, entity lar |
| `UnauthorizedException` | 401 | `RequireUserId()` — `X-User-Id` yo'q |
| `X-Api-Key` noto'g'ri | 401 | `ApiKeyEndpointFilter` |
| `NotFoundException` | 404 | Hujjat/sessiya/fayl topilmadi |
| `ConflictException` | 409 | Dublikat fayl, retry noto'g'ri holatda |
| Rate limit | 429 | `POST /api/chat` |
| `ServiceUnavailableException` | 503 | `OpenAiCompatibleLlmClient` |
| `HttpRequestException` | 503 | Ollama / LLM ulanish xatosi |
| Boshqa | 500 | logga yoziladi |

---

## 11. Konfiguratsiya

`appsettings.json` (lokal `dotnet run`), `appsettings.Development.json` (lokal sirlar, git da yo'q),
Docker da `.env` → `docker-compose.yml` → environment (`Bo'lim__Kalit`).

| Bo'lim | Kalit | Standart | Klass |
|---|---|---|---|
| `Rag:Chunking` | `TextSize`, `Overlap`, `MinLength`, `JsonMaxSize` | 1000, 150, 50, 3000 | `RagOptions` |
| `Rag:Retrieval` | `TopK`, `MinScore`, `HistoryMessages`, `Hybrid` | 8, 0.35, 6, false | `RagOptions` |
| `Rag:Ingestion` | `EmbeddingBatchSize`, `MaxRetries`, `MaxFileSizeMb` | 16, 3, 50 | `RagOptions` |
| `Embedding` | `BaseUrl`, `Model`, `Dimensions`, `TimeoutSeconds` | `http://localhost:11434`, `bge-m3`, 1024, 300 | `EmbeddingOptions` |
| `Llm` | `BaseUrl`, `ApiKey`, `Model`, `Temperature`, `MaxRetries`, `TimeoutSeconds` | `https://openrouter.ai/api/v1`, —, `openrouter/free`, 0.2, 2, 120 | `LlmOptions` |
| `Minio` | `Endpoint`, `AccessKey`, `SecretKey`, `Bucket`, `UseSsl` | `localhost:9000`, ..., `documents`, false | `MinioOptions` |
| `Security` | `AdminApiKey`, `ChatRequestsPerMinute` | bo'sh, 20 | `SecurityOptions` |
| `Database` | `MigrateOnStartup` | true | `Program.cs` |
| `Swagger` | `Enabled` | true | `Program.cs` |
| `ConnectionStrings` | `Db` | `Host=localhost;Port=5433;Database=rag;...` | `AddInfrastructure` |

`.env` → environment:

| `.env` | Environment |
|---|---|
| `POSTGRES_*` | `ConnectionStrings__Db` |
| `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `MINIO_BUCKET` | `Minio__AccessKey`, `Minio__SecretKey`, `Minio__Bucket` |
| `EMBEDDING_BASE_URL`, `EMBEDDING_MODEL`, `EMBEDDING_DIMENSIONS` | `Embedding__*` |
| `LLM_BASE_URL`, `LLM_API_KEY`, `LLM_MODEL`, `LLM_TEMPERATURE` | `Llm__*` |
| `ADMIN_API_KEY`, `CHAT_REQUESTS_PER_MINUTE` | `Security__*` |
| `RAG_HYBRID_SEARCH` | `Rag__Retrieval__Hybrid` |

---

## 12. DI ro'yxati — nima qayerda ro'yxatdan o'tadi

| Joy | Ro'yxatdan o'tadi | Lifetime |
|---|---|---|
| `Rag.Application/DependencyInjection.cs` → `AddApplication` | `RagOptions`; 14 ta handler; `IngestionPipeline` | Scoped |
| `Rag.Infrastructure/DependencyInjection.cs` → `AddInfrastructure` | `EmbeddingOptions`, `LlmOptions`, `MinioOptions` | Options |
| | `AppDbContext` (+ `IAppDbContext`), `IVectorStore → PgVectorStore` | Scoped |
| | `IMinioClient`, `IPdfTextExtractor`, `IChunker` | Singleton |
| | `IFileStorage → MinioFileStorage` | Scoped |
| | `IEmbeddingService`, `OllamaHealthCheck`, `ILlmClient` — typed `HttpClient` | Transient (`AddHttpClient`) |
| | `ChannelIngestionQueue` (+ `IIngestionQueue`) | Singleton |
| | `IngestionWorker` | HostedService |
| | Health check lar: `postgres`, `minio`, `ollama` | |
| `Rag.Api/Program.cs` | `SecurityOptions`, `ICurrentUser → HttpCurrentUser`, `ExceptionHandler`, Swagger, RateLimiter, JSON enum → string | Scoped / Singleton |

---

## 13. Docker

| Servis | Image | Port (host) | Vazifa |
|---|---|---|---|
| `postgres` | `pgvector/pgvector:pg16` | 5433 | DB, volume `pgdata` |
| `minio` | `pgsty/minio` (community fork) | 9000 / 9001 | PDF fayllar, volume `miniodata` |
| `minio-init` | `pgsty/mc` | — | Bucket yaratadi va to'xtaydi |
| `ollama` | `ollama/ollama` | 11434 | Profil `ollama` (ixtiyoriy, GPU) |
| `api` | `rag-api` (`src/Rag.Api/Dockerfile`) | 8080 | Rag.Api |

`docker-compose.override.yml` — `api` ni oldindan mavjud Ollama konteyneri network iga ulaydi (`.env`: `OLLAMA_NETWORK`,
`EMBEDDING_BASE_URL=http://ollama:11434`). Tartib: `postgres`, `minio` healthy → `minio-init` → `api`.

---

## 14. Yangi funksiya qo'shish

Yangi endpoint (masalan, hujjat chunk larini ko'rish):

1. **Application:** `Documents/Queries/GetDocumentChunks.cs` — `GetDocumentChunksHandler(IAppDbContext db)` + `HandleAsync`, DTO.
2. **Application:** `DependencyInjection.cs` → `services.AddScoped<GetDocumentChunksHandler>();`
3. **Api:** `Endpoints/DocumentsEndpoints.cs` → `group.MapGet("/{id:guid}/chunks", ...)`; o'zgartiruvchi bo'lsa `admin` guruhiga.
4. Xatolar uchun `AppExceptions` dagi mavjud exception larni ishlating — `ExceptionHandler` ularni HTTP kodga aylantiradi.

Yangi tashqi integratsiya: interfeys `Application/Abstractions/` da → implementatsiya `Infrastructure/<Papka>/` da →
`AddInfrastructure` da ro'yxat → options klassi `Infrastructure/Options/` da va `appsettings.json` / `.env` / compose ga kalitlar.

Entity yoki sxema o'zgarsa: `dotnet ef migrations add <Nom> -p src/Rag.Infrastructure -s src/Rag.Api -o Persistence/Migrations`.

---

## 15. Qilinmagan

Streaming (SSE), query rewriting (follow-up savollar), reranker, qo'shni chunk lar, eval to'plami,
yetim MinIO fayllarni tozalovchi job, testlar (`tests/`), OCR.

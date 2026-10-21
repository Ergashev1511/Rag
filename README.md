# Dashboard AI Assistant (RAG)

.NET 8, Clean Architecture (Api, Application, Infrastructure, Domain), PostgreSQL + pgvector, MinIO, Ollama (bge-m3), OpenAI-compatible LLM.

Arxitektura va har endpoint oqimi: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Servislar

| Servis | Image | Port (host) | Vazifa |
|---|---|---|---|
| `postgres` | `pgvector/pgvector:pg16` | 5433 | Asosiy DB + vector + full-text |
| `minio` | `pgsty/minio` (community fork) | 9000 (API), 9001 (Console) | Original PDF lar |
| `minio-init` | `pgsty/mc` | — | Bucket ni bir marta yaratadi va to'xtaydi |
| `ollama` | `ollama/ollama` | 11434 | Embedding (bge-m3), keyin lokal Qwen. `ollama` profili, GPU |
| `api` | `rag-api` (o'zingiz build qilasiz) | 8080 | Rag.Api |

Hammasi `rag` network ida, bir-biriga servis nomi bilan ulanadi (`postgres:5432`, `minio:9000`, `ollama:11434`).
LLM (OpenRouter) tashqi servis, compose da yo'q.

Ishga tushish tartibi: `postgres` (healthy) va `minio` (healthy) → `minio-init` (tugadi) → `ollama` (healthy, profil yoqilgan bo'lsa) → `api`.
API ishga tushganda migratsiyalarni o'zi qo'llaydi (`DB_MIGRATE_ON_STARTUP=true`). API holati: `GET /health`.

## Sozlash

```bash
cp .env.example .env
# .env: parollar, LLM_API_KEY, LLM_MODEL ni to'ldiring
```

Barcha sirlar `.env` da saqlanadi, `.env` git ga qo'shilmaydi. Compose ularni `ConnectionStrings__Db`, `Minio__*`, `Embedding__*`, `Llm__*` environment o'zgaruvchilari orqali API ga beradi.

### Ollama: Docker ichida yoki hostda

- **Docker ichida (GPU):** `.env` da `COMPOSE_PROFILES=ollama`, `EMBEDDING_BASE_URL=http://ollama:11434`.
  Buning uchun hostda NVIDIA Container Toolkit o'rnatilgan bo'lishi kerak.
  Modellar `OLLAMA_MODELS_DIR` da saqlanadi: nomli volume yoki hostdagi papka, masalan `/data/ollama`.
- **Hostda:** `COMPOSE_PROFILES=` (bo'sh), `EMBEDDING_BASE_URL=http://host.docker.internal:11434`.
  Host dagi Ollama `OLLAMA_HOST=0.0.0.0` bilan ishlashi kerak, aks holda konteynerdan ko'rinmaydi.

### LLM

| Rejim | `LLM_BASE_URL` | `LLM_MODEL` | `LLM_API_KEY` |
|---|---|---|---|
| OpenRouter | `https://openrouter.ai/api/v1` | `<model-id>:free` | OpenRouter kaliti |
| Lokal Qwen (Ollama) | `http://ollama:11434/v1` | masalan `qwen2.5:7b` | `ollama` (istalgan qiymat) |

## Ishga tushirish

```bash
# 1. API image ni build qilish
docker compose build api
#    yoki o'zingizning image ingiz: .env da API_IMAGE=registry/rag-api:tag

# 2. Servislarni ishga tushirish
docker compose up -d

# 3. Modellarni yuklash (Ollama Docker ichida bo'lsa)
docker compose exec ollama ollama pull bge-m3
docker compose exec ollama ollama pull qwen2.5:7b   # lokal LLM kerak bo'lsa

# 4. Tekshirish
docker compose ps
curl http://localhost:8080/health
```

MinIO Console: http://localhost:9001 (login: `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`).

Faqat infratuzilma (API ni IDE dan ishga tushirish uchun):

```bash
docker compose up -d postgres minio minio-init
dotnet run --project src/Rag.Api
```

`src/Rag.Api/appsettings.json` dagi lokal qiymatlar `.env.example` dagi standart qiymatlarga mos. O'zgartirsangiz `dotnet user-secrets` dan foydalaning.

## Foydali buyruqlar

```bash
docker compose logs -f api                  # API loglari
docker compose down                         # to'xtatish (ma'lumotlar saqlanadi)
docker compose down -v                      # to'xtatish va volume larni o'chirish (DB, fayllar, modellar)
dotnet ef migrations add <Name> -p src/Rag.Infrastructure -s src/Rag.Api -o Persistence/Migrations
```

## API

Swagger UI: http://localhost:8080/swagger (`SWAGGER_ENABLED=true`). Yuqoridagi **Authorize** tugmasi orqali `X-Api-Key` va `X-User-Id` kiritiladi.

Hujjat boshqaruvi endpointlari `X-Api-Key` header ini talab qiladi (`ADMIN_API_KEY`; bo'sh bo'lsa tekshirilmaydi).
Chat endpointlari `X-User-Id` header ini talab qiladi (dashboard backend yuboradi). Xatolar `ProblemDetails` formatida.

| Metod | URL | Vazifa | Header |
|---|---|---|---|
| POST | `/api/documents` | PDF yuklash (multipart: `file`, `title`, `description?`) → 202 | X-Api-Key |
| GET | `/api/documents` | Ro'yxat | |
| GET | `/api/documents/{id}` | Hujjat va versiyalari | |
| GET | `/api/documents/{id}/status` | Indekslash holati | |
| GET | `/api/documents/{id}/download` | Original PDF | |
| PUT | `/api/documents/{id}` | `{title, description}` tahrirlash | X-Api-Key |
| PUT | `/api/documents/{id}/file` | PDF ni almashtirish (multipart: `file`) → 202 / 200 (o'zgarmagan) | X-Api-Key |
| POST | `/api/documents/{id}/retry` | Failed versiyani qayta indekslash | X-Api-Key |
| DELETE | `/api/documents/{id}` | O'chirish → 204 | X-Api-Key |
| POST | `/api/admin/reindex` | Hammasini qayta indekslash | X-Api-Key |
| POST | `/api/chat` | `{sessionId?, question}` → `{sessionId, messageId, answer, sources}` | X-User-Id |
| GET | `/api/chat/sessions` | Mening sessiyalarim | X-User-Id |
| GET | `/api/chat/sessions/{id}/messages` | Xabarlar va manbalar | X-User-Id |
| DELETE | `/api/chat/sessions/{id}` | Sessiyani o'chirish | X-User-Id |
| GET | `/health` | DB, MinIO, Ollama holati | |
| GET | `/health/live` | Jarayon tirikligi (Docker healthcheck) | |

Misol:

```bash
curl -H "X-Api-Key: $ADMIN_API_KEY" -F file=@dashboard.pdf -F title="Dashboard qo'llanmasi" http://localhost:8080/api/documents
curl http://localhost:8080/api/documents/<id>/status
curl -H "X-User-Id: 42" -H "Content-Type: application/json" \
     -d '{"question":"Buyurtmani qanday bekor qilaman?"}' http://localhost:8080/api/chat
```

Hybrid qidiruv (vector + kalit so'z, RRF): `Rag:Retrieval:Hybrid=true` (`appsettings.json` yoki `Rag__Retrieval__Hybrid=true`).

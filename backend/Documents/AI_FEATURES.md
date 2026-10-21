# AI features: setup, workflow and limits

## Implemented features

| Feature | Where to use it | Data and permissions |
| --- | --- | --- |
| Application help / RAG | Application Help | All roles; approved user-guide sections only |
| Semantic retrieval | Admin indexes in AI Workspace → Knowledge index; staff ask in Application Help | Ollama embeddings + Qdrant; no business records indexed |
| Purchase invoice extraction | Purchasing → AI supplier invoice extraction | Admin/Pharmacist; copied text or PNG/JPEG image; explicit review |
| Inventory questions | AI Workspace → Inventory questions | All roles; fixed branch-filtered stock queries; no costs exposed |
| Sales insights | AI Workspace → Sales insights | Admin/Pharmacist; aggregate branch sales; original facts shown |
| Reorder planner | AI Workspace → Reorder planner | Admin/Pharmacist; deterministic demand calculation, not a trained forecast |
| Feedback sentiment | AI Workspace → Feedback sentiment | Admin/Pharmacist; analyze anonymized feedback, not stored by this app |

No module autonomously purchases goods, receives stock, changes an invoice, moves money or gives clinical advice. Existing business endpoints remain responsible for all operational validation and posting. No database migration is needed for these features.

## What works immediately on this development machine

`appsettings.Development.json` enables the local Ollama provider at `http://localhost:11434/` with `llama3.2:3b`, which was found installed. Start Ollama and restart the API/frontend. This development setting is not enabled in production settings.

- Application Help retrieves guide sections and can generate an answer using the configured local model. Configured Langflow generation is tried first; Ollama is the next option, followed by guide-only fallback.
- Inventory questions work even if generation is unavailable: the response identifies whether interpretation was AI-based or rule-based.
- Sales insights always return verified report figures when available. Model failure returns figures without a generated explanation.
- Reorder planning uses existing SQL data and does not require an AI provider.
- Text invoice extraction and sentiment analysis require the model; failures are explicit and do not produce invented fallback data.
- Vision extraction needs a separately installed compatible vision model. Vector search needs an embedding model, running Qdrant and a completed index. Neither is configured automatically.

Model inference uses local hardware resources. Model licenses, hardware needs and any remote hosting charges are separate from application code.

## Configure model services

Server settings use .NET environment-variable notation below. Do not put service credentials into frontend variables or source control.

```text
Ai__Enabled=true
Ai__OllamaUrl=http://localhost:11434/
Ai__Model=llama3.2:3b
Ai__VisionModel=<installed-vision-capable-model-name>
Ai__EmbeddingModel=nomic-embed-text
Ai__EnableVectorSearch=true
Ai__QdrantUrl=http://localhost:6333/
Ai__QdrantApiKey=<key-if-required-by-your-Qdrant-deployment>
```

Only configure a vision model after installing and testing a model that accepts Ollama image inputs. Ordinary text models cannot read images. Leave the vision setting blank until ready.

Install the embedding model separately when you are ready to enable vector retrieval:

```powershell
ollama pull nomic-embed-text
```

For a local development Qdrant instance, start Docker Desktop, then run from the repository root:

```powershell
docker compose -f backend/operations/compose.ai.yml up -d
```

The supplied compose file binds Qdrant to loopback and uses a named persistent volume. It is a local development configuration, not an internet-facing deployment. For a remote service, use HTTPS and its configured API key. The application rejects remote plain HTTP URLs, URL-embedded credentials and redirects.

Restart the API after changing settings. AI Workspace's configuration flags indicate configured values, not successful connectivity checks. Requests report missing services/models. Keep service endpoints private and review retention and access controls before deploying to customers.

The local container stack explicitly sets `Ai__AllowInsecureHttp=true` to reach private Docker service names. This defaults to false outside that stack. Keep it false for remote services using HTTPS. See [Docker setup](../../ApplicationHelp/DOCKER.md) for container networking and startup.

### Build the RAG index

1. Enable the model/embedding/vector settings and start the services.
2. Sign in as Admin and open **AI Workspace → Knowledge index**.
3. Select **Index current user guide** and wait for success.
4. Open **Application Help**, ask a workflow question and confirm the response says **Semantic vector search**.
5. Expand the guide sources and check the generated answer against them.

RAG pipeline:

```text
Packaged USER_GUIDE.md → numbered sections → 1,400-character chunks
  → Ollama embeddings → Qdrant cosine-vector collection

Question → query embedding → Qdrant matches → trusted packaged source IDs
  → guide sections + question → Langflow or Ollama → answer + source sections
```

Only the server-owned guide is indexed. Qdrant payloads contain source IDs; retrieved content is resolved from the packaged guide, so a forged vector payload cannot supply arbitrary prompt text. The shared guide is not branch-specific and documents all roles without granting permissions.

Collection names include a digest of the guide and embedding model name. Changed guides/models use a new collection, so old versions are not silently searched. Rebuild the API after guide edits, then index again. Use a new model name when changing embedding weights; changing weights behind the same tag is not automatically detected. Retired collections are retained; an operator can remove them after verifying the active version. Keyword search is used when vector search fails or returns no matches. The UI shows the actual retrieval mode.

### Optional Langflow orchestration

The existing Langflow adapter is retained. Configure a stateless **Chat Input → language model → Chat Output** flow with no operational tools or memory retrieval, and set:

```text
HelpAssistant__EnableLangflow=true
HelpAssistant__BaseUrl=http://localhost:7860/
HelpAssistant__FlowId=<flow-UUID>
HelpAssistant__ApiKey=<server-side-key>
```

Langflow receives the selected guide sections and question for answer generation. Embedding and Qdrant retrieval currently run in the .NET adapter, not inside Langflow. Langflow is optional; direct Ollama generation also works. Each generation request is independent, without application conversation memory.

## Staff workflows

### Extract a supplier invoice

1. Open Purchasing and paste invoice text or select a PNG/JPEG page image up to 4 MB. For PDF, copy its text or export a page image; direct PDF parsing is not implemented.
2. Click **Extract for review**. The document is sent to the configured model service.
3. Check the original document against every extracted value. Missing values remain blank. Month-only expiry, unclear quantity units and absent selling prices need explicit correction.
4. Choose the exact medicine for every line. Only unique, exact active-catalog name matches are suggested; even these require review of strength/form/manufacturer and units.
5. Correct invoice number/date, batch, expiry, quantity and per-unit prices. No automatic GST, pack conversion, supplier creation or medicine creation occurs.
6. Confirm that every field was reviewed and select **Use reviewed values in purchase form**. This replaces only the unsaved form after confirmation.
7. Select the supplier, review the existing purchase form, and create the draft through the normal workflow.
8. Inspect the physical delivery and use **Receive stock** separately.

Maximum extraction is 100 lines and 20,000 text characters. Invalid structures, negative prices/quantities and other out-of-range output are rejected. Model output is not treated as verified accounting data.

### Ask about inventory

Enter a stock, expiry or low-stock question, then set the explicit medicine/batch filter and expiry horizon. The question classifies the query type only. Medicine names and dates mentioned solely in the question do not become filters. The screen and response make this limitation explicit.

The model can choose only `stock`, `expiry` or `low-stock`; it cannot generate SQL, change the branch or call arbitrary tools. All results are obtained through the existing branch-filtered DbContext. Results contain at most 100 batches; refine the filter when necessary. Low stock follows the branch's batch threshold. Expiry results include expired batches and are not a list of saleable goods.

### Review reorder suggestions

Choose 7–365 history days and 1–180 target coverage days. Calculations use inventory units:

```text
net sold = units on non-cancelled invoices in the history cohort
           minus returns against those sale lines
daily demand = max(0, net sold) / history days
target = ceiling(daily demand × coverage days)
usable stock = saleable stock − stock expiring within coverage
suggested = max(0, target − usable stock − eligible draft purchase quantities)
```

Draft purchase quantities are included only when their expiry is beyond the coverage horizon. They are expected supply, not available stock. Check whether those drafts will actually arrive. Calculations do not learn seasonality, supplier delivery dates or pack sizes; zero sales history produces zero demand. Review supplier lead times, unusual demand and physical stock before creating any order. Nothing is purchased automatically.

### Review sales insights

Select 1–90 days. The backend supplies existing date-correct report aggregates, daily activity and up to 20 top medicines. Customer identities and invoice/payment references are not sent to the model. The explanation is displayed beside verified totals so staff can compare it. It is not a causal analysis, profit calculation or tax return.

### Analyze feedback sentiment

Paste up to 4,000 characters of anonymized service feedback. The model returns positive/neutral/negative/mixed, a summary and service themes. No customer feedback records or chat history are persisted by this application. The provider may retain requests, so configure it appropriately. This feature does not provide trend history or automatic customer surveys and must not be used to infer medical conditions or decide employee performance.

## API and architecture

All `/api/ai` endpoints require authentication and use the existing password-change, security-stamp and branch validation. They share the help-assistant limit of 10 requests per account per minute.

| Endpoint | Roles |
| --- | --- |
| GET `/api/ai/status` | All application roles |
| POST `/api/ai/inventory` | All application roles |
| GET `/api/ai/reorders?historyDays=30&horizonDays=30` | Admin/Pharmacist |
| GET `/api/ai/sales-insights?days=30` | Admin/Pharmacist |
| POST `/api/ai/sentiment` with `{ "text": "…" }` | Admin/Pharmacist |
| POST `/api/ai/extract-invoice` with `{ "text": "…", "imageBase64": null }` | Admin/Pharmacist |
| POST `/api/ai/knowledge/index` | Admin |
| POST `/api/help-assistant/ask` with `{ "question": "…" }` | All application roles |

Application contracts and validation are in `Pharmacy.Application/Assistant`. Infrastructure holds Ollama, Qdrant and branch-data adapters. Controllers enforce role access; React shows only the authorized tools. API keys remain server-side; model output is rendered as text, not executable HTML. Model services have no financial or inventory write tools. API requests are bounded and cancellable; provider response sizes and deadlines are bounded as well.

## Verification and remaining deployment work

```powershell
dotnet test backend/Pharmacy.Tests/Pharmacy.Tests.csproj -c ReturnValidation --filter 'FullyQualifiedName~HelpAssistantTests|FullyQualifiedName~AiFeatureTests'
.\scripts\test-ai.ps1
```

The first command uses stubbed providers and checks retrieval, extraction validation, model contracts, sentiment labels, planning arithmetic and failure paths. The second performs a real synthetic-feedback request to local Ollama; it does not establish accuracy across real documents or feedback. `AiBusinessIntegrationTests` additionally requires the disposable SQL Server test connection described in SQL_TESTING.md.

Before customer rollout, test a running Qdrant index/search and an installed vision model with representative anonymized documents. Evaluate extraction, unsupported questions, prompt injection attempts, semantic relevance, sentiment language coverage and incorrect explanations. This implementation is not a certification of production readiness or model accuracy.

Protocol references: [Ollama generation](https://docs.ollama.com/api/generate), [Ollama embeddings](https://docs.ollama.com/api/embed), [Qdrant query API](https://api.qdrant.tech/api-reference/search/query-points), [Langflow run API](https://docs.langflow.org/api-flows-run).

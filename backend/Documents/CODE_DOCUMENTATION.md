# Reading the implementation

The backend's XML comments explain public types, workflow methods and important calculated values across the four Clean Architecture projects. Hover a documented symbol in the IDE or use Go to Definition to read its summary. More detailed methods include parameters, return semantics, exceptions and remarks.

## Where to start

| Layer | Responsibility | Useful starting points |
| --- | --- | --- |
| Domain | Entities, state transitions and monetary/quantity invariants | `Sale`, `SaleItem`, `MedicineBatch`, `SaleSettlement`, `GstCalculator` |
| Application | Use cases, input/output contracts and validation | `BatchService`, repository interfaces, `GstPreparationRules`, `AiFeatureService` |
| Infrastructure | Database queries, transactions, auditing, identity and providers | `SaleRepository`, `SaleReturnRepository`, `PaymentRepository`, `PharmacyDbContext` |
| API | Authentication, role access, HTTP validation and responses | Controllers, `ApiExceptionHandler`, `ExpiryNotificationWorker` |

For AI, follow `HelpAssistant` → `HybridKnowledge` → `SemanticKnowledge`/`GuideKnowledge` → `HelpGeneratorRouter`. Business assistance goes through `AiFeatureService`; `AiBusinessData` supplies fixed branch-filtered reads and `OllamaAiModel` supplies untrusted generation or embeddings.

## Documentation conventions

- Use `<summary>` for purpose and observable behavior, especially what changes or remains a separate operation.
- Use `<param>` for units, bounds, nullable meaning and ownership of explicit inputs.
- Use `<returns>` for missing-record behavior, advisory results, resource ownership and result limits.
- Use `<remarks>` for transaction boundaries, branch scope, time conventions and limitations.
- Use `<exception>` for important validation/provider failures. Do not promise that these are the only possible infrastructure exceptions.
- Keep financial distinctions explicit: invoices, credits, receipts and payouts are separate records. A payment/refund ledger entry does not initiate a transfer.
- Explain deterministic planning separately from model-generated suggestions, and distinguish configured services from successful live execution.
- Update comments whenever behavior changes. Original method bodies remain the authority; comments do not add runtime validation or authorization.

Generated migrations, model snapshots and build outputs are intentionally excluded. These are C# XML comments; React/TypeScript documentation uses JSDoc rather than XML and is outside this documentation pass.

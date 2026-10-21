Customer release readiness
===========================

Docker build recipes and a loopback-only Compose evaluation stack are available in [DOCKER.md](DOCKER.md), including a separate migration job, persistent SQL storage and optional AI services. Compose configuration, migration-runner compilation and the targeted backend tests were validated; Linux image builds and runtime startup still require a running Docker engine. The supplied Developer-edition SQL stack is for local evaluation, not a licensed customer deployment.

For staff onboarding and daily application workflows, see the [step-by-step user guide](USER_GUIDE.md).

AI modules now include optional Qdrant/Ollama semantic RAG, Langflow or Ollama help generation, invoice extraction with staff review, bounded branch-inventory queries, aggregate sales explanations, deterministic reorder planning and feedback sentiment analysis. Development uses the installed local llama3.2:3b model. Vector and vision services still require configuration and live validation; no autonomous operational actions are enabled. See [AI features and deployment limits](backend/Documents/AI_FEATURES.md).

AI integration validation: all 181 backend tests passed against disposable local SQL Express databases, including the new branch-data test. A real local Ollama synthetic-feedback smoke check also passed. Stubbed Qdrant/vision-related contract tests are not evidence of a successful live deployment of those services.

This is an application with a substantial operational feature set, not a
certification of production suitability. The current target is one pharmacy
organization with multiple branches and a shared medicine/supplier catalog.
Separate customer organizations require separate deployments/databases; this is
not a multi-tenant SaaS boundary.

Implemented in this readiness pass
----------------------------------

Sales cancellation was added in a subsequent pass: Admin/Pharmacist invoice
cancellation, original-invoice preservation, inspected opt-in stock reversal,
refund liabilities/payout history and date-correct sales/GST/profit credits.
See backend/SALES_CANCELLATION.md. Validation after this change: all 116 backend
tests passed (including nine cancellation cases), frontend build/lint passed.
The AddSaleCancellation migration is required before starting the updated API.

Direct UPI QR collection was added subsequently, disabled by default per branch.
It creates payment requests only; staff verifies bank receipt and records the UTR
through the existing manual ledger. No automated settlement or gateway is claimed.
See backend/UPI_COLLECTION.md for setup and actual bank/app acceptance requirements.
After this change, all 130 backend tests and frontend build/lint passed. Apply
AddBranchUpiCollection before starting the updated API.

GST return preparation now provides an Admin-only monthly branch workspace,
audited tax annotations, normalized invoice-only GSTR-2B CSV reconciliation,
reviewed snapshots, stale-source detection and Excel/PDF accountant worksheets.
All 142 backend tests passed after this change. AddGstPreparationWorkspace adds
the supporting tables. See backend/GST_RETURN_PREPARATION.md for setup and limits.
This remains branch-level preparation, not GSTIN-wide consolidation, official
Portal file import/export, tax payable calculation or return submission. Unsupported
or incomplete source data is blocked from reviewed totals instead of inferred.

* Branch directory and Admin branch switching. Employees have an assigned branch.
  Reassigning an employee invalidates their session. Admins may operate across
  branches by explicitly switching; pharmacists/cashiers are confined to their
  assigned branch. Every signed token contains the authorized branch ID.
* Branch-scoped stock, purchases/items, sales/items, payment/refund/return ledgers,
  customers, notifications, settings and audit history. EF query filters include
  raw SQL queries, and writes are stamped/guarded by the current branch. Shared
  medicines and suppliers are intentionally organization-wide.
* Branch-aware uniqueness for batch numbers, invoices, settings and payment keys.
  Stock, pricing, invoice sequence and tax/receipt configuration can differ by
  branch. Reports show the active branch; no misleading consolidated total is
  presented as an organization-wide report.
* Existing data migrates to Main branch (ID 1). New branches start with no stock
  or settings. Configure each branch before normal operation. The notification
  worker iterates every active branch using separate scoped contexts.
* Checkout retry keys and payload fingerprints prevent duplicate invoices/stock
  deductions when the frontend retries a lost response. Identical retries return
  the original invoice; changed payloads with the same key conflict. The browser
  stores unresolved requests by account and branch and offers a safe retry.
  API callers should always supply idempotencyKey; the optional field retains
  compatibility with older clients that do not get retry guarantees.
* Consistent India business-date expiry checks across stock receiving, sale and
  return validation. A batch expiring on the current business date is unavailable.
* Configurable production CORS, HTTPS/HSTS, API security/cache headers, generic
  exception responses with trace IDs, sign-in rate limits and trusted proxy support.
* Explicit production Identity bootstrap; new bootstrap administrators must change
  their password. Health endpoints distinguish liveness from SQL/schema readiness.
* Responsive sidebar, active branch selector, role-aware navigation, keyboard focus,
  skip link, mobile menu, updated login/dashboard styling and lighter initial JS via
  lazy loading. UI error recovery and invalid-session sign-out are included.
* Updated .NET 8 package references and test tooling, including explicit patched
  PKCS and JSON dependencies found by the dependency audit. Package scanning must
  still be repeated for every release.

Required before installing at a customer
---------------------------------------

Validation on 2026-10-21: backend build passed; all 103 tests passed, including
26 live SQL integration tests against the configured local SQL Express instance;
frontend production build and lint passed; NuGet scan (including transitive
dependencies) and npm production dependency scan reported no known vulnerable
packages. EF reports no pending model changes. SQL tests required execution outside
the restricted sandbox; native networking worked with Windows Authentication.
Test results: backend/Pharmacy.Tests/TestResults/sql-validation.trx. Browser
workflow checks, customer migration rehearsal and restore validation remain open.

1. Run the complete SQL integration suite against a NON-PRODUCTION instance with
   PHARMACY_TEST_SQLSERVER configured. It creates/drops uniquely named databases.
   Unit tests and SQL translation checks do not replace live concurrency, migration
   and rollback tests. Record evidence of branch isolation for every employee role.
2. Back up the real database and prove restoration on a separate server. Apply all
   migrations on a restored staging copy before production. AddBranchIsolation and
   AddCheckoutRetryProtection are the new migrations for this readiness pass.
   Existing sessions must sign in again because older JWTs have no branch claim.
3. Configure branch profiles, taxes, prices, stock and employees with a pharmacist
   or responsible business owner. Reconcile opening stock and outstanding balances.
4. Run customer acceptance scenarios: purchases/FEFO, expiry boundary, concurrent
   checkout, split payments, lost-response retry, damaged/expired returns, inspection
   approval, partial refunds, invoice printing, Excel/PDF, branch switching and
   employee reassignment. Confirm actual receipt printers and paper sizes.
5. Test on supported browsers and actual mobile/desktop hardware. Build/lint checks
   do not establish visual or workflow acceptance. Establish monitoring, support,
   a release/rollback process, scheduled backups and a recovery-time target.
6. Have the customer validate market-specific dispensing, tax, invoice, privacy and
   record-retention obligations with their appropriate advisers. The internal GST
   reports are not statutory filing or regulatory certification.
7. Install a currently patched .NET 8 hosting bundle/runtime on the deployment
   server. The development machine currently has runtime 8.0.13; updating NuGet
   packages does not update that shared runtime. Plan framework upgrades as part
   of ongoing product maintenance.

Do not use a Down migration as the branch rollback plan: after branches issue
duplicate invoice or batch numbers, the previous global unique indexes cannot be
recreated. Restore the verified pre-deployment backup instead.

Known product scope still requiring a separate implementation/decision
---------------------------------------------------------------------

* Stock transfers with dispatch/receipt/reconciliation between branches are not
  implemented. Do not emulate transfers with unrelated adjustments or receipts.
* Head-office consolidated reporting is not implemented; reports are branch-local.
* Offline sales synchronization and shared-stock merging are not implemented.
* Supplier/customer credit settlement beyond the existing sale payment ledger,
  prescription/controlled-medicine workflows, e-invoicing and tax filing integrations
  need explicit requirements before being advertised.
* Server-side pagination/search for large datasets and load/soak testing require
  realistic customer data volumes. Current reports have date/export limits, but
  list screens should not be sold as validated for unlimited data.
* Automated offsite backup scheduling, restore verification, MFA/recovery procedures,
  independent security review and UI end-to-end tests are not claimed by this pass.

Production configuration
------------------------

Set secrets through the deployment secret store/environment, not committed files:
ConnectionStrings__PharmacyDb, Jwt__Key (at least 32 bytes), Jwt__Issuer and
Jwt__Audience. Configure Cors__AllowedOrigins__0 with the real HTTPS frontend
origin when serving it separately; same-origin hosting requires no CORS grant.
Configure ReverseProxy__KnownProxies__0 etc. only for your actual trusted proxy IPs.
Forwarded scheme/client IP is used only from trusted proxies. Serve the React build
over HTTPS and apply suitable static-host security headers and SPA route fallback.

After migrating, set BootstrapIdentity__Enabled=true with SeedAdmin__Email,
SeedAdmin__Password and SeedAdmin__FullName for the initial controlled bootstrap.
Remove the bootstrap password/disable the switch after setup. There are no
hard-coded default account credentials. Do not publicly expose Swagger in production.
Configure PHARMACY_REPORT_FONT if a supported TrueType font is not installed.

The login limiter is per application instance and observed client IP. Multi-node
deployments also need gateway/shared rate limiting. Access tokens remain in memory;
refreshing the browser signs the user out. Pending transaction requests persist in
session storage for safe retry after reauthentication in the same branch.

GET /api/health is a liveness probe. GET /api/health/ready requires SQL connectivity
and no pending EF migrations; neither endpoint exposes credentials or records.

See backend/PHARMACY_SETTINGS.md and backend/ADVANCED_REPORTS.md for module-specific
deployment requirements. Inspect NuGet/npm advisory results at each release;
passing an advisory scan does not prove that application authorization is correct.

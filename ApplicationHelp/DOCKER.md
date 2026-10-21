# Run Pharmacy Management with Docker

The repository includes multi-stage images for the ASP.NET Core API, a one-shot migration runner, and the React frontend served by unprivileged Nginx. The root Compose stack creates a separate SQL Server database in a persistent named volume. It does not use or migrate your Windows SQL Express database.

## Start locally on Windows

1. Start Docker Desktop in **Linux containers** mode. The supplied SQL Server service targets `linux/amd64`; allow sufficient Docker memory for SQL Server and any optional local models.
2. Open PowerShell in the repository root and generate unique local credentials:

   ```powershell
   .\scripts\init-docker.ps1 -AdminEmail 'admin@yourpharmacy.example'
   ```

   This creates the ignored `.env` file without overwriting an existing one. Read `PHARMACY_ADMIN_PASSWORD` privately from that file. The script does not print passwords. If `.env` already exists, retain it and configure the required fields manually using `.env.example` as a reference.

3. Build and start the stack:

   ```powershell
   docker compose up --build -d
   docker compose ps -a
   ```

4. Wait for `db` and `api` to become healthy. `migrate` should show **Exited (0)**: this is its expected successful state, not a crashed long-running service. The frontend starts after API readiness succeeds.
5. Open **http://localhost:8080**. Sign in using the configured Admin email and the temporary password in `.env`, then complete the mandatory password change.
6. Configure branch Pharmacy Settings, employees, medicines and opening stock/purchases as described in [the user guide](USER_GUIDE.md). This is an independent database, so existing Windows-host invoices and accounts do not appear automatically.
7. After initial onboarding, set `PHARMACY_BOOTSTRAP_IDENTITY=false` in `.env`, remove the temporary Admin password value, and recreate the API:

   ```powershell
   docker compose up -d --no-deps api
   ```

   The existing account remains in SQL Server; the seed password does not replace a password already changed by the user.

## Services and startup order

```text
SQL Server healthy → migrations complete → API healthy → React/Nginx
Browser → localhost:8080 → Nginx /api proxy → private API:8080 → SQL Server
```

- `web`: builds with Node 22 and serves only compiled assets; React Router deep links fall back to `index.html`.
- `api`: .NET 8 runtime, non-root application user, readiness check and a Linux TrueType font for PDF export.
- `migrate`: applies pending existing EF migrations and exits. It requires no JWT or bootstrap account configuration.
- `db`: SQL Server 2022 Developer with a persistent named volume; no host SQL port is published.
- Optional `ollama` and `qdrant`: start only with the `ai` profile and keep their data in separate named volumes.

Only the frontend is bound to host loopback by default. All browser API requests are same-origin, so `VITE_API_BASE_URL` is deliberately empty during the image build. Do not set it to `http://api:8080`: that Docker hostname is not resolvable by the browser.

Development settings, `.env`, credentials, dependency folders and build artifacts are excluded from Docker build contexts. The packaged guide is explicitly copied from `ApplicationHelp/USER_GUIDE.md` so help retrieval remains available in the published image.

## Useful commands

```powershell
docker compose logs --tail=100 api
docker compose logs --tail=100 migrate
docker compose logs --tail=100 db
docker compose logs --tail=100 web
docker compose down
```

`docker compose down` preserves named database/model/vector volumes. Do not add `--volumes` unless you intend to permanently delete their stored data. Keep `.env` backed up securely; replacing its password does not automatically change credentials inside an existing SQL volume.

For code updates to this evaluation stack, stop the services first, rebuild and rerun the migration job, then start the application:

```powershell
docker compose down
docker compose build
docker compose up -d db
docker compose run --rm migrate
docker compose up -d
```

The explicit migration command must succeed before continuing. Take a database backup before schema upgrades. Do not run migrations concurrently against one database.

## Optional AI containers

1. Start the optional services:

   ```powershell
   docker compose --profile ai up -d ollama qdrant
   docker compose exec ollama ollama pull llama3.2:3b
   docker compose exec ollama ollama pull nomic-embed-text
   ```

   Model downloads consume disk space and inference uses local CPU/RAM. A suitable vision model is a separate installation; a text model cannot read images.

2. Edit `.env`:

   ```text
   PHARMACY_AI_ENABLED=true
   PHARMACY_OLLAMA_URL=http://ollama:11434/
   PHARMACY_AI_MODEL=llama3.2:3b
   PHARMACY_EMBEDDING_MODEL=nomic-embed-text
   PHARMACY_VECTOR_ENABLED=true
   ```

3. Recreate the API using `docker compose up -d --no-deps api`.
4. Sign in as Admin, open **AI Workspace → Knowledge index**, and index the current guide before testing semantic help.
5. To reuse a Windows-host Ollama instead, set `PHARMACY_OLLAMA_URL=http://host.docker.internal:11434/`. Its listener/firewall must allow the Docker host connection. Models installed on Windows are not copied into the Ollama container.

The local Compose stack explicitly sets `Ai__AllowInsecureHttp=true` for private HTTP service names. The application still rejects non-HTTP(S) URLs and URL-embedded credentials; outside this stack the opt-out defaults to false. Keep remote model/vector services on HTTPS. Langflow is not included as another container; an existing HTTPS Langflow endpoint can be configured using the settings in [AI features](../backend/Documents/AI_FEATURES.md).

## Build images individually

Use the repository root as the context for both Dockerfiles:

```powershell
docker build -f backend/Pharmacy.Api/Dockerfile --target final -t pharmacy-api:local .
docker build -f backend/Pharmacy.Api/Dockerfile --target migrator -t pharmacy-migrator:local .
docker build -f frontend/Dockerfile -t pharmacy-web:local .
```

The frontend image expects the backend to be reachable as `api:8080` on its Docker network. Provide runtime database/JWT settings to the API and a database connection to the migration image through your deployment secret configuration; credentials are not embedded in the image.

## Customer deployment

This supplied Compose file is a **local evaluation setup**. SQL Server Developer is for development/test rather than commercial production use. For customer deployment, use an appropriately licensed database, a dedicated least-privilege application login, trusted SQL TLS certificates, backups and a restore test.

Terminate HTTPS at the customer ingress/proxy and configure ASP.NET forwarded-header trust for that known proxy. The local file explicitly sets `Hosting__AllowHttp=true` to permit its localhost HTTP UI. For deployment requiring application HTTPS redirection, remove this opt-out or set it to false and supply a correctly trusted HTTPS forwarding configuration. Do not expose the HTTP setup publicly unchanged. Use a deployment secret store rather than a shared `.env`, disable bootstrap after onboarding, and pin/refresh image digests through your release process.

A running Docker engine is required to verify image builds, health checks and Linux runtime behavior. Compose validation and .NET tests alone do not prove that the images start successfully.

References: [.NET container user/port defaults](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8/containers), [Compose startup services](https://docs.docker.com/reference/compose-file/services/), [SQL Server container deployment](https://learn.microsoft.com/sql/linux/sql-server-linux-docker-container-deployment).

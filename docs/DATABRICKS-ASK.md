# Ask + Databricks + Bedrock (read-only)

JobDesk **Ask** can ground answers in the **synthetic** `jobdesk_synthetic` schema on Databricks, then optionally summarize with **Amazon Bedrock Nova Micro** on the existing Lambda demo (`jobdesk-demo`, `us-east-1`).

**This document separates code readiness from live deployment.** Shipping this PR does **not** mean the hosted demo is already calling Databricks; it means the app is ready **when** a supported server-side token and catalog are configured.

## Security model (code)

- Only **ten allowlisted SELECT** statements run (see `sql/synthetic/example_queries.sql`). User questions map to intents in code; **no user/model SQL**.
- Tokens are **never** in git or `appsettings*.json`. No browser sessions, cookies, or internal workspace UI APIs are used.
- Implemented auth: **`PatBearer`** only — `Authorization: Bearer <token>` on the [SQL Statement Execution API](https://docs.databricks.com/api/workspace/statementexecution).
- **Not implemented / not assumed:** OAuth service-principal M2M, account-level APIs, or any credential type blocked on [Databricks Free Edition limitations](https://docs.databricks.com/aws/en/getting-started/free-edition-limitations).

## Data (as-of 2026-11-06)

| Item | Value |
|---|---|
| Synthetic bundle | Fixed **2026-11-06** (`Databricks__AsOfDate` default) |
| Workspace URL | `https://dbc-dab8e394-216d.cloud.databricks.com` |
| SQL warehouse ID | `2f980fb501d6a119` (config default; verify in workspace) |
| Schema | `jobdesk_synthetic` (eight Delta tables per seed SQL) |
| Unity Catalog name | **TBD** — parent discovers after notebook seed in workspace; set `Databricks__Catalog` on Lambda |
| Load method | **SQL notebook / SQL editor** in workspace (`setup_seed_databricks.sql` from dataset zip) — **not** performed by JobDesk |

Tables: `customers`, `crews`, `materials`, `work_orders`, `material_usage`, `invoices`, `payments`, `activity_notes`.

Fully qualified names in queries: `{catalog}.jobdesk_synthetic.{table}`.

## Code readiness (this repo — no live Databricks required)

- [x] Allowlisted intent → SQL builder with read-only validation
- [x] Statement Execution API client with timeouts and poll caps
- [x] Ask orchestration: Databricks facts → optional Bedrock; explicit fallbacks
- [x] `source` labels that do not claim warehouse data on failure
- [x] Unit tests (`JobDesk.Tests`) for intents, SQL safety, response parsing
- [x] CI builds app + runs tests (no Databricks secret in CI)
- [x] PublicDemo still read-only; `Demo.PreparedFor` empty; no employer branding

Until `DATABRICKS_TOKEN` + `Databricks__Catalog` exist on Lambda, `/Ask` uses **local SQLite** with an explicit “Databricks not configured / unavailable” note when `Databricks:Enabled=true`.

## Live deploy auth prerequisites (parent / ops — **not confirmed yet**)

Free Edition **unattended SQL API access is not confirmed** for this workspace. Do not assume account-console or account-level API patterns from the limitations doc.

| Prerequisite | Status | Notes |
|---|---|---|
| PAT (or equivalent) for SQL warehouse API | **TBD** | Parent to inspect workspace **Settings → Developer → Access tokens** after sign-in ([Free Edition limitations](https://docs.databricks.com/aws/en/getting-started/free-edition-limitations)). |
| Service principal OAuth M2M | **Do not assume** | Excluded / unverified for Free Edition unattended use. |
| Seed data in catalog | **Parent** | Run `setup_seed_databricks.sql` via notebook; note catalog name. |
| Read-only grants | **Parent** | `SELECT` on `{catalog}.jobdesk_synthetic.*` only. |
| Lambda env: `DATABRICKS_TOKEN` or `Databricks__AccessToken` | **Missing** | Set in Lambda console/secret manager when PAT exists. |
| Lambda env: `Databricks__Catalog` | **Missing** | After seed, e.g. `main` or workspace default catalog. |
| Lambda IAM: `bedrock:InvokeModel` for Nova Micro | **Likely on live demo** | Existing `amazon.nova-micro-v1:0` path. |

**When PAT is unavailable:** Leave token unset; demo stays on local SQLite. Do not embed workspace login in the app.

### Application environment variables (when going live)

| Variable | Required for warehouse Ask | Notes |
|---|---|---|
| `DATABRICKS_TOKEN` or `Databricks__AccessToken` | Yes (when enabling live warehouse) | PAT Bearer only; never commit. |
| `Databricks__Catalog` | Yes | From post-seed discovery. |
| `Databricks__AuthMode` | Optional | Default `PatBearer`. Other modes are rejected at runtime. |
| `Databricks__Host` | Optional | Default in `appsettings.PublicDemo.json`. |
| `Databricks__WarehouseId` | Optional | Default `2f980fb501d6a119`. |
| `Databricks__Schema` | Optional | Default `jobdesk_synthetic`. |
| `Databricks__AsOfDate` | Optional | Default `2026-11-06`. |
| `Databricks__Enabled` / `Ask__UseDatabricks` | Optional | `true`/`false`. |
| `Ask__UseBedrock`, `BEDROCK_MODEL_ID` | Optional | Same as current Lambda Bedrock setup. |

Non-secrets in `JobDesk/appsettings.PublicDemo.json`: `Databricks:Enabled=true`, host, warehouse, schema, as-of; **`Catalog` intentionally empty** until env is set.

## Troubleshooting 401 Unauthorized (live)

**Ruled out for workspace `dbc-dab8e394-216d…`:** trailing-slash redirects — direct `POST /api/2.0/sql/statements` and `…/statements/` return 200 with redirects disabled; CloudWatch shows the same absolute URL with 401 (no `Location` hop).

Typical causes after URL/path checks:

1. **Authorization not on the wire** — older builds set `AuthenticationHeaderValue`; fix uses `TryAddWithoutValidation("Authorization", "Bearer …")` and logs **`outboundAuthorizationPresent`**, **`authorizationValueLength`**, **`patLength`** (never the secret).
2. **`Databricks__AccessToken` empty string** — legacy `??` in `Program.cs` kept `""` and ignored `DATABRICKS_TOKEN`. Fix: `DatabricksPatLoader` treats whitespace-only as missing; prefer `DATABRICKS_TOKEN` first.
3. **PAT pasted with `Bearer ` prefix** — normalized at runtime.
4. **Workspace network / IP access** — same PAT can work from a laptop but fail from **AWS Lambda egress IPs** if the workspace restricts networks (often still HTTP 401). Compare a one-off Lambda curl vs local; check Databricks admin **IP access lists / firewall** settings.
5. **Wrong host vs PAT workspace** — cold-start log: `tokenLength`, `host`, `warehouse`, `catalog`.

Parent checks (no secrets) after deploying the fix branch:

- Cold start: `Ask Databricks: enabled host=… tokenLength=…`
- On Ask 401: `Databricks HTTP POST … outboundAuthorizationPresent=… bearerPrefix=… authorizationValueLength=…` then `Databricks statement create 401 … wwwAuthenticate=…`
- If `outboundAuthorizationPresent=false` → app transport bug (header not attached).
- If `true` and lengths match console PAT count but still 401 → token bytes on Lambda differ from laptop, stale deploy artifact, or **network policy** on Databricks side.

## Limits

- `StatementTimeoutSeconds` (default 20) and poll caps fit Lambda **30s** timeout.
- `MaxRows` (default 100) caps displayed rows.
- Free Edition cold starts / warehouse queue → expect `*-databricks-fallback` to local SQLite.

## Deployment steps (human, after auth confirmed)

1. Merge PR after green CI (code deploy only; no auto secrets).
2. Parent: seed via SQL notebook; confirm catalog; create PAT if offered.
3. Lambda: set `DATABRICKS_TOKEN`, `Databricks__Catalog`; keep `ASPNETCORE_ENVIRONMENT=PublicDemo`.
4. CD on `main` uploads zip (existing workflow).
5. Verify `/Ask?q=Work%20orders%20by%20status` → `source: databricks` or `databricks+bedrock` (not `local+databricks-unconfigured`).

## Ask `source` labels

| `source` | Meaning |
|---|---|
| `databricks+bedrock` | Allowlisted SQL succeeded; Nova summarized facts. |
| `databricks` | Warehouse rows only. |
| `databricks-help` | No allowlisted intent match. |
| `local+databricks-unconfigured` | Enabled but missing token/catalog/auth. |
| `*-databricks-fallback` | Warehouse error; local SQLite with disclaimer. |
| `bedrock` / `local` / `fallback` | Databricks disabled. |

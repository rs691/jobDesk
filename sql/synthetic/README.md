# Synthetic warehouse reference (as-of 2026-11-06)

Wholly synthetic learning data. Not employer-specific.

| Artifact | Purpose |
|---|---|
| `example_queries.sql` | Ten allowlisted read-only questions the Ask page maps to (must stay in sync with `JobDesk/Services/Databricks/`). |
| `manifest.json` | Row counts and SHA metadata from the generator bundle. |

**Databricks schema** (Unity Catalog): `{catalog}.jobdesk_synthetic` with tables `customers`, `crews`, `materials`, `work_orders`, `material_usage`, `invoices`, `payments`, `activity_notes`.

Load data with the parent-provided `setup_seed_databricks.sql` from the dataset bundle (not committed here — large file). See [docs/DATABRICKS-ASK.md](../../docs/DATABRICKS-ASK.md) for deployment.

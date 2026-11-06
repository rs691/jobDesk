# JobDesk, explained

## 60-second pitch

> "JobDesk is a small ASP.NET Core MVC app for field-service-style work orders: customers, jobs, materials, and SQL-backed reports. A Razor page calls a thin controller, the controller calls a service through an interface, and the service uses EF Core against SQLite (or SQL Server with real stored procedures). The schema is normalized to 3NF, and there is a page that walks through 1NF → 2NF → 3NF using the app’s own data. Work-order files go through an `IFileStorage` interface: a local folder by default, or Amazon S3 with a config change. There is an optional **Ask** page that answers short questions from the job list, using heuristics locally or Amazon Bedrock on the hosted demo when configured. The public demo runs on AWS Lambda in read-only mode: mutations return **Changes unavailable** (403), except the Ask page which is read-only GET."

## Framing the sample data

**Why this domain:** Work orders, material lines (posts, panels, concrete, hardware), and site photos are realistic records for crews and dispatch. The names, prices, and scenarios are **invented** sample data.

**Don’t claim:**

- That the demo reflects any real employer’s systems, data, or processes;
- That the hosted demo uses SQL Server or S3 (it’s SQLite under `/tmp`, read-only, with local file storage);
- That it is production-ready (see the table at the end).

**Prepared-for line:** Leave `Demo:PreparedFor` empty in `JobDesk/appsettings.PublicDemo.json`, or set the Lambda environment variable `Demo__PreparedFor` to empty, to hide the optional home-page callout.

## Which code does what

| Layer | Where in the code | What it's responsible for |
|---|---|---|
| Browser / Razor views | `JobDesk/Views/**.cshtml`, `Views/Shared/_Layout.cshtml` | HTML with Bootstrap. Forms post back to controllers. Tag helpers (`asp-for`, `asp-action`) build inputs and links. |
| Controllers | `Controllers/CustomersController.cs`, `WorkOrdersController.cs`, `ReportsController.cs`, `LearnController.cs`, `AskController.cs` | Thin: read the request, check `ModelState`, call a service, pick a view or redirect. No SQL here. |
| Services + DI | `Services/IJobService.cs` + `JobService.cs`, `Services/AttachmentService.cs`, `Services/IAskService` + `AskService`, registered in `Program.cs` | Business and data logic behind interfaces. |
| EF Core DbContext | `Data/JobDeskContext.cs`, `Data/SeedData.cs` | Maps C# classes to tables, creates and seeds the database, and refuses to touch an outdated one. |
| Models | `Models/*.cs` | Entities and report row classes. |
| Normalization | `Views/Learn/Normalization.cshtml`, `NORMALIZATION.md` | Rebuilds the "spreadsheet" from the real tables and shows 1NF → 2NF → 3NF. |
| Stored procedures | `sql/stored-procedures.sql`, `JobService.GetOpenJobsReportAsync` / `GetWorkOrdersByCustomerAsync` | **SQLite** (F5 and the hosted demo): the same SELECT runs inline. **SQL Server** (the LocalDB profile): the app installs the procs and calls `EXEC dbo.usp_...`. |
| Bug fix | `BUG.md`, `JobService.OpenJobsSql` vs `OpenJobsSqlBuggy` | The wrong join grain and INNER JOIN, fixed with a pre-aggregated subquery, LEFT JOIN, and the filter moved to `ON`. |
| File storage | `Storage/IFileStorage.cs`, `LocalFileStorage.cs`, `S3FileStorage.cs` | **Local** (default): files go in `App_Data/uploads`. **S3**: `Storage:Provider = S3` plus a bucket name; credentials come from the AWS SDK, never from config files. |
| Ask | `Services/AskService.cs`, `Services/BedrockAskClient.cs`, `AskController`, `Views/Ask/Index.cshtml` | Builds a text snapshot from customers and work orders; optional Bedrock via `Ask:UseBedrock` or env `Ask__UseBedrock` / `BEDROCK_MODEL_ID`; heuristic fallback if Bedrock fails or is off. |
| Demo safety | `Hosting/DemoMode.cs`, `appsettings.PublicDemo.json` | Read-only mode. Middleware returns 403 for mutating methods (with **Changes unavailable** HTML). `/Ask` is allowed for GET only. |
| AWS hosting | `Program.cs` (`AddAWSLambdaHosting(LambdaEventSource.HttpApi)`), `aws-lambda-tools-defaults.json`, `DEPLOY-AWS-LAMBDA.md` | The same app runs inside AWS Lambda (.NET 8, handler `JobDesk`) behind a Function URL. Outside Lambda, that line does nothing. |

## Demo click sequence (about 3 minutes)

1. **Home** → quick links to reports, normalization, Ask, and a sample work order.
2. **Work orders** → filter by **Open** → "A LINQ `Where` in `JobService`."
3. Click a **job title** → Details: materials from the junction table. "Uploads go through `IFileStorage`. In the public demo writes are blocked; locally they save to a folder or S3 with config."
4. **Reports** → stored procedure vs inline SQLite, optional BUG toggle.
5. **Normalization** → spreadsheet → 1NF → 2NF → 3NF.
6. **Ask** → try "open jobs by customer" (heuristic locally; Bedrock when enabled on Lambda).
7. Optional: open `ReportsController.Index` → `JobService.GetOpenJobsReportAsync` → `sql/stored-procedures.sql`.

## What I practiced vs. what production would need

| Practiced here | A production version would add |
|---|---|
| MVC + DI + interfaces, thin controllers | Authentication and roles |
| EF Core with SQLite; SQL Server procs via LocalDB | EF Core **migrations**, persistent DB (e.g. RDS) |
| Parameterized SQL, join grain | Automated tests |
| 3NF schema | Audit history, soft deletes |
| `IFileStorage` local + S3 | IAM-scoped bucket, pre-signed URLs |
| Read-only public demo on Lambda | Monitoring, custom domain |

## Be honest about the hosted demo

- It runs **SQLite in `/tmp`**, re-seeded on cold starts. It is **not** a persistent database and does not use SQL Server on the host.
- Reports show **Provider: SQLite** on the demo; T-SQL stored procedure definitions are shown as text; they are exercised on SQL Server in the LocalDB profile, not on the hosted runtime path.
- It **does not use S3** on Lambda; uploads are blocked. The S3 code is config-switchable for local practice.
- Writes are blocked: POST/PUT/PATCH/DELETE return 403 with **Changes unavailable**, except the Ask controller uses GET only.
- Data is invented (example-style contacts). No real customers.

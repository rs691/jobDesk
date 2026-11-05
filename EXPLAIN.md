# JobDesk, explained (for Robert)

## 60-second pitch

> "JobDesk is a small ASP.NET Core MVC app I built to practice the junior developer stack: customers, work orders, and reports for a field-service crew. A Razor page calls a thin controller, the controller calls a service through an interface, and the service uses EF Core against SQLite. The reports are parameterized SQL, written as real T-SQL stored procedures that the app calls with EXEC when it runs on SQL Server. On SQLite it runs the same SQL inline, because SQLite has no procs. The schema is normalized to 3NF, and there's a page that walks through 1NF, 2NF, and 3NF using the app's own data. Work-order photos go through an `IFileStorage` interface: a local folder by default, or Amazon S3 with a config change. I also fixed a real reporting bug: a join at the wrong grain over-counted jobs and dropped customers. The public demo runs on AWS Lambda in read-only mode with sample data."

## For the American Fence Company interviewer

**Why this domain:** I picked work orders, crews' material lists (pickets, posts, concrete, chain link, gate kits), and job-site photos because those are the everyday records a fence installer would plausibly track. It shows I thought about the business, not just the code.

**How to frame it (say this, roughly):**
> "I built this to practice the skills in your Junior Software Developer posting: C#, ASP.NET Core MVC, SQL, and AWS. I used fence-job sample data so it would be familiar. I don't know how your internal systems work, so this isn't a copy of anything you use. It's me showing how I'd approach work orders, material reporting, and job photos."

**Don't claim:**
- that it reflects American Fence Company's real systems, data, prices, or processes;
- that the hosted demo uses SQL Server or S3 (it's SQLite, read-only, sample data);
- that it's production-ready (see the table at the end).

**Good questions to ask them:**
- "What does your stack look like today: SQL Server, an ERP, custom .NET apps?"
- "Where do work orders and material estimates live now, and what reports do crews or office staff wish they had?"
- "How do you deploy: on-premises servers, Azure, or AWS?"
- "What would a junior developer own in the first 90 days?"

The hosted demo's home page shows a "Prepared for American Fence Company" note. To remove it, or to change the name, edit `Demo:PreparedFor` in `JobDesk/appsettings.PublicDemo.json`, or set the Lambda environment variable `Demo__PreparedFor` (an empty value hides it).

## Which code does what

| Layer | Where in the code | What it's responsible for |
|---|---|---|
| Browser / Razor views | `JobDesk/Views/**.cshtml`, `Views/Shared/_Layout.cshtml` | HTML with Bootstrap. Forms post back to controllers. Tag helpers (`asp-for`, `asp-action`) build inputs and links. The layout shows the demo banner. |
| Controllers | `Controllers/CustomersController.cs`, `WorkOrdersController.cs`, `ReportsController.cs`, `LearnController.cs` | Thin: read the request, check `ModelState`, call a service, pick a view or redirect. No SQL here. |
| Services + DI | `Services/IJobService.cs` + `JobService.cs`, `Services/AttachmentService.cs`, registered in `Program.cs` | Business and data logic behind interfaces. `Program.cs` decides which implementation gets injected (`AddScoped<IJobService, JobService>()`). |
| EF Core DbContext | `Data/JobDeskContext.cs`, `Data/SeedData.cs` | Maps C# classes to tables, sets up foreign keys, stores the enum as text, creates and seeds the database, and refuses to touch an outdated one. |
| Models | `Models/*.cs` | Entities (`Customer`, `WorkOrder`, `Material`, `WorkOrderMaterial`, `WorkOrderAttachment`) and report row classes. |
| Normalization | `Views/Learn/Normalization.cshtml`, `NORMALIZATION.md` | Rebuilds the "spreadsheet" from the real tables and shows 1NF → 2NF → 3NF, plus why the junction table and FKs exist. |
| Stored procedures | `sql/stored-procedures.sql`, `JobService.GetOpenJobsReportAsync` / `GetWorkOrdersByCustomerAsync` | **SQLite** (F5 and the hosted demo): the same SELECT runs inline. **SQL Server** (the LocalDB profile): the app installs the procs and calls `EXEC dbo.usp_...`. The Reports page says which one ran. |
| Bug fix | `BUG.md`, `JobService.OpenJobsSql` vs `OpenJobsSqlBuggy` | The wrong join grain and INNER JOIN, fixed with a pre-aggregated subquery, LEFT JOIN, and the filter moved to `ON`. |
| File storage | `Storage/IFileStorage.cs`, `LocalFileStorage.cs`, `S3FileStorage.cs` | **Local** (default): files go in `App_Data/uploads`. **S3**: `Storage:Provider = S3` plus a bucket name; credentials come from the AWS SDK, never from config files. |
| Demo safety | `Hosting/DemoMode.cs`, `appsettings.PublicDemo.json` | Read-only mode. The middleware returns 403 for every POST/PUT/PATCH/DELETE, and the services also refuse writes. |
| AWS hosting | `Program.cs` (`AddAWSLambdaHosting(LambdaEventSource.HttpApi)`), `aws-lambda-tools-defaults.json`, `DEPLOY-AWS-LAMBDA.md` | The same app runs inside AWS Lambda (.NET 8, handler `JobDesk`) behind a Function URL. Outside Lambda, that line does nothing. |

## Demo click sequence (about 3 minutes)

1. **Home** → "It's one MVC app. Here are the three topics I practiced."
2. **Work orders** → filter by **Open** → "A LINQ `Where` in `JobService`. The controller just passes the filter."
3. Click a **job title** → Details: materials come from the junction table. "Uploads go through `IFileStorage`. In the public demo they're turned off; locally they save to a folder, and in production they'd go to S3."
4. **Reports** → point at "Stored procedure: `dbo.usp_OpenJobsByCustomer`" and "Provider: SQLite", then expand **Stored procedure definition**. "On SQL Server the app runs `EXEC`. SQLite has no procs, so it runs the same SQL."
5. Flip **Original (buggy) query** → 6 jobs with two customers missing, versus 4. Tell the BUG.md story: reproduce → check join grain → fix → verify.
6. **Normalization** → the spreadsheet row, the highlighted copies in 1NF ("Maple's phone stored 4 times"), then 2NF, 3NF, and the real tables.
7. Optional: open the code in VS. Go `ReportsController.Index` → `JobService.GetOpenJobsReportAsync` → `sql/stored-procedures.sql`.

## What I practiced vs. what production would need

| I practiced | A production version would add |
|---|---|
| MVC + DI + interfaces, thin controllers | Authentication and roles (who can edit what) |
| EF Core with SQLite; real SQL Server procs via the LocalDB profile | EF Core **migrations** instead of `EnsureCreated`, and a persistent DB such as **Amazon RDS** |
| Parameterized SQL, INNER vs LEFT JOIN, join grain | Automated tests (unit tests for services, integration tests for SQL) |
| 3NF schema, FKs, junction table | Audit history, soft deletes, maybe price snapshots on lines |
| `IFileStorage` with a local and an S3 implementation (S3 tested against an emulator, not a real bucket) | A real bucket with an IAM role, pre-signed URLs, virus scanning, size quotas |
| A read-only public demo on Lambda with seeded SQLite in `/tmp` | Persistent storage, monitoring/alerts, CI/CD, a custom domain |

## Be honest about the hosted demo

- It runs **SQLite in `/tmp`**, re-seeded on each cold start. It's **not** a persistent database, and it doesn't use SQL Server.
- On the hosted demo, the Reports page shows **"Provider: SQLite"**. The stored procedures are shown as T-SQL text; they were exercised on SQL Server locally, not on the hosted demo.
- It **does not use S3**, and uploads are disabled. The S3 code exists and is config-switchable, but there's no live bucket behind the demo.
- It's read-only by design. Every write request gets a 403 from the server.
- The data is invented (example.com-style emails, 555 phone numbers). No real people or customers.

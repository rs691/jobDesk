# JobDesk

A small ASP.NET Core MVC practice app: customers, work orders, SQL-backed reports, and work-order photo uploads.
I built it to practice for a junior developer interview, so it is not production software.
The path to walk through is **Razor view → controller → service → EF Core → SQLite or SQL Server, and file storage → local folder or S3**.

**Stack:** .NET 8, ASP.NET Core MVC, EF Core 8 (SQLite by default, SQL Server optional), AWS SDK for .NET (S3), Razor + Bootstrap. One solution, one web project.

## Run it

**Visual Studio 2022 (17.8+):** open `JobDesk.sln`, then press **F5**. The default `http` profile uses SQLite and local file storage, and needs no setup.

**Command line:**

```bash
dotnet build
dotnet run --project JobDesk
```

On first run, `JobDesk/jobdesk.db` is created and seeded (`Data/SeedData.cs`). The app **never deletes or alters an existing database**. If it finds a `jobdesk.db` from an older version of JobDesk, it stops with a message telling you to back the file up and then delete or rename it. Uploaded files go to `JobDesk/App_Data/uploads/`.

### Launch profiles (the Visual Studio dropdown next to the green F5 button)

| Profile | Database | File storage | Needs |
|---|---|---|---|
| `http` / `https` | SQLite | Local folder | Nothing |
| `PublicDemo (read-only)` | SQLite (`jobdesk-demo.db`), sample data | Local folder | Nothing. Matches the hosted demo: writes return **Changes unavailable** (403); create/edit/upload controls are hidden. Optional **Ask** page (Bedrock off locally unless you configure it). |
| `SqlServer (LocalDB, stored procs)` | SQL Server LocalDB, real stored procedures | Local folder | LocalDB, which installs with Visual Studio's "ASP.NET and web development" workload |
| `AWS S3 storage` | SQLite | Amazon S3 | A bucket name and AWS credentials (see the AWS section below) |

Each profile only sets `ASPNETCORE_ENVIRONMENT`, which loads `appsettings.SqlServer.json` or `appsettings.Aws.json` on top of `appsettings.json`. **Switching is config only. No code changes.**

## Public read-only demo mode

`ASPNETCORE_ENVIRONMENT=PublicDemo` loads `appsettings.PublicDemo.json`, which sets `Demo:ReadOnly = true`, leaves `Demo:PreparedFor` empty (no “prepared for” line on the home page), puts SQLite and uploads under `/tmp`, trusts proxy headers, and shows generic error pages. The app logs that writes are blocked at startup.

- **Enforced on the server, not just by hiding buttons.** `ReadOnlyDemoMiddleware` returns **403** with an HTML “Changes unavailable” page for every POST, PUT, PATCH, and DELETE except **GET/HEAD** on `/Ask` (the Ask page uses GET only). As a second layer, `JobService` and `AttachmentService` throw if a write is attempted.
- The New, Edit, Upload, and Delete controls are hidden in Razor when read-only; there is no yellow demo banner in the layout.
- To verify, run `scripts/demo-smoke-test.sh <base-url>`. It checks the GET pages, then sends every create/edit/upload/delete endpoint with a valid antiforgery token, plus PUT/PATCH/DELETE, and confirms 403s and unchanged data.
- **Hosting:** see [DEPLOY-AWS-LAMBDA.md](DEPLOY-AWS-LAMBDA.md) (.NET 8 Lambda + Function URL). For a beginner walkthrough of every part, see [EXPLAIN.md](EXPLAIN.md).
- **CI/CD:** GitHub Actions builds and smoke-tests every pull request, and deploys `main` to the Lambda demo. See [CI-CD.md](CI-CD.md).

## What's where

```
JobDesk.sln
JobDesk/
  Models/         Customer, WorkOrder (+ WorkOrderStatus), Material, WorkOrderMaterial, WorkOrderAttachment, report rows
  Data/           JobDeskContext (EF Core), SeedData, StoredProcedureScripts (reads sql/stored-procedures.sql)
  Services/       IJobService/JobService (CRUD + reports), IAttachmentService/AttachmentService (uploads), Ask (optional Bedrock)
  Storage/        IFileStorage, LocalFileStorage, S3FileStorage, options classes
  Hosting/        DemoOptions + ReadOnlyDemoMiddleware (public demo mode)
  Controllers/    Home, Customers, WorkOrders, Reports, Learn, Ask (thin: validate → call service → view)
  Views/          Razor + Bootstrap
  appsettings*.json   Database:Provider, Storage:Provider, AWS:* (no secrets)
sql/
  stored-procedures.sql   dbo.usp_OpenJobsByCustomer, dbo.usp_WorkOrdersByCustomer (T-SQL)
  demo-joins.sql          INNER vs LEFT, grain, WHERE vs ON (SQLite)
BUG.md             The report bug: reproduce → root cause → fix → verify
NORMALIZATION.md   1NF → 2NF → 3NF using JobDesk's data
```

| Page | What it does |
|---|---|
| Ask | Short natural-language answers from the seeded work-order data. **Local default:** heuristic answers only (`Ask:UseBedrock` false in `appsettings.json`). **Hosted demo:** can use Amazon Bedrock Nova Micro when IAM allows `bedrock:InvokeModel` and env/config set `Ask__UseBedrock` / `BEDROCK_MODEL_ID` (falls back to heuristics if Bedrock fails). |
| Customers | List, create, edit |
| Work orders | List with status filter, create, edit. **Details** shows materials and photo/file attachments (upload, view, delete). |
| Reports | Two stored-procedure reports. Each shows the proc name, what it returns, the provider, the SQL that ran, and the T-SQL definition. Includes the BUG.md toggle. |
| Normalization | A live before/after lesson: spreadsheet → 1NF → 2NF → 3NF |

---

## 1. Stored procedures

**Files:** `sql/stored-procedures.sql`, `Services/JobService.cs` (`GetOpenJobsReportAsync`, `GetWorkOrdersByCustomerAsync`), `Views/Reports/`.

| Procedure | Parameter | Returns |
|---|---|---|
| `dbo.usp_OpenJobsByCustomer` | `@Status NVARCHAR(20)` | One row per customer: work order count and material cost for that status |
| `dbo.usp_WorkOrdersByCustomer` | `@CustomerId INT` | One row per work order for that customer: material line count and cost |

How the C# chooses a path:

```csharp
var callProc = _db.Database.IsSqlServer();
var sql = callProc
    ? "EXEC dbo.usp_WorkOrdersByCustomer @CustomerId = @customerId"   // SQL Server: real proc
    : WorkOrdersByCustomerSql;                                         // SQLite: same SELECT inline
var rows = await _db.Database
    .SqlQueryRaw<CustomerJobsRow>(sql, Param("@customerId", customerId))   // always a bound parameter
    .ToListAsync();
```

- **SQLite has no stored procedures**, so on F5 the identical SELECT runs inline. The Reports page says so in plain words.
- **On SQL Server** (the LocalDB profile, or RDS), the app creates the tables and runs `sql/stored-procedures.sql` at startup. `CREATE OR ALTER` makes that safe to repeat. The reports then call `EXEC`.
- The Reports page shows the T-SQL by reading the same `.sql` file, so what you show in the interview is exactly what runs.

**60-second demo:**
1. Open **Reports**. Point at "Stored procedure: `dbo.usp_OpenJobsByCustomer`", "Returns: …", and "Provider: SQLite".
2. Expand **Stored procedure definition** to show `CREATE OR ALTER PROCEDURE … @Status`, `SET NOCOUNT ON`, and the LEFT JOINs.
3. Expand **SQL that ran**. Locally it's the inline SELECT. Say: "On SQL Server this line is `EXEC dbo.usp_OpenJobsByCustomer @Status = @status`."
4. Optional, if LocalDB is installed: switch the profile to **SqlServer (LocalDB, stored procs)** and press F5. The footer now says "Ran on SQL Server by calling EXEC dbo.usp_…". The numbers are identical.

**Talking points:** procs give one reviewed, parameterized query in the database. DBAs can tune and grant `EXECUTE` without giving table access, and the plan gets cached. The trade-off is that logic lives in two places (C# and SQL), so it must be version-controlled. That's why the script is in `sql/` and the app installs it.

## 2. Normalization

**Files:** `NORMALIZATION.md`, `Views/Learn/Normalization.cshtml`, `JobService.GetNormalizationLessonAsync`.

The page rebuilds the "spreadsheet" from the normalized tables and walks through each step:
- **Unnormalized:** one row per job, with a Materials cell holding a list.
- **1NF:** one row per material line. Repeated customer and material cells are highlighted, and the page counts them live ("Maple Street HOA's phone is stored 4 times").
- **2NF:** material facts move to `Materials`, job facts to `WorkOrders`, and `Quantity` stays in the junction table.
- **3NF:** customer facts move to `Customers`, and `WorkOrders` keeps only `CustomerId`.
- **After:** the four real tables, with PK and FK columns labeled.

**60-second demo:**
1. Open **Normalization**. Show the Materials list cell: "That's not 1NF, because you can't sum or join on it."
2. Scroll to 1NF and point at the highlighted copies: "Change Maple's phone and you'd update 4 rows. That's an update anomaly."
3. Scroll to 2NF and 3NF: "Material name and price depend only on MaterialId, and the customer phone depends on the customer, not the job."
4. Edit Maple's phone on **Customers**, then reload. It changes everywhere, because it's stored once.

**Talking points:** each fact is stored once, FKs prevent orphans, and junction tables model many-to-many. The cost is joins, which is where the BUG.md bug came from. Deliberate denormalization, such as snapshotting price on an invoice line, is a valid choice when history matters.

## 3. AWS: S3 for work-order photos

**Files:** `Storage/IFileStorage.cs`, `Storage/LocalFileStorage.cs`, `Storage/S3FileStorage.cs`, `Services/AttachmentService.cs`, `Program.cs`, `appsettings.Aws.json`.

```
WorkOrdersController ──> IAttachmentService ──> JobDeskContext  (metadata row: file name, size, StorageKey)
                                          └──> IFileStorage     (bytes)
                                                 ├─ LocalFileStorage  App_Data/uploads/   (default)
                                                 └─ S3FileStorage     s3://bucket/jobdesk/ (Storage:Provider = S3)
```

`Program.cs` picks the implementation from config:

```csharp
if (config["Storage:Provider"] == "S3")       // case-insensitive in the real code
{
    builder.Services.AddDefaultAWSOptions(config.GetAWSOptions());   // region/profile from "AWS" section
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
}
else builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
```

- Uploads are validated in the service: JPG, PNG, GIF, WEBP or PDF, 5 MB max. The storage key is generated (`work-orders/{id}/{guid}.png`) and never comes from the user's file name.
- Files are served through `/WorkOrders/Attachment/{id}`, so neither the local folder nor the bucket needs to be public.
- If the database save fails after the upload, the file is deleted so no orphaned objects are left behind.

**60-second demo (no AWS account needed):**
1. Open **Work orders**, then a job title, to reach the Details page. Point at the badge "Storage: Local folder (App_Data/uploads)".
2. Upload a photo. It appears with its storage key. Delete it.
3. Open `appsettings.Aws.json`: "Switching to S3 is this file: `Storage:Provider = S3`, a bucket and a region. No secrets in it."
4. Open `S3FileStorage.cs`: `PutObjectAsync`, `GetObjectAsync`, `DeleteObjectAsync`, with the same interface as the local version.

**Using a real bucket:**
1. Create an S3 bucket, and keep "Block all public access" on.
2. Create an IAM user (for a laptop) or an IAM role (for EC2, ECS, or Elastic Beanstalk) with only this policy:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Effect": "Allow",
       "Action": ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"],
       "Resource": "arn:aws:s3:::YOUR-BUCKET/jobdesk/*"
     }]
   }
   ```
3. Give the app credentials **without putting them in the repo**. Either run `aws configure` (writes `~/.aws/credentials`), set the env vars `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`, or, on AWS, attach the role. The SDK's default credential chain finds them.
4. Set the bucket: `dotnet user-secrets set "AWS:BucketName" "your-bucket" --project JobDesk` (or edit `appsettings.Aws.json`), then run the **AWS S3 storage** profile.
5. Optional, for testing against LocalStack or another S3 emulator: set `AWS:ServiceURL` to the emulator's URL.

**Talking points:** the interface plus dependency injection means the controller doesn't know where files go. Local is the default for development, and production is a config change. Least-privilege IAM is scoped to one prefix, and credentials come from the environment or an IAM role, never from appsettings. Metadata goes in SQL and bytes go in S3. **In production the SQL Server database would typically be Amazon RDS for SQL Server**, with its connection string stored in AWS Secrets Manager or an environment variable (`ConnectionStrings__JobDesk`), not in source.

---

## The bug story (details in [BUG.md](BUG.md))

> "The open-jobs report said 6 open work orders, but the Work orders page filtered to Open showed 4. I reproduced it with the seed data, then checked grain: the query joined work orders to material **lines**, so `COUNT(*)` counted lines. On top of that it was an INNER JOIN, so open jobs with no materials picked yet, and customers with no jobs, disappeared. I fixed it by summing materials per work order in a subquery first, LEFT JOINing from Customers, using `COUNT(wo.Id)`, and moving the status filter into the `ON` clause. Then I verified that the report total matches the filtered list: 4 jobs, same $1,593 in materials. The fixed query is now the stored procedure `usp_OpenJobsByCustomer`."

Try it live: on **Reports**, flip **Original (buggy) query**, then create a new Open work order and watch it appear only in the fixed version.

## 60-second overall talking points

1. **Shape.** "It's one MVC project. Controllers are thin: they check `ModelState`, call a service interface, and return a view."
2. **DI.** "`IJobService`, `IAttachmentService` and `IFileStorage` are registered in `Program.cs`. Config decides SQLite or SQL Server, and local disk or S3."
3. **EF + SQL.** "CRUD is LINQ with `AsNoTracking` and `Include`. Reports are parameterized raw SQL or stored procedures. I never concatenate user input into SQL."
4. **Data design.** "The schema is in 3NF, with a junction table for work-order materials. The Normalization page shows why."
5. **Debugging.** "The BUG.md story: reproduce, check join grain, fix, verify."

## INNER vs LEFT JOIN in one example

```sql
-- INNER: only open work orders that already have material lines (drops "Gate latch repair")
SELECT wo.Title, wom.Quantity
FROM WorkOrders wo
INNER JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
WHERE wo.Status = 'Open';

-- LEFT: every open work order; Quantity is NULL when nothing is picked yet
SELECT wo.Title, wom.Quantity
FROM WorkOrders wo
LEFT JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
WHERE wo.Status = 'Open';
```

Run the full walkthrough with `sqlite3 JobDesk/jobdesk.db < sql/demo-joins.sql`, or open the `.db` in DB Browser for SQLite.

## Troubleshooting

- **The SqlServer profile sits for about a minute, then fails with "Login failed" or "Cannot open database".** EF Core retries SQL Server logins quietly before giving up. Check that LocalDB exists (`sqllocaldb info` in a Developer Command Prompt should list `MSSQLLocalDB`), or override the connection string with `dotnet user-secrets set "ConnectionStrings:JobDesk" "..." --project JobDesk`.
- **The AWS profile stops at startup with "AWS:BucketName is not set".** Set the bucket as shown in step 4 of "Using a real bucket".
- **A work order's Details page takes about 20 seconds, then shows "Failed to resolve AWS credentials".** The SDK found no credentials and spent that time checking for an EC2 instance role. Run `aws configure` or set the `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` env vars. Without an AWS account, stay on the default `http` profile.
- **"The database at ... was created by an older version of JobDesk".** The file was left untouched. Copy it somewhere as a backup, then delete it (or point `ConnectionStrings:JobDesk` at a new file) and run again.
- **To reset the data,** delete `JobDesk/jobdesk.db` (and `JobDesk/App_Data/uploads/`), then run again.

## Deliberately left out

There's no auth, no separate API project, and no migrations (`EnsureCreated` keeps setup to one F5, and a check that refuses to start on an outdated database instead of changing it). In a real app I'd use `dotnet ef migrations`, add unit tests around the services, and serve S3 files with pre-signed URLs instead of streaming them through the app.

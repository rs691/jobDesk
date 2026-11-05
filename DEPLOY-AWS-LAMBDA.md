# Deploying the read-only demo to AWS Lambda

This puts the **read-only public demo** on AWS Lambda behind a **Lambda Function URL**. The steps below are for creating the function the first time. After that, code updates go through GitHub Actions; see [CI-CD.md](CI-CD.md).

**What the hosted demo is:** made-up sample data in a SQLite file under `/tmp`, re-seeded on every cold start. Every POST/PUT/PATCH/DELETE request is refused with a 403. It's ephemeral and for demos only. It is **not** a production database, and it uses **no** S3, RDS, or SQL Server.

## Lambda settings

| Setting | Value |
|---|---|
| Runtime | **.NET 8** (`dotnet8`, managed runtime) |
| Architecture | **x86_64** (the package is published for `linux-x64`) |
| Handler | **`JobDesk`** (the assembly name; the app uses ASP.NET Core "executable assembly" hosting) |
| Memory / timeout | 1024 MB / 30 s |
| Environment variable | **`ASPNETCORE_ENVIRONMENT=PublicDemo`** (turns on read-only mode, `/tmp` paths, proxy headers, generic error pages) |
| Front door | **Function URL**, auth type `NONE` (public) |
| Event source in code | `AddAWSLambdaHosting(LambdaEventSource.HttpApi)`. Function URLs send the same payload format 2.0 as API Gateway HTTP APIs, so `HttpApi` is the correct choice. If you later put an HTTP API in front instead, no code change is needed. |

## Option A: AWS Console (no extra tools)

1. **Lambda → Create function → Author from scratch.** Name `jobdesk-demo`, runtime **.NET 8**, architecture **x86_64**. Let it create the default execution role (basic CloudWatch Logs only).
2. **Code → Upload from → .zip file** and pick `JobDesk-lambda-publish.zip`.
3. **Runtime settings → Edit → Handler:** `JobDesk`.
4. **Configuration → General:** memory 1024 MB, timeout 30 s.
5. **Configuration → Environment variables:** `ASPNETCORE_ENVIRONMENT` = `PublicDemo`.
6. **Configuration → Function URL → Create:** auth type `NONE`. Copy the URL.
7. **Cost guard (recommended):** set **Configuration → Concurrency → Reserved concurrency = 2**, so a flood of requests can't scale it up. Optionally add an AWS Budgets alert.
8. Open the URL. The first request after idle is a cold start (about 1 to 3 s). Then run the smoke test from any machine with bash:
   ```bash
   scripts/demo-smoke-test.sh https://<id>.lambda-url.<region>.on.aws
   ```

## Option B: command line

```bash
dotnet tool install -g Amazon.Lambda.Tools
cd JobDesk
dotnet lambda deploy-function        # reads aws-lambda-tools-defaults.json; asks for / creates an execution role
```

This uses your AWS CLI credentials (`aws configure`). None are stored in the repo.

## Rebuilding the publish zip yourself

```bash
cd JobDesk
dotnet publish -c Release -r linux-x64 --self-contained false -o ../publish/lambda
cd ../publish/lambda && zip -r ../JobDesk-lambda-publish.zip .     # files must be at the zip root
```

## Cost notes

With low demo traffic, Lambda requests and compute usually stay within the free tier, and you pay nothing while it's idle. Function URLs have no separate charge. CloudWatch Logs storage is a few cents at most. Check current pricing for your account; this guide doesn't create anything that bills by the hour.

## Known limits of this demo hosting

- Data resets on cold starts, and every Lambda instance has its own copy. That's fine because the demo is read-only.
- Antiforgery keys are kept in memory (there's nowhere persistent to store them). Warnings about this in the logs are expected and harmless here, because forms can't submit anyway.
- Real production would use a persistent database (for example RDS), S3 for files, authentication, and migrations. See EXPLAIN.md.

## If Lambda is blocked

Any container host works too: run `dotnet publish` and start it with `ASPNETCORE_ENVIRONMENT=PublicDemo` and `ASPNETCORE_URLS=http://0.0.0.0:$PORT`. No Dockerfile was needed for the Lambda route, so none is included.

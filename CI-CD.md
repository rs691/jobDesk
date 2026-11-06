# CI/CD with GitHub Actions

Everything lives in one workflow file: [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml).

## CI vs CD, in plain words

- **CI (continuous integration)** answers "does this change still build and work?" It runs on every pull request to `main`, on every push to `main`, and when you start it by hand. It never touches AWS.
- **CD (continuous deployment)** answers "put the tested build on the live demo." It runs only on `main`, and only after CI passed in the same run. It uploads the **exact zip CI tested**, not a fresh build.

## What CI does (job `build-test`)

1. `dotnet restore` and `dotnet build -c Release` for .NET 8.
2. `dotnet publish -c Release -r linux-x64 --self-contained false`, then zips the output with the files at the zip root. This is the Lambda package (about 7.7 MB). The app runs on Lambda through `Amazon.Lambda.AspNetCoreServer.Hosting` (`AddAWSLambdaHosting(LambdaEventSource.HttpApi)`, payload format 2.0), with handler `JobDesk`.
3. Checks the zip: `JobDesk.dll`, the runtime config, and the Lambda hosting DLLs are at the root, and `appsettings.PublicDemo.json` still says read-only, SQLite, a `/tmp` database file, local (not S3) storage, and **empty** `Demo:PreparedFor`.
4. Starts the published app in **PublicDemo** mode and runs `scripts/demo-smoke-test.sh`: the GET pages return 200 (including `/Ask`), and every mutating POST/PUT/PATCH/DELETE gets a server-side 403 with data unchanged.
5. Starts it again in **local practice** mode and runs `scripts/practice-smoke-test.sh`: pages load and a real form POST adds a customer, so local full practice keeps working.
6. Saves the zip as a run artifact (kept 7 days).

There is no unit-test project in this repo; the two smoke tests are the tests.

## What CD does (job `deploy`)

1. Fails with a clear message if a required repository variable is missing.
2. Gets **short-lived AWS credentials through GitHub OIDC** (`aws-actions/configure-aws-credentials`). There are no AWS keys anywhere in the repo or in GitHub Secrets.
3. Reads the function's configuration and refuses to deploy unless it is still runtime `dotnet8`, handler `JobDesk`, and `ASPNETCORE_ENVIRONMENT=PublicDemo`.
4. `aws lambda update-function-code --zip-file` with the tested zip (no S3 bucket needed), then waits for the update to finish. Only the code changes. Environment variables, memory, timeout, the Function URL, and IAM are left alone.
5. If `JOBDESK_DEMO_URL` is set, runs the read-only smoke test against the live URL.

Two deploys never overlap (`concurrency: jobdesk-lambda-deploy`).

## Where the settings go

GitHub repo → **Settings → Secrets and variables → Actions → Variables tab → New repository variable**. These are **variables, not secrets**; none of them is a credential.

| Variable | Value for this demo |
|---|---|
| `AWS_REGION` | `us-east-1` |
| `AWS_ROLE_ARN` | ARN of the `jobdesk-demo-github-oidc-deploy` role (from the AWS setup) |
| `AWS_LAMBDA_FUNCTION_NAME` | `jobdesk-demo` |
| `JOBDESK_DEMO_URL` (optional, enables the live smoke test) | `https://6amrloi3la3xwbdq5ytoj3tsfa0djavu.lambda-url.us-east-1.on.aws/` |

Or with the GitHub CLI, from a machine logged in as a repo admin:

```bash
gh variable set AWS_REGION --repo rs691/jobDesk --body us-east-1
gh variable set AWS_ROLE_ARN --repo rs691/jobDesk --body "<role ARN from the AWS setup>"
gh variable set AWS_LAMBDA_FUNCTION_NAME --repo rs691/jobDesk --body jobdesk-demo
gh variable set JOBDESK_DEMO_URL --repo rs691/jobDesk --body https://6amrloi3la3xwbdq5ytoj3tsfa0djavu.lambda-url.us-east-1.on.aws/
```

## Security choices

- The workflow defaults to `contents: read`. Only the `deploy` job gets `id-token: write`, and that job never runs for pull requests.
- The AWS role trusts only `aud = sts.amazonaws.com` and `sub = repo:rs691/jobDesk:ref:refs/heads/main`. Running the workflow by hand from another branch skips the deploy job, and AWS would refuse it anyway.
- The deploy job deliberately does **not** use a GitHub "environment": that would change the OIDC subject to `...:environment:<name>` and break the trust above.
- The role can only call `UpdateFunctionCode`, `GetFunction`, and `GetFunctionConfiguration` on `jobdesk-demo`. It cannot create or change AWS resources.
- Third-party actions are pinned to full commit SHAs.

## Cost

A few cents a month at light demo traffic: Lambda bills per request, there is no provisioned concurrency, no always-on server, no RDS, and no S3 in the pipeline. GitHub Actions minutes are free for public repositories.

## Limits (be honest about these)

- The live demo runs **only SQLite** from `/tmp`, seeded with made-up sample data and reset on cold starts. It is read-only.
- **SQL Server stored procedures and S3 uploads are not live.** They run in local practice (see README), not on the hosted demo, and CI does not test them against real SQL Server or S3.
- This is an AI-assisted practice project, not production software.

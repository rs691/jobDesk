using Amazon.S3;
using JobDesk.Data;
using JobDesk.Hosting;
using JobDesk.Services;
using JobDesk.Services.Databricks;
using JobDesk.Storage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Public demo mode is off unless config turns it on (ASPNETCORE_ENVIRONMENT=PublicDemo
// loads appsettings.PublicDemo.json, or set Demo__ReadOnly=true).
var demo = config.GetSection(DemoOptions.Section).Get<DemoOptions>() ?? new DemoOptions();
builder.Services.AddSingleton(demo);

// Local practice runs (Development, SqlServer, Aws profiles) may read user secrets.
// Production and the public demo never do.
if (!builder.Environment.IsProduction() && !demo.ReadOnly)
    config.AddUserSecrets<Program>(optional: true);

builder.Services.AddControllersWithViews();

// Runs the app inside AWS Lambda behind a Function URL (payload v2 = HttpApi event source).
// Does nothing when the app runs outside Lambda, e.g. F5 or dotnet run.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Database: SQLite by default (file created on first run). Set Database:Provider = SqlServer
// to use SQL Server / LocalDB / RDS; the report then calls the stored procedures.
var connectionString = config.GetConnectionString("JobDesk");
var useSqlServer = string.Equals(config["Database:Provider"], "SqlServer", StringComparison.OrdinalIgnoreCase);
builder.Services.AddDbContext<JobDeskContext>(options =>
{
    if (useSqlServer) options.UseSqlServer(connectionString);
    else options.UseSqlite(connectionString);
});

// File storage: local folder by default. Set Storage:Provider = S3 (plus AWS:BucketName / AWS:Region)
// to store attachments in Amazon S3. No code changes, only config.
builder.Services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
builder.Services.Configure<AwsS3Options>(config.GetSection(AwsS3Options.Section));
if (string.Equals(config["Storage:Provider"], "S3", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(config["AWS:BucketName"]))
        throw new InvalidOperationException(
            "Storage:Provider is S3 but AWS:BucketName is not set. " +
            "Run: dotnet user-secrets set \"AWS:BucketName\" \"your-bucket\" --project JobDesk");

    builder.Services.AddDefaultAWSOptions(config.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
}
else
{
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
}

builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

var askOpts = config.GetSection(AskOptions.Section).Get<AskOptions>() ?? new AskOptions();
// Env overrides: Ask__UseBedrock, Ask__ModelId, Ask__Region (or BEDROCK_MODEL_ID)
if (bool.TryParse(Environment.GetEnvironmentVariable("Ask__UseBedrock"), out var useBr))
    askOpts.UseBedrock = useBr;
else if (bool.TryParse(Environment.GetEnvironmentVariable("USE_BEDROCK"), out var useBr2))
    askOpts.UseBedrock = useBr2;
var modelEnv = Environment.GetEnvironmentVariable("Ask__ModelId")
    ?? Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID");
if (!string.IsNullOrWhiteSpace(modelEnv)) askOpts.ModelId = modelEnv!;
var regionEnv = Environment.GetEnvironmentVariable("Ask__Region")
    ?? Environment.GetEnvironmentVariable("AWS_REGION")
    ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
if (!string.IsNullOrWhiteSpace(regionEnv)) askOpts.Region = regionEnv!;
builder.Services.AddSingleton(askOpts);

var dbxOpts = config.GetSection(DatabricksOptions.Section).Get<DatabricksOptions>() ?? new DatabricksOptions();
// Tokens only from env / configuration keys — never from checked-in JSON.
dbxOpts.AccessToken = DatabricksPatLoader.ResolveFromEnvironmentAndConfiguration(config);
if (bool.TryParse(Environment.GetEnvironmentVariable("Databricks__Enabled"), out var dbxOn))
    dbxOpts.Enabled = dbxOn;
if (bool.TryParse(Environment.GetEnvironmentVariable("Ask__UseDatabricks"), out var askDbxOn))
    dbxOpts.Enabled = askDbxOn;
var dbxHost = Environment.GetEnvironmentVariable("Databricks__Host");
if (!string.IsNullOrWhiteSpace(dbxHost)) dbxOpts.Host = dbxHost.Trim();
var dbxWh = Environment.GetEnvironmentVariable("Databricks__WarehouseId");
if (!string.IsNullOrWhiteSpace(dbxWh)) dbxOpts.WarehouseId = dbxWh.Trim();
var dbxCat = Environment.GetEnvironmentVariable("Databricks__Catalog");
if (!string.IsNullOrWhiteSpace(dbxCat)) dbxOpts.Catalog = dbxCat.Trim();
var dbxSchema = Environment.GetEnvironmentVariable("Databricks__Schema");
if (!string.IsNullOrWhiteSpace(dbxSchema)) dbxOpts.Schema = dbxSchema.Trim();
var dbxAsOf = Environment.GetEnvironmentVariable("Databricks__AsOfDate");
if (!string.IsNullOrWhiteSpace(dbxAsOf)) dbxOpts.AsOfDate = dbxAsOf.Trim();
var dbxAuth = Environment.GetEnvironmentVariable("Databricks__AuthMode");
if (!string.IsNullOrWhiteSpace(dbxAuth)
    && Enum.TryParse<DatabricksAuthMode>(dbxAuth, ignoreCase: true, out var parsedAuth))
    dbxOpts.AuthMode = parsedAuth;
builder.Services.AddSingleton(dbxOpts);

builder.Services.AddHttpClient<IDatabricksStatementClient, DatabricksStatementClient>((sp, client) =>
{
    var o = sp.GetRequiredService<DatabricksOptions>();
    if (!string.IsNullOrWhiteSpace(o.Host))
    {
        var host = o.Host.Trim().TrimEnd('/');
        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host[8..];
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host[7..];
        client.BaseAddress = new Uri($"https://{host}/api/2.0/");
    }
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.StatementTimeoutSeconds + 10, 15, 55));
});

builder.Services.AddScoped<IAskService, AskService>();

// Behind a proxy (Lambda Function URL, a load balancer, a container host) the original
// scheme and client IP arrive in X-Forwarded-* headers. Only trusted when enabled in config.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.Logger.LogInformation(demo.ReadOnly
    ? "Public site: writes blocked (Changes unavailable). Ask allowed."
    : "Local practice mode: create, edit, upload, and delete are enabled.");
if (dbxOpts.Enabled)
{
    var pat = DatabricksPatLoader.Normalize(dbxOpts.AccessToken);
    if (dbxOpts.IsConfigured)
    {
        app.Logger.LogInformation(
            "Ask Databricks: enabled host={Host} warehouse={WarehouseId} catalog={Catalog} tokenLength={TokenLength}",
            DatabricksApiUrls.NormalizeHost(dbxOpts.Host),
            dbxOpts.WarehouseId.Trim(),
            dbxOpts.Catalog.Trim(),
            pat!.Length);
    }
    else
        app.Logger.LogInformation(
            "Ask Databricks: enabled but not configured (host/warehouse/catalog/token). Host={Host} tokenLength={TokenLength}",
            string.IsNullOrWhiteSpace(dbxOpts.Host) ? "(missing)" : DatabricksApiUrls.TryNormalizeHost(dbxOpts.Host),
            pat?.Length ?? 0);
}

// Creates the database on first run and fills it with practice data.
// Never deletes or alters an existing database.
using (var scope = app.Services.CreateScope())
{
    SeedData.Initialize(
        scope.ServiceProvider.GetRequiredService<JobDeskContext>(),
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

if (config.GetValue<bool>("Hosting:ForwardedHeaders"))
    app.UseForwardedHeaders();

if (config.GetValue<bool>("Hosting:DetailedErrors"))
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

if (demo.ReadOnly)
    app.UseMiddleware<ReadOnlyDemoMiddleware>();

// Function URLs are HTTPS-only already, so the public demo turns this off to avoid redirect loops.
if (config.GetValue("Hosting:HttpsRedirection", true))
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using Amazon.S3;
using JobDesk.Data;
using JobDesk.Hosting;
using JobDesk.Services;
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
    ? "PUBLIC DEMO MODE: read-only. All POST/PUT/PATCH/DELETE requests are refused."
    : "Local practice mode: create, edit, upload, and delete are enabled.");

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

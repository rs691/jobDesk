namespace JobDesk.Hosting;

// Bound from the "Demo" config section. Off by default, so local practice runs
// keep full create/edit/upload. The PublicDemo environment turns it on.
public class DemoOptions
{
    public const string Section = "Demo";

    public bool ReadOnly { get; set; }

    // Optional "prepared for" line on the home page. Empty hides it.
    public string PreparedFor { get; set; } = string.Empty;
}

public class ReadOnlyDemoException : InvalidOperationException
{
    public ReadOnlyDemoException() : base("Changes aren’t available on this site.") { }
}

// Blocks every request that could change data (POST, PUT, PATCH, DELETE, ...)
// before it reaches routing, so no controller action can be reached by a crafted request.
// Services also refuse writes (see JobService / AttachmentService) as a second layer.
public class ReadOnlyDemoMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    private readonly RequestDelegate _next;

    public ReadOnlyDemoMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Ask is Q&A only — does not change customers/work orders/files.
        var path = context.Request.Path.Value ?? "";
        if (path.Equals("/Ask", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Ask/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("""
            <!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>Changes unavailable</title>
            <meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="font-family:system-ui,sans-serif;max-width:560px;margin:4rem auto;padding:0 1rem">
            <h1>Changes unavailable</h1>
            <p>This JobDesk site doesn’t allow creating, editing, uploading, or deleting.</p>
            <p><a href="/">Back to JobDesk</a></p></body></html>
            """);
    }
}

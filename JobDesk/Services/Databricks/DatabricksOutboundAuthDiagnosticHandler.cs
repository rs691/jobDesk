namespace JobDesk.Services.Databricks;

/// <summary>
/// Logs whether Authorization is present on the outbound Databricks call (never logs the secret).
/// </summary>
internal sealed class DatabricksOutboundAuthDiagnosticHandler : DelegatingHandler
{
    private readonly ILogger<DatabricksOutboundAuthDiagnosticHandler> _log;

    public DatabricksOutboundAuthDiagnosticHandler(ILogger<DatabricksOutboundAuthDiagnosticHandler> log)
    {
        _log = log;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (present, bearerPrefix, valueLength) = DescribeAuthorization(request);
        var method = request.Method.Method;
        var uri = request.RequestUri?.ToString() ?? "(null)";
        _log.LogInformation(
            "Databricks HTTP {Method} {Uri} outboundAuthorizationPresent={Present} bearerPrefix={BearerPrefix} authorizationValueLength={Length}",
            method,
            uri,
            present,
            bearerPrefix,
            valueLength);

        if (!present)
            _log.LogWarning("Databricks outbound request is missing an Authorization header");

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var www = response.Headers.WwwAuthenticate.Count > 0
                ? string.Join("; ", response.Headers.WwwAuthenticate.Select(h => h.ToString()))
                : "(none)";
            _log.LogWarning(
                "Databricks HTTP 401 for {Uri} outboundAuthorizationPresent={Present} authorizationValueLength={Length} wwwAuthenticate={WwwAuthenticate}",
                uri,
                present,
                valueLength,
                www);
        }

        return response;
    }

    internal static (bool Present, bool BearerPrefix, int ValueLength) DescribeAuthorization(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues("Authorization", out var values))
            return (false, false, 0);

        var line = values.FirstOrDefault();
        if (string.IsNullOrEmpty(line))
            return (false, false, 0);

        var bearer = line.StartsWith("Bearer ", StringComparison.Ordinal);
        return (true, bearer, line.Length);
    }
}

namespace JobDesk.Services.Databricks;

internal static class DatabricksRequestAuth
{
    /// <summary>
    /// Sets Bearer PAT without encoding surprises (some tokens fail <see cref="System.Net.Http.Headers.AuthenticationHeaderValue"/> parsing).
    /// </summary>
    public static void ApplyBearer(HttpRequestMessage request, string normalizedToken)
    {
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + normalizedToken);
    }
}

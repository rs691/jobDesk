using System.Net.Http.Headers;
using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

/// <summary>
/// Transport/auth construction on the outbound HttpRequestMessage (redirect disproven for this workspace).
/// </summary>
public class DatabricksAuthorizationTransportTests
{
    [Fact]
    public void ApplyBearer_sets_single_Authorization_header_try_add_without_validation()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://dbc.example/api/2.0/sql/statements/");
        DatabricksRequestAuth.ApplyBearer(req, "dapi-sample-token");

        Assert.True(req.Headers.TryGetValues("Authorization", out var values));
        Assert.Single(values!);
        Assert.Equal("Bearer dapi-sample-token", values!.First());
    }

    [Fact]
    public void Main_style_AuthenticationHeaderValue_also_exposes_Authorization()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://dbc.example/api/2.0/sql/statements/");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "dapi-sample-token");

        Assert.NotNull(req.Headers.Authorization);
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("dapi-sample-token", req.Headers.Authorization.Parameter);
    }

    [Fact]
    public void DescribeAuthorization_reports_presence_without_secret()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://example/");
        DatabricksRequestAuth.ApplyBearer(req, "dapi-abcdef");

        var (present, bearerPrefix, valueLength) = DatabricksOutboundAuthDiagnosticHandler.DescribeAuthorization(req);

        Assert.True(present);
        Assert.True(bearerPrefix);
        Assert.Equal("Bearer dapi-abcdef".Length, valueLength);
    }

    [Fact]
    public void Legacy_null_coalesce_keeps_empty_string_over_second_env_value()
    {
        const string? first = "";
        const string? second = "dapi-from-databricks-token";
        var legacy = first ?? second;
        Assert.Equal("", legacy);

        Assert.Equal("dapi-from-databricks-token", DatabricksPatLoader.Resolve(first, second));
    }
}

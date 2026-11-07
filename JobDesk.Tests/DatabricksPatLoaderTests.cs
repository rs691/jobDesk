using JobDesk.Services.Databricks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace JobDesk.Tests;

public class DatabricksPatLoaderTests
{
    [Fact]
    public void Resolve_skips_empty_first_candidate()
    {
        var t = DatabricksPatLoader.Resolve("", "  dapi-test  ");
        Assert.Equal("dapi-test", t);
    }

    [Fact]
    public void Resolve_prefers_first_non_empty_in_order()
    {
        Assert.Equal("from-env", DatabricksPatLoader.Resolve("from-env", "other"));
        Assert.Equal("second", DatabricksPatLoader.Resolve(null, "  second "));
    }

    [Fact]
    public void Normalize_strips_Bearer_prefix()
    {
        Assert.Equal("dapi-abc", DatabricksPatLoader.Normalize("Bearer dapi-abc"));
    }

    [Fact]
    public void Normalize_rejects_whitespace_only()
    {
        Assert.Null(DatabricksPatLoader.Normalize("  \n  "));
    }

    [Fact]
    public void ResolveFromConfiguration_reads_Databricks_AccessToken_key()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Databricks:AccessToken"] = "dapi-from-config"
            })
            .Build();
        Assert.Equal("dapi-from-config", DatabricksPatLoader.ResolveFromEnvironmentAndConfiguration(config));
    }
}

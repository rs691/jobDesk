using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

public class DatabricksAuthTests
{
    [Fact]
    public void IsConfigured_requires_pat_bearer_and_catalog()
    {
        var o = new DatabricksOptions
        {
            Enabled = true,
            AuthMode = DatabricksAuthMode.PatBearer,
            Host = "dbc-example.cloud.databricks.com",
            WarehouseId = "wh",
            Catalog = "main",
            AccessToken = "secret"
        };
        Assert.True(o.IsConfigured);
    }

    [Fact]
    public void IsConfigured_false_when_auth_mode_none()
    {
        var o = new DatabricksOptions
        {
            Enabled = true,
            AuthMode = DatabricksAuthMode.None,
            Host = "h",
            WarehouseId = "w",
            Catalog = "c",
            AccessToken = "t"
        };
        Assert.False(o.IsConfigured);
    }
}

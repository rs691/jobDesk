using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

public class DatabricksApiUrlsTests
{
    [Fact]
    public void StatementExecution_builds_post_path()
    {
        var uri = DatabricksApiUrls.StatementExecution(
            "dbc-dab8e394-216d.cloud.databricks.com",
            "sql/statements/");
        Assert.Equal("https://dbc-dab8e394-216d.cloud.databricks.com/api/2.0/sql/statements/", uri.ToString());
    }

    [Fact]
    public void StatementExecution_strips_https_prefix()
    {
        var uri = DatabricksApiUrls.StatementExecution(
            "https://dbc-dab8e394-216d.cloud.databricks.com/",
            "sql/statements/abc");
        Assert.Equal("https://dbc-dab8e394-216d.cloud.databricks.com/api/2.0/sql/statements/abc", uri.ToString());
    }
}

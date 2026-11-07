using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

public class AskAllowlistedSqlTests
{
    private static DatabricksOptions Opts() => new()
    {
        Catalog = "samples",
        Schema = "jobdesk_synthetic",
        AsOfDate = "2026-11-06"
    };

    [Fact]
    public void Every_intent_builds_select_only()
    {
        foreach (AskIntent intent in Enum.GetValues<AskIntent>())
        {
            var sql = AskAllowlistedSql.Build(intent, Opts());
            Assert.StartsWith("SELECT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(";", sql, StringComparison.Ordinal);
            Assert.Contains("samples.jobdesk_synthetic.", sql);
        }
    }

    [Fact]
    public void Rejects_invalid_catalog()
    {
        var bad = Opts();
        bad.Catalog = "evil;drop";
        Assert.Throws<ArgumentException>(() => AskAllowlistedSql.Build(AskIntent.WorkOrdersByStatus, bad));
    }

    [Fact]
    public void Rejects_non_select_in_validator()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AskAllowlistedSql.EnsureReadOnlySelect("DELETE FROM t"));
    }
}

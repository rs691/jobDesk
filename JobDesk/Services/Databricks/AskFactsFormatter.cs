using System.Text;

namespace JobDesk.Services.Databricks;

public static class AskFactsFormatter
{
    public static string FormatFacts(AskIntent intent, DatabricksQueryResult result, DatabricksOptions opts)
    {
        var title = AskIntentResolver.Title(intent);
        var sb = new StringBuilder();
        sb.AppendLine($"SYNTHETIC WAREHOUSE FACTS (Databricks, allowlisted read-only SQL, as-of {opts.AsOfDate})");
        sb.AppendLine($"Intent: {title}");
        sb.AppendLine($"Columns: {string.Join(", ", result.Columns)}");
        sb.AppendLine("Rows:");

        var max = Math.Clamp(opts.MaxRows, 1, 500);
        var count = 0;
        foreach (var row in result.Rows)
        {
            if (count >= max) break;
            sb.AppendLine("  " + string.Join(" | ", row.Select(v => v ?? "NULL")));
            count++;
        }

        if (result.Rows.Count > max)
            sb.AppendLine($"  … ({result.Rows.Count - max} more rows not shown, cap {max})");

        return sb.ToString();
    }

    public static string FormatDeterministic(AskIntent intent, DatabricksQueryResult result, DatabricksOptions opts)
    {
        var facts = FormatFacts(intent, result, opts);
        return facts + "\n(Data returned directly from Databricks; not paraphrased by a model.)";
    }
}

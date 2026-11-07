namespace JobDesk.Services.Databricks;

public static class AskIntentResolver
{
    private static readonly (AskIntent Intent, string Title, string[] Keywords)[] Catalog =
    [
        (AskIntent.WorkOrdersByStatus, "Work orders by status", ["work order", "orders", "status", "count"]),
        (AskIntent.OverdueOpenWorkByCrew, "Overdue open work by crew", ["overdue", "crew", "open work", "behind"]),
        (AskIntent.OutstandingInvoiceBalance, "Outstanding invoice balance", ["outstanding", "invoice", "balance", "owed", "ar"]),
        (AskIntent.OverdueInvoiceBalance, "Overdue invoice balance", ["overdue invoice", "past due", "late invoice"]),
        (AskIntent.TopCustomersByBilledRevenue, "Top five customers by billed revenue", ["top customer", "billed", "revenue", "top five"]),
        (AskIntent.MaterialsBelowReorderPoint, "Materials below reorder point", ["reorder", "stock", "below", "materials low"]),
        (AskIntent.MaterialReservationShortages, "Material reservation shortages", ["shortage", "reserved", "reservation"]),
        (AskIntent.BlockedJobsExplained, "Blocked jobs explained", ["blocked", "blocked job", "why blocked"]),
        (AskIntent.NextSevenDaysCrewLoad, "Next seven days planned crew load", ["next seven", "7 day", "week", "crew load", "planned load"]),
        (AskIntent.CompletedHoursVsEstimateByService, "Completed hours versus estimate by service", ["completed hours", "estimate", "actual hours", "service type"])
    ];

    public static AskIntent? Resolve(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;
        var q = question.ToLowerInvariant();

        AskIntent? best = null;
        var bestScore = 0;
        foreach (var entry in Catalog)
        {
            var score = 0;
            foreach (var kw in entry.Keywords)
            {
                if (q.Contains(kw, StringComparison.Ordinal))
                    score += kw.Contains(' ') ? 3 : 1;
            }
            if (entry.Title.Equals(question.Trim(), StringComparison.OrdinalIgnoreCase))
                score += 10;
            if (score > bestScore)
            {
                bestScore = score;
                best = entry.Intent;
            }
        }

        return bestScore >= 2 ? best : null;
    }

    public static string Title(AskIntent intent) =>
        Catalog.First(e => e.Intent == intent).Title;

    public static string HelpText()
    {
        var lines = Catalog.Select(c => $"• {c.Title}");
        return "Ask about the synthetic warehouse using one of these allowlisted questions (data as-of 2026-11-06):\n"
               + string.Join("\n", lines)
               + "\n\nYour question did not match an allowlisted query. No arbitrary SQL is run.";
    }

    public static IReadOnlyList<string> ExampleTitles() =>
        Catalog.Select(c => c.Title).ToList();
}

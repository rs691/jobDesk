using System.Text.RegularExpressions;

namespace JobDesk.Services.Databricks;

/// <summary>Builds fixed SELECT statements only — user text never becomes SQL.</summary>
public static class AskAllowlistedSql
{
    private static readonly Regex Forbidden = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|CREATE|ALTER|TRUNCATE|GRANT|REVOKE|COPY|PUT|EXEC)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Build(AskIntent intent, DatabricksOptions opts)
    {
        DatabricksOptions.ValidateIdentifier(opts.Catalog, nameof(opts.Catalog));
        DatabricksOptions.ValidateIdentifier(opts.Schema, nameof(opts.Schema));
        DatabricksOptions.ValidateAsOfDate(opts.AsOfDate);

        var t = $"{opts.Catalog}.{opts.Schema}";
        var asOf = opts.AsOfDate;
        var windowEnd = DateOnly.Parse(asOf, System.Globalization.CultureInfo.InvariantCulture).AddDays(6)
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var sql = intent switch
        {
            AskIntent.WorkOrdersByStatus =>
                $"SELECT status, COUNT(*) AS orders FROM {t}.work_orders GROUP BY status ORDER BY status",
            AskIntent.OverdueOpenWorkByCrew =>
                $"SELECT crew_id, COUNT(*) AS overdue_orders, ROUND(SUM(estimated_hours),2) AS estimated_hours FROM {t}.work_orders WHERE status IN ('scheduled','in_progress','blocked') AND scheduled_date < DATE '{asOf}' GROUP BY crew_id ORDER BY overdue_orders DESC, crew_id",
            AskIntent.OutstandingInvoiceBalance =>
                $"SELECT ROUND(SUM(i.total_amount-COALESCE(p.paid,0)),2) AS outstanding FROM {t}.invoices i LEFT JOIN (SELECT invoice_id,SUM(amount) paid FROM {t}.payments GROUP BY invoice_id) p ON i.invoice_id=p.invoice_id",
            AskIntent.OverdueInvoiceBalance =>
                $"SELECT COUNT(*) AS overdue_invoices,ROUND(SUM(i.total_amount-COALESCE(p.paid,0)),2) AS overdue_balance FROM {t}.invoices i LEFT JOIN (SELECT invoice_id,SUM(amount) paid FROM {t}.payments GROUP BY invoice_id) p ON i.invoice_id=p.invoice_id WHERE i.status='overdue'",
            AskIntent.TopCustomersByBilledRevenue =>
                $"SELECT c.customer_id,c.customer_name,ROUND(SUM(i.total_amount),2) AS billed FROM {t}.customers c JOIN {t}.work_orders w ON c.customer_id=w.customer_id JOIN {t}.invoices i ON w.work_order_id=i.work_order_id GROUP BY c.customer_id,c.customer_name ORDER BY billed DESC,c.customer_id LIMIT 5",
            AskIntent.MaterialsBelowReorderPoint =>
                $"SELECT material_id,stock_on_hand,reorder_point FROM {t}.materials WHERE stock_on_hand < reorder_point ORDER BY material_id",
            AskIntent.MaterialReservationShortages =>
                $"SELECT m.material_id,m.stock_on_hand,SUM(u.quantity) AS reserved_quantity,SUM(u.quantity)-m.stock_on_hand AS shortage FROM {t}.materials m JOIN {t}.material_usage u ON m.material_id=u.material_id WHERE u.usage_type='reserved' GROUP BY m.material_id,m.stock_on_hand HAVING SUM(u.quantity)>m.stock_on_hand ORDER BY shortage DESC,m.material_id LIMIT 10",
            AskIntent.BlockedJobsExplained =>
                $"SELECT w.work_order_id,w.crew_id,n.note_text FROM {t}.work_orders w JOIN {t}.activity_notes n ON w.work_order_id=n.work_order_id WHERE w.status='blocked' AND n.note_type='status' ORDER BY w.work_order_id LIMIT 5",
            AskIntent.NextSevenDaysCrewLoad =>
                $"SELECT crew_id,COUNT(*) AS orders,ROUND(SUM(estimated_hours),2) AS hours FROM {t}.work_orders WHERE status IN ('scheduled','in_progress','blocked') AND scheduled_date BETWEEN DATE '{asOf}' AND DATE '{windowEnd}' GROUP BY crew_id ORDER BY hours DESC, crew_id",
            AskIntent.CompletedHoursVsEstimateByService =>
                $"SELECT service_type,COUNT(*) AS orders,ROUND(SUM(estimated_hours),2) AS estimated,ROUND(SUM(actual_hours),2) AS actual FROM {t}.work_orders WHERE status='completed' GROUP BY service_type ORDER BY service_type",
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
        };

        EnsureReadOnlySelect(sql);
        return sql;
    }

    public static void EnsureReadOnlySelect(string sql)
    {
        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only SELECT statements are allowed.");
        if (trimmed.Contains(';', StringComparison.Ordinal))
            throw new InvalidOperationException("Multi-statement SQL is not allowed.");
        if (Forbidden.IsMatch(sql))
            throw new InvalidOperationException("Disallowed SQL keyword in allowlisted query.");
    }
}

namespace JobDesk.Services.Databricks;

/// <summary>Allowlisted Ask intents aligned with sql/synthetic/example_queries.sql (as-of 2026-11-06).</summary>
public enum AskIntent
{
    WorkOrdersByStatus,
    OverdueOpenWorkByCrew,
    OutstandingInvoiceBalance,
    OverdueInvoiceBalance,
    TopCustomersByBilledRevenue,
    MaterialsBelowReorderPoint,
    MaterialReservationShortages,
    BlockedJobsExplained,
    NextSevenDaysCrewLoad,
    CompletedHoursVsEstimateByService
}

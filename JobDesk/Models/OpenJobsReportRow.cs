namespace JobDesk.Models;

// Not EF entities: populated by raw SQL / stored procedures in JobService.
// Property names must match the column aliases in the SQL.
public class OpenJobsReportRow
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int OpenWorkOrders { get; set; }
    public double MaterialCost { get; set; }
}

public class CustomerJobsRow
{
    public int WorkOrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledDate { get; set; }
    public int MaterialLines { get; set; }
    public double MaterialCost { get; set; }
}

// What the Reports page shows about how a result set was produced.
public class QueryInfo
{
    public string Provider { get; set; } = string.Empty;
    public string ProcName { get; set; } = string.Empty;
    public string Returns { get; set; } = string.Empty;
    public bool CalledStoredProcedure { get; set; }
    public string ExecutedSql { get; set; } = string.Empty;
    public string ProcDefinition { get; set; } = string.Empty;
}

public class OpenJobsReportViewModel
{
    public WorkOrderStatus Status { get; set; }
    public bool ShowBuggyVersion { get; set; }
    public List<OpenJobsReportRow> Rows { get; set; } = new();
    public QueryInfo Query { get; set; } = new();

    public int TotalWorkOrders => Rows.Sum(r => r.OpenWorkOrders);
    public double TotalMaterialCost => Rows.Sum(r => r.MaterialCost);
}

public class CustomerJobsViewModel
{
    public int CustomerId { get; set; }
    public List<CustomerJobsRow> Rows { get; set; } = new();
    public QueryInfo Query { get; set; } = new();
}

public class ReportsPageViewModel
{
    public OpenJobsReportViewModel OpenJobs { get; set; } = new();
    public CustomerJobsViewModel CustomerJobs { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
}

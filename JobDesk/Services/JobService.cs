using System.Data.Common;
using JobDesk.Data;
using JobDesk.Hosting;
using JobDesk.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobDesk.Services;

public class JobService : IJobService
{
    private readonly JobDeskContext _db;
    private readonly DemoOptions _demo;

    public JobService(JobDeskContext db, DemoOptions demo)
    {
        _db = db;
        _demo = demo;
    }

    private void EnsureWritable()
    {
        if (_demo.ReadOnly) throw new ReadOnlyDemoException();
    }

    // ---------- Customers ----------

    public Task<List<Customer>> GetCustomersAsync() =>
        _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    public Task<Customer?> GetCustomerAsync(int id) =>
        _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddCustomerAsync(Customer customer)
    {
        EnsureWritable();
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> UpdateCustomerAsync(Customer customer)
    {
        EnsureWritable();
        var existing = await _db.Customers.FindAsync(customer.Id);
        if (existing is null) return false;

        existing.Name = customer.Name;
        existing.Phone = customer.Phone;
        existing.Email = customer.Email;
        existing.City = customer.City;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---------- Work orders ----------

    public Task<List<WorkOrder>> GetWorkOrdersAsync(WorkOrderStatus? status)
    {
        var query = _db.WorkOrders.AsNoTracking().Include(w => w.Customer).AsQueryable();
        if (status.HasValue)
            query = query.Where(w => w.Status == status.Value);

        return query.OrderBy(w => w.ScheduledDate == null).ThenBy(w => w.ScheduledDate).ThenBy(w => w.Id).ToListAsync();
    }

    public Task<WorkOrder?> GetWorkOrderAsync(int id) =>
        _db.WorkOrders.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);

    public Task<WorkOrder?> GetWorkOrderDetailsAsync(int id) =>
        _db.WorkOrders.AsNoTracking()
            .Include(w => w.Customer)
            .Include(w => w.Materials).ThenInclude(m => m.Material)
            .Include(w => w.Attachments.OrderByDescending(a => a.UploadedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task AddWorkOrderAsync(WorkOrder workOrder)
    {
        EnsureWritable();
        workOrder.CreatedAt = DateTime.UtcNow;
        _db.WorkOrders.Add(workOrder);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> UpdateWorkOrderAsync(WorkOrder workOrder)
    {
        EnsureWritable();
        var existing = await _db.WorkOrders.FindAsync(workOrder.Id);
        if (existing is null) return false;

        existing.CustomerId = workOrder.CustomerId;
        existing.Title = workOrder.Title;
        existing.Status = workOrder.Status;
        existing.ScheduledDate = workOrder.ScheduledDate;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---------- Report: work orders + material cost per customer ----------

    // FIXED query (see BUG.md). One row per customer.
    // - Materials are pre-aggregated to ONE row per work order before joining,
    //   so line items cannot multiply the work-order count.
    // - LEFT JOINs keep customers with no matching work orders and work orders
    //   with no material lines yet (they show 0 instead of disappearing).
    // - The status filter lives in the ON clause, not WHERE. A WHERE on wo.Status
    //   would throw away the NULL rows and quietly turn the LEFT JOIN into an INNER JOIN.
    // - @status is a bound parameter, never string-concatenated.
    public const string OpenJobsSql = """
        SELECT c.Id                              AS CustomerId,
               c.Name                            AS CustomerName,
               COUNT(wo.Id)                      AS OpenWorkOrders,
               COALESCE(SUM(m.MaterialCost), 0)  AS MaterialCost
        FROM Customers c
        LEFT JOIN WorkOrders wo
               ON wo.CustomerId = c.Id
              AND wo.Status = @status
        LEFT JOIN (
            SELECT wom.WorkOrderId,
                   SUM(wom.Quantity * mat.UnitCost) AS MaterialCost
            FROM WorkOrderMaterials wom
            INNER JOIN Materials mat ON mat.Id = wom.MaterialId
            GROUP BY wom.WorkOrderId
        ) m ON m.WorkOrderId = wo.Id
        GROUP BY c.Id, c.Name
        ORDER BY MaterialCost DESC, c.Name
        """;

    // ORIGINAL (buggy) query, kept only so the bug can be reproduced on the Reports page.
    // INNER JOIN to material lines drops work orders with no materials, and
    // COUNT(*) counts material LINES instead of work orders (wrong grain).
    public const string OpenJobsSqlBuggy = """
        SELECT c.Id                              AS CustomerId,
               c.Name                            AS CustomerName,
               COUNT(*)                          AS OpenWorkOrders,
               SUM(wom.Quantity * mat.UnitCost)  AS MaterialCost
        FROM Customers c
        INNER JOIN WorkOrders wo          ON wo.CustomerId = c.Id
        INNER JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
        INNER JOIN Materials mat          ON mat.Id = wom.MaterialId
        WHERE wo.Status = @status
        GROUP BY c.Id, c.Name
        ORDER BY MaterialCost DESC, c.Name
        """;

    public const string OpenJobsProc = "dbo.usp_OpenJobsByCustomer";

    public async Task<OpenJobsReportViewModel> GetOpenJobsReportAsync(WorkOrderStatus status, bool useBuggyQuery = false)
    {
        // SQL Server: call the stored procedure from sql/stored-procedures.sql.
        // SQLite has no procs, so run the identical SELECT inline.
        var callProc = _db.Database.IsSqlServer() && !useBuggyQuery;
        var sql = callProc
            ? $"EXEC {OpenJobsProc} @Status = @status"
            : useBuggyQuery ? OpenJobsSqlBuggy : OpenJobsSql;

        var rows = await _db.Database
            .SqlQueryRaw<OpenJobsReportRow>(sql, Param("@status", status.ToString()))
            .ToListAsync();

        return new OpenJobsReportViewModel
        {
            Status = status,
            ShowBuggyVersion = useBuggyQuery,
            Rows = rows,
            Query = BuildQueryInfo(OpenJobsProc, "One row per customer: CustomerId, CustomerName, OpenWorkOrders, MaterialCost", callProc, sql)
        };
    }

    // ---------- Report: every work order for one customer ----------

    public const string WorkOrdersByCustomerProc = "dbo.usp_WorkOrdersByCustomer";

    // SQLite copy of dbo.usp_WorkOrdersByCustomer. Keep the two in sync.
    public const string WorkOrdersByCustomerSql = """
        SELECT wo.Id                                         AS WorkOrderId,
               wo.Title                                      AS Title,
               wo.Status                                     AS Status,
               wo.ScheduledDate                              AS ScheduledDate,
               COUNT(wom.Id)                                 AS MaterialLines,
               COALESCE(SUM(wom.Quantity * mat.UnitCost), 0) AS MaterialCost
        FROM WorkOrders wo
        LEFT JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
        LEFT JOIN Materials mat          ON mat.Id = wom.MaterialId
        WHERE wo.CustomerId = @customerId
        GROUP BY wo.Id, wo.Title, wo.Status, wo.ScheduledDate
        ORDER BY wo.ScheduledDate, wo.Id
        """;

    public async Task<CustomerJobsViewModel> GetWorkOrdersByCustomerAsync(int customerId)
    {
        var callProc = _db.Database.IsSqlServer();
        var sql = callProc
            ? $"EXEC {WorkOrdersByCustomerProc} @CustomerId = @customerId"
            : WorkOrdersByCustomerSql;

        var rows = await _db.Database
            .SqlQueryRaw<CustomerJobsRow>(sql, Param("@customerId", customerId))
            .ToListAsync();

        return new CustomerJobsViewModel
        {
            CustomerId = customerId,
            Rows = rows,
            Query = BuildQueryInfo(WorkOrdersByCustomerProc, "One row per work order: WorkOrderId, Title, Status, ScheduledDate, MaterialLines, MaterialCost", callProc, sql)
        };
    }

    // ---------- Normalization lesson: the "spreadsheet" view of the same data ----------

    public async Task<NormalizationViewModel> GetNormalizationLessonAsync() => new()
    {
        FlatLines = await GetFlatWorkOrderLinesAsync(),
        Customers = await _db.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync(),
        WorkOrders = await _db.WorkOrders.AsNoTracking().OrderBy(w => w.Id).ToListAsync(),
        Materials = await _db.Materials.AsNoTracking().OrderBy(m => m.Id).ToListAsync(),
        Lines = await _db.WorkOrderMaterials.AsNoTracking().OrderBy(l => l.WorkOrderId).ThenBy(l => l.Id).ToListAsync()
    };

    // Joins the normalized tables back into one flat row per material line,
    // which is what a denormalized WorkOrderSheet table would have to store.
    private Task<List<FlatWorkOrderLine>> GetFlatWorkOrderLinesAsync() =>
        (from wo in _db.WorkOrders
         join c in _db.Customers on wo.CustomerId equals c.Id
         from line in wo.Materials.DefaultIfEmpty()
         orderby wo.Id
         select new FlatWorkOrderLine
         {
             WorkOrderId = wo.Id,
             WorkOrderTitle = wo.Title,
             Status = wo.Status,
             CustomerName = c.Name,
             CustomerPhone = c.Phone,
             CustomerCity = c.City,
             MaterialName = line == null ? null : line.Material!.Name,
             UnitCost = line == null ? null : line.Material!.UnitCost,
             Quantity = line == null ? null : line.Quantity
         }).AsNoTracking().ToListAsync();

    private DbParameter Param(string name, object value) =>
        _db.Database.IsSqlServer() ? new SqlParameter(name, value) : new SqliteParameter(name, value);

    private string ProviderName => _db.Database.IsSqlServer() ? "SQL Server" : "SQLite";

    private QueryInfo BuildQueryInfo(string procName, string returns, bool calledProc, string executedSql) => new()
    {
        Provider = ProviderName,
        ProcName = procName,
        Returns = returns,
        CalledStoredProcedure = calledProc,
        ExecutedSql = executedSql,
        ProcDefinition = StoredProcedureScripts.Get(procName)
    };
}

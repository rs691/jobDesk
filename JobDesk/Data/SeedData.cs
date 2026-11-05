using JobDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace JobDesk.Data;

// Practice data only. Shaped on purpose so the report bug is visible:
// - some open work orders have NO material lines yet (INNER JOIN drops them)
// - some open work orders have SEVERAL material lines (COUNT(*) over lines over-counts)
// - one customer has no work orders at all (only a LEFT JOIN from Customers shows them)
public static class SeedData
{
    // EnsureCreated creates a missing database but never alters an existing one.
    // If an older JobDesk database is found, stop with instructions instead of
    // touching it. A real app would use EF Core migrations here.
    public static void Initialize(JobDeskContext db, ILogger logger)
    {
        db.Database.EnsureCreated();
        EnsureSchemaIsCurrent(db);
        logger.LogInformation("Database ready: {Source}", db.Database.GetDbConnection().DataSource);

        // SQL Server has real stored procedures: install them on every start.
        // CREATE OR ALTER makes this safe to repeat.
        if (db.Database.IsSqlServer())
        {
            foreach (var batch in StoredProcedureScripts.CreateBatches())
                db.Database.ExecuteSqlRaw(batch);
        }

        if (db.Customers.Any()) return;

        var maple = new Customer { Name = "Maple Street HOA", City = "Springfield", Phone = "555-0101", Email = "board@maplehoa.example" };
        var riverside = new Customer { Name = "Riverside Elementary", City = "Riverton", Phone = "555-0142" };
        var greenfield = new Customer { Name = "Greenfield Storage", City = "Springfield", Email = "ops@greenfield.example" };
        var oakRidge = new Customer { Name = "Oak Ridge Church", City = "Oak Ridge" };
        db.Customers.AddRange(maple, riverside, greenfield, oakRidge);

        var picket = new Material { Name = "Wood picket 6 ft", Unit = "each", UnitCost = 3.25m };
        var post = new Material { Name = "4x4 post 8 ft", Unit = "each", UnitCost = 12.50m };
        var concrete = new Material { Name = "Concrete mix 50 lb", Unit = "bag", UnitCost = 6.75m };
        var chainLink = new Material { Name = "Chain link 50 ft", Unit = "roll", UnitCost = 89.00m };
        var gateKit = new Material { Name = "Gate hardware kit", Unit = "kit", UnitCost = 42.00m };
        db.Materials.AddRange(picket, post, concrete, chainLink, gateKit);

        var today = DateTime.UtcNow.Date;

        db.WorkOrders.AddRange(
            new WorkOrder
            {
                Customer = maple, Title = "Replace 120 ft privacy fence", Status = WorkOrderStatus.Open,
                ScheduledDate = today.AddDays(7),
                Materials =
                {
                    new WorkOrderMaterial { Material = picket, Quantity = 200 },
                    new WorkOrderMaterial { Material = post, Quantity = 16 },
                    new WorkOrderMaterial { Material = concrete, Quantity = 32 }
                }
            },
            new WorkOrder { Customer = maple, Title = "Gate latch repair", Status = WorkOrderStatus.Open },
            new WorkOrder
            {
                Customer = riverside, Title = "Chain link along playground", Status = WorkOrderStatus.Open,
                ScheduledDate = today.AddDays(3),
                Materials =
                {
                    new WorkOrderMaterial { Material = chainLink, Quantity = 3 },
                    new WorkOrderMaterial { Material = post, Quantity = 10 },
                    new WorkOrderMaterial { Material = concrete, Quantity = 20 }
                }
            },
            new WorkOrder
            {
                Customer = riverside, Title = "Damaged panel swap", Status = WorkOrderStatus.Completed,
                ScheduledDate = today.AddDays(-10),
                Materials = { new WorkOrderMaterial { Material = picket, Quantity = 20 } }
            },
            new WorkOrder { Customer = greenfield, Title = "Security fence site walk", Status = WorkOrderStatus.Open },
            new WorkOrder
            {
                Customer = greenfield, Title = "Rolling gate install", Status = WorkOrderStatus.Scheduled,
                ScheduledDate = today.AddDays(14),
                Materials =
                {
                    new WorkOrderMaterial { Material = gateKit, Quantity = 1 },
                    new WorkOrderMaterial { Material = post, Quantity = 2 }
                }
            });

        db.SaveChanges();
    }

    private static void EnsureSchemaIsCurrent(JobDeskContext db)
    {
        var expected = db.Model.GetEntityTypes().Select(t => t.GetTableName()).OfType<string>();
        var existing = QueryStrings(db, db.Database.IsSqlServer()
            ? "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"
            : "SELECT name FROM sqlite_master WHERE type = 'table'");

        var missing = expected.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0) return;

        var source = db.Database.GetDbConnection().DataSource;
        throw new InvalidOperationException(
            $"The database at '{source}' was created by an older version of JobDesk and is missing: {string.Join(", ", missing)}. " +
            "It was NOT modified. To continue: (1) back it up, e.g. copy jobdesk.db to jobdesk.backup.db; " +
            "(2) then either delete the original so a fresh one is seeded, or point ConnectionStrings:JobDesk at a new file or database.");
    }

    private static List<string> QueryStrings(JobDeskContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var results = new List<string>();
            while (reader.Read()) results.Add(reader.GetString(0));
            return results;
        }
        finally
        {
            connection.Close();
        }
    }
}

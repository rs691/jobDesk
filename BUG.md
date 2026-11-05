# BUG: Open-jobs report over-counts work orders and drops customers

**Where:** Reports page → `JobService.GetOpenJobsReportAsync` (`JobDesk/Services/JobService.cs`)
**Status:** Fixed. The original query is kept as `OpenJobsSqlBuggy` so it can be reproduced.
The fixed query is also the stored procedure `dbo.usp_OpenJobsByCustomer` in `sql/stored-procedures.sql`, which the app calls on SQL Server.

## Symptom

With the seed data and status = **Open**:

| Customer | Report said (buggy) | Actually (Work orders page) |
|---|---|---|
| Maple Street HOA | 3 open | 2 open |
| Riverside Elementary | 3 open | 1 open |
| Greenfield Storage | *missing* | 1 open |
| Oak Ridge Church | *missing* | 0 open |
| **Total** | **6** | **4** |

The material cost totals looked right, which is why the bug was easy to miss.

## Reproduce

1. Run the app, open **Reports**, turn on **Original (buggy) query** (or go to `/Reports?buggy=true`).
2. Open **Work orders**, filter to **Open**, and count: 4 rows, not 6.
3. Create a new Open work order for any customer. It shows up in the fixed report but never in the buggy one, because it has no material lines.

## Root cause: wrong join grain + INNER JOIN

```sql
FROM Customers c
INNER JOIN WorkOrders wo          ON wo.CustomerId = c.Id
INNER JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id   -- one row per material LINE
INNER JOIN Materials mat          ON mat.Id = wom.MaterialId
WHERE wo.Status = @status
GROUP BY c.Id, c.Name
-- SELECT ... COUNT(*) AS OpenWorkOrders
```

1. **Grain:** joining to `WorkOrderMaterials` turns the result into one row per material line. `COUNT(*)` then counts lines (Maple's fence job has 3 lines, so it counted as 3 jobs).
2. **INNER JOIN drops rows:** a work order with no material lines yet has no match in `WorkOrderMaterials`, so it vanishes. Same for a customer with no work orders.

## Fix

```sql
FROM Customers c
LEFT JOIN WorkOrders wo
       ON wo.CustomerId = c.Id
      AND wo.Status = @status          -- filter in ON, not WHERE, or LEFT becomes INNER
LEFT JOIN (
    SELECT wom.WorkOrderId, SUM(wom.Quantity * mat.UnitCost) AS MaterialCost
    FROM WorkOrderMaterials wom
    INNER JOIN Materials mat ON mat.Id = wom.MaterialId
    GROUP BY wom.WorkOrderId           -- back to one row per work order
) m ON m.WorkOrderId = wo.Id
GROUP BY c.Id, c.Name
-- SELECT ... COUNT(wo.Id) AS OpenWorkOrders, COALESCE(SUM(m.MaterialCost), 0) AS MaterialCost
```

- Pre-aggregate materials to **one row per work order** before joining, so the count is at work-order grain.
- **LEFT JOIN** so customers and work orders without matches still appear (with 0).
- `COUNT(wo.Id)` instead of `COUNT(*)`, because `COUNT(*)` would count the NULL row a LEFT JOIN produces for a customer with no work orders as 1.
- The status filter goes in the `ON` clause. Putting `wo.Status = @status` in `WHERE` would filter out the NULL rows and quietly undo the LEFT JOIN.

## Verify

Fixed report (`/Reports`): Maple 2, Riverside 1, Greenfield 1, Oak Ridge 0, total **4**, which matches `/WorkOrders?status=Open`. Material cost total is unchanged at **$1,593.00**.
`sql/demo-joins.sql` shows each step (row counts per table, INNER vs LEFT, `COUNT(*)` vs `COUNT(DISTINCT)`, `WHERE` vs `ON`).

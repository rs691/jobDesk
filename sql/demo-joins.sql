-- INNER vs LEFT JOIN on the JobDesk schema.
-- Run against the app's database after one F5 / dotnet run:
--   sqlite3 JobDesk/jobdesk.db < sql/demo-joins.sql
-- (or open jobdesk.db in DB Browser for SQLite / VS Code SQLite extension)

.headers on
.mode column

-- 1) Grain check: how many rows per table?
SELECT 'Customers' AS tbl, COUNT(*) AS row_count FROM Customers
UNION ALL SELECT 'WorkOrders', COUNT(*) FROM WorkOrders
UNION ALL SELECT 'WorkOrderMaterials', COUNT(*) FROM WorkOrderMaterials;

-- 2) INNER JOIN: only work orders that HAVE material lines.
--    Open work orders with nothing picked yet disappear.
SELECT wo.Id, wo.Title, wo.Status, wom.MaterialId, wom.Quantity
FROM WorkOrders wo
INNER JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
WHERE wo.Status = 'Open'
ORDER BY wo.Id;

-- 3) LEFT JOIN: every open work order; material columns are NULL when there are none.
SELECT wo.Id, wo.Title, wo.Status, wom.MaterialId, wom.Quantity
FROM WorkOrders wo
LEFT JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
WHERE wo.Status = 'Open'
ORDER BY wo.Id;

-- 4) Join grain trap: after joining to line items there is one row per LINE,
--    so COUNT(*) counts lines, not work orders.
SELECT COUNT(*)              AS rows_after_join,
       COUNT(DISTINCT wo.Id) AS distinct_work_orders
FROM WorkOrders wo
LEFT JOIN WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
WHERE wo.Status = 'Open';

-- 5) WHERE vs ON with a LEFT JOIN.
--    Filter in WHERE: customers with no open work orders are removed (acts like INNER).
SELECT c.Name, COUNT(wo.Id) AS open_work_orders
FROM Customers c
LEFT JOIN WorkOrders wo ON wo.CustomerId = c.Id
WHERE wo.Status = 'Open'
GROUP BY c.Id, c.Name;

--    Filter in ON: every customer stays, zero when nothing matches.
SELECT c.Name, COUNT(wo.Id) AS open_work_orders
FROM Customers c
LEFT JOIN WorkOrders wo ON wo.CustomerId = c.Id AND wo.Status = 'Open'
GROUP BY c.Id, c.Name;

-- JobDesk stored procedures (SQL Server / Azure SQL / Amazon RDS for SQL Server).
--
-- SQLite has no stored procedures, so local F5 runs execute the same SELECT
-- logic inline from C# (see JobService). When Database:Provider = SqlServer,
-- JobService calls these procs with EXEC instead.
--
-- Install once, after the app has created the tables:
--   sqlcmd -S localhost -d JobDesk -E -i sql/stored-procedures.sql
-- or open this file in SSMS / Azure Data Studio and run it.

-- =============================================================
-- dbo.usp_OpenJobsByCustomer
-- One row per customer: how many work orders are in @Status and
-- their total material cost. This is the fixed query from BUG.md.
-- =============================================================
CREATE OR ALTER PROCEDURE dbo.usp_OpenJobsByCustomer
    @Status NVARCHAR(20) = N'Open'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.Id                              AS CustomerId,
           c.Name                            AS CustomerName,
           COUNT(wo.Id)                      AS OpenWorkOrders,
           COALESCE(SUM(m.MaterialCost), 0)  AS MaterialCost
    FROM dbo.Customers c
    LEFT JOIN dbo.WorkOrders wo
           ON wo.CustomerId = c.Id
          AND wo.Status = @Status
    LEFT JOIN (
        SELECT wom.WorkOrderId,
               SUM(wom.Quantity * mat.UnitCost) AS MaterialCost
        FROM dbo.WorkOrderMaterials wom
        INNER JOIN dbo.Materials mat ON mat.Id = wom.MaterialId
        GROUP BY wom.WorkOrderId
    ) m ON m.WorkOrderId = wo.Id
    GROUP BY c.Id, c.Name
    ORDER BY MaterialCost DESC, c.Name;
END
GO

-- =============================================================
-- dbo.usp_WorkOrdersByCustomer
-- Every work order for one customer, with material line count and cost.
-- LEFT JOIN so work orders with no materials yet still appear.
-- =============================================================
CREATE OR ALTER PROCEDURE dbo.usp_WorkOrdersByCustomer
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT wo.Id                                        AS WorkOrderId,
           wo.Title                                     AS Title,
           wo.Status                                    AS Status,
           wo.ScheduledDate                             AS ScheduledDate,
           COUNT(wom.Id)                                AS MaterialLines,
           COALESCE(SUM(wom.Quantity * mat.UnitCost), 0) AS MaterialCost
    FROM dbo.WorkOrders wo
    LEFT JOIN dbo.WorkOrderMaterials wom ON wom.WorkOrderId = wo.Id
    LEFT JOIN dbo.Materials mat          ON mat.Id = wom.MaterialId
    WHERE wo.CustomerId = @CustomerId
    GROUP BY wo.Id, wo.Title, wo.Status, wo.ScheduledDate
    ORDER BY wo.ScheduledDate, wo.Id;
END
GO

-- Try them:
-- EXEC dbo.usp_OpenJobsByCustomer @Status = N'Open';
-- EXEC dbo.usp_WorkOrdersByCustomer @CustomerId = 1;

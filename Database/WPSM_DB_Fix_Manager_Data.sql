/* WPSM_DB_Fix_Manager_Data.sql
   Repairs development seed data that made the Manager screens show wrong money and statuses.
   Safe to run more than once: each part only touches rows still in the broken state.

   1. Seeded sample projects (WPSM-2025-001/002/004/008/011/019) had every catalog item in baseline v1
      (17 lines, 561.95M) because the old seeder added each new catalog item on every start.
      The baseline is rebuilt as the core combo whose total equals BaseContractValue
      (same result as the fixed DevelopmentDatabaseSeeder.BuildSampleSpecification).
   2. WPSM-2025-002/008/019 were still "Active Baseline" with wedding dates in 2025.
      They move to January 2027 (Saturdays, free hall slots); every related date moves by the same offset.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

-- 1. Baseline specification of the seeded projects. ------------------------------------------------------------
DECLARE @Spec TABLE(Code NVARCHAR(30) PRIMARY KEY, HallPrice DECIMAL(18,2), Tables INT);
INSERT @Spec VALUES
(N'WPSM-2025-001', 24000000, 42), (N'WPSM-2025-002', 25000000, 26), (N'WPSM-2025-004', 24500000, 49),
(N'WPSM-2025-008', 25000000, 46), (N'WPSM-2025-011', 25000000, 36), (N'WPSM-2025-019', 24000000, 32);

DECLARE @Core TABLE(Category NVARCHAR(30) PRIMARY KEY, ItemId INT);
INSERT @Core SELECT c.Category, MIN(c.ItemId) FROM dbo.ServiceCatalogItems c
WHERE c.Status = N'Active' AND c.Category IN (N'Hall', N'Catering', N'Decor', N'MC', N'AV') GROUP BY c.Category;

-- Only projects whose baseline still has the leaked extra lines.
DECLARE @Fix TABLE(ProjectId INT PRIMARY KEY, HallPrice DECIMAL(18,2), Tables INT);
INSERT @Fix SELECT p.ProjectId, s.HallPrice, s.Tables
FROM dbo.WeddingProjects p JOIN @Spec s ON s.Code = p.ProjectCode
WHERE (SELECT COUNT(*) FROM dbo.SpecItems x WHERE x.ProjectId = p.ProjectId AND x.Version = 1) > 5;

-- Remember CR lines before the baseline changes, to restate them against the new baseline.
DECLARE @CrLine TABLE(CRItemId INT PRIMARY KEY, OldQ INT, OldP DECIMAL(18,2), NewQ INT, NewP DECIMAL(18,2));
INSERT @CrLine SELECT ci.CRItemId, ci.OldQuantity, ci.OldUnitPrice, ci.NewQuantity, ci.NewUnitPrice
FROM dbo.CRItems ci JOIN dbo.SpecItems si ON si.SpecItemId = ci.TargetSpecItemId JOIN @Fix f ON f.ProjectId = si.ProjectId;

-- Seeded frozen baselines were wrong from the start: lift the immutability trigger for this repair only
-- (inside the transaction, so an error also restores it).
DISABLE TRIGGER dbo.TR_SpecItems_BaselineImmutable ON dbo.SpecItems;

DELETE si FROM dbo.SpecItems si JOIN @Fix f ON f.ProjectId = si.ProjectId
WHERE si.Version = 1 AND si.CatalogItemId NOT IN (SELECT ItemId FROM @Core)
  AND NOT EXISTS (SELECT 1 FROM dbo.CRItems ci WHERE ci.TargetSpecItemId = si.SpecItemId);

UPDATE si SET
    Quantity  = CASE WHEN c.Category = N'Catering' THEN f.Tables ELSE 1 END,
    UnitPrice = CASE WHEN c.Category = N'Hall' THEN f.HallPrice ELSE cat.DefaultPrice END
FROM dbo.SpecItems si JOIN @Fix f ON f.ProjectId = si.ProjectId
JOIN @Core c ON c.ItemId = si.CatalogItemId JOIN dbo.ServiceCatalogItems cat ON cat.ItemId = si.CatalogItemId
WHERE si.Version = 1;

ENABLE TRIGGER dbo.TR_SpecItems_BaselineImmutable ON dbo.SpecItems;

-- CR lines keep their change (extra tables or the same money delta) on top of the new baseline.
UPDATE ci SET
    OldQuantity  = si.Quantity,
    OldUnitPrice = si.UnitPrice,
    NewQuantity  = si.Quantity + (l.NewQ - l.OldQ),
    NewUnitPrice = CASE WHEN l.NewP = l.OldP THEN si.UnitPrice
                        ELSE si.UnitPrice + ROUND((l.NewP - l.OldP) * l.OldQ / si.Quantity, 2) END
FROM dbo.CRItems ci JOIN @CrLine l ON l.CRItemId = ci.CRItemId JOIN dbo.SpecItems si ON si.SpecItemId = ci.TargetSpecItemId;

IF EXISTS (SELECT 1 FROM @Fix f JOIN dbo.WeddingProjects p ON p.ProjectId = f.ProjectId
           WHERE (SELECT SUM(LineTotal) FROM dbo.SpecItems x WHERE x.ProjectId = f.ProjectId AND x.Version = 1
                    AND x.CatalogItemId IN (SELECT ItemId FROM @Core)) <> p.BaseContractValue)
    THROW 50301, N'Tổng đặc tả sau khi sửa không bằng giá trị hợp đồng.', 1;

-- 2. Drafting sample projects whose wedding date already passed. ---------------------------------------------------
DECLARE @Move TABLE(ProjectId INT PRIMARY KEY, Offset INT);
INSERT @Move SELECT p.ProjectId, DATEDIFF(DAY, p.EventDate, m.NewDate)
FROM dbo.WeddingProjects p
JOIN (VALUES (N'WPSM-2025-002', CAST('2027-01-23' AS DATE)), (N'WPSM-2025-008', '2027-01-16'), (N'WPSM-2025-019', '2027-01-30')) m(Code, NewDate)
  ON m.Code = p.ProjectCode
WHERE p.Status = N'Active Baseline' AND p.EventDate < CAST(GETDATE() AS DATE)
  AND NOT EXISTS (SELECT 1 FROM dbo.WeddingProjects o WHERE o.ProjectId <> p.ProjectId AND o.HallId = p.HallId
                  AND o.SlotId = p.SlotId AND o.EventDate = m.NewDate);

UPDATE p SET EventDate = DATEADD(DAY, m.Offset, p.EventDate), CreatedAt = DATEADD(DAY, m.Offset, p.CreatedAt)
FROM dbo.WeddingProjects p JOIN @Move m ON m.ProjectId = p.ProjectId;

UPDATE t SET DueDate = DATEADD(DAY, m.Offset, t.DueDate), CompletedAt = DATEADD(DAY, m.Offset, t.CompletedAt), RemindedAt = NULL
FROM dbo.RoadmapTasks t JOIN @Move m ON m.ProjectId = t.ProjectId;

UPDATE x SET DueDate = DATEADD(DAY, m.Offset, x.DueDate), PaidDate = DATEADD(DAY, m.Offset, x.PaidDate)
FROM dbo.Payments x JOIN @Move m ON m.ProjectId = x.ProjectId;

UPDATE d SET DeadlineTime = DATEADD(DAY, m.Offset, d.DeadlineTime), Status = N'Pending', CompletedAt = NULL, CompletedBy = NULL, OverdueFlaggedAt = NULL
FROM dbo.DayOfTasks d JOIN @Move m ON m.ProjectId = d.ProjectId;

COMMIT;

SELECT p.ProjectCode, p.Status, p.EventDate, p.BaseContractValue,
       (SELECT SUM(LineTotal) FROM dbo.SpecItems s WHERE s.ProjectId = p.ProjectId AND s.Version = 1) AS BaselineTotal
FROM dbo.WeddingProjects p WHERE p.ProjectCode LIKE N'WPSM-2025-%' ORDER BY p.ProjectCode;

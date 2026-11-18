-- FE-07 Resource Calendar (M-21, M-22) — additive update for existing databases.
-- Adds the PendingException state documented in SDS 3.13: an over-capacity allocation waiting for the
-- Admin decision on its special exception request (UC-47). Pending rows do not consume capacity.
-- Safe to run repeatedly; no data is deleted.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Alloc_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.ResourceAllocations')
      AND definition NOT LIKE '%PendingException%')
BEGIN
    ALTER TABLE dbo.ResourceAllocations DROP CONSTRAINT CK_Alloc_Status;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Alloc_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.ResourceAllocations'))
BEGIN
    ALTER TABLE dbo.ResourceAllocations WITH CHECK
        ADD CONSTRAINT CK_Alloc_Status CHECK (Status IN ('Reserved','PendingException','Exception Approved','Released'));
END
GO

-- Demo resources (idempotent) so the calendar has lanes on a seeded database.
INSERT INTO dbo.Resources (Name, Type, TotalQuantity)
SELECT seed.Name, seed.Type, seed.TotalQuantity
FROM (VALUES
    (N'Dàn âm thanh sảnh chính', N'Audio',        4),
    (N'Dàn âm thanh sân vườn',  N'Audio',        2),
    (N'Đèn Moving Head & Follow Spot', N'Follow Light', 4),
    (N'Đèn rọi sân khấu',       N'Follow Light', 6),
    (N'Khung trống vòm hoa đôi VIP', N'Decor Frame', 3),
    (N'Cổng hoa chính',         N'Decor Frame',  2),
    (N'Kỹ thuật viên AV & Stage Manager', N'Technician', 6),
    (N'Nhân sự ánh sáng',       N'Technician',   4),
    (N'Đội bếp trưởng & bếp nòng VIP', N'Kitchen', 3),
    (N'Nhân sự sơ chế',         N'Kitchen',      5)
) AS seed(Name, Type, TotalQuantity)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Resources r WHERE r.Type = seed.Type AND r.Name = seed.Name);
GO

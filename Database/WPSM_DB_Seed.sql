-- WPSM SQL Server demo data. Run AFTER WPSM_DB_Schema.sql.
-- These are fictional fixtures, not an export of private user records.
-- Demo accounts: demo.admin@example.invalid, demo.manager@example.invalid,
-- demo.customer1@example.invalid through demo.customer7@example.invalid.
-- Initial password for DEMO accounts only: WpsmDemo@2026!
-- First login requires a password change. Existing accounts are never reset.
-- Does not send email, replace existing data, or copy receipts/refresh tokens.
USE [WPSM_DB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @Hash NVARCHAR(255)=N'AQAAAAIAAYagAAAAEAkFXKxli4/fMlLncehrn/ESG5Pcj97oHO+WV86cJ+j+4eGsK6r0KJROvP698apnzg==';
DECLARE @Effective DATE='2025-01-01';
DECLARE @Projects TABLE(Code NVARCHAR(30),Couple NVARCHAR(100),Email NVARCHAR(100),Hall NVARCHAR(100),Slot NVARCHAR(20),EventDate DATE,BaseValue DECIMAL(18,2),NRD DECIMAL(18,2),Status NVARCHAR(20));
INSERT @Projects VALUES
(N'WPSM-2026-014',N'Nguyễn Minh Anh & Trần Thu Hà',N'demo.customer1@example.invalid',N'Sảnh Hoàng Gia',N'Evening',N'2026-12-20',185000000,55500000,N'Baseline Frozen'),
(N'WPSM-2025-008',N'Nguyễn Bảo Long & Trần Phương Linh',N'demo.customer2@example.invalid',N'Sảnh Kim Cương',N'Evening',N'2025-12-28',210000000,63000000,N'Active Baseline'),
(N'WPSM-2025-019',N'Lê Hoàng Nam & Vũ Hoài Phương',N'demo.customer3@example.invalid',N'Sảnh Pha Lê',N'Lunch',N'2025-12-15',160000000,48000000,N'Active Baseline'),
(N'WPSM-2025-002',N'Phạm Tuấn & Đỗ Lan Hương',N'demo.customer4@example.invalid',N'Sảnh Vườn Ngọc',N'Evening',N'2025-11-10',140000000,42000000,N'Active Baseline'),
(N'WPSM-2025-001',N'Trần Văn An & Lê Mai Chi',N'demo.customer5@example.invalid',N'Sảnh Kim Cương',N'Evening',N'2025-10-25',195000000,58500000,N'Event Completed'),
(N'WPSM-2025-011',N'Hoàng Quốc Bảo & Phạm Thảo Linh',N'demo.customer6@example.invalid',N'Sảnh Pha Lê',N'Evening',N'2025-10-05',175000000,52500000,N'Settled'),
(N'WPSM-2025-004',N'Đặng Tuấn Kiệt & Bùi Thúy Hạnh',N'demo.customer7@example.invalid',N'Sảnh Hoàng Gia',N'Lunch',N'2025-09-18',220000000,66000000,N'Closed');

-- 1. Users: use separate demo identities, never passwords from a live DB.
DECLARE @Accounts TABLE(Email NVARCHAR(100),FullName NVARCHAR(100),Role NVARCHAR(20));
INSERT @Accounts VALUES (N'demo.admin@example.invalid',N'WPSM Demo Admin',N'Admin'),(N'demo.manager@example.invalid',N'WPSM Demo Manager',N'Manager');
INSERT @Accounts SELECT Email,Couple,N'Customer' FROM @Projects;
IF EXISTS (SELECT 1 FROM @Accounts a JOIN dbo.Users u ON u.Email=a.Email WHERE u.Role<>a.Role OR u.Status<>N'Active')
    THROW 50001,N'A demo email exists with a different role or inactive status. No data was changed.',1;
INSERT dbo.Users(Email,PasswordHash,FullName,Phone,Role,Status,MustChangePassword,CreatedAt)
SELECT a.Email,@Hash,a.FullName,NULL,a.Role,N'Active',1,SYSUTCDATETIME() FROM @Accounts a
WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Email=a.Email);
DECLARE @Admin INT=(SELECT UserId FROM dbo.Users WHERE Email=N'demo.admin@example.invalid');
DECLARE @Manager INT=(SELECT UserId FROM dbo.Users WHERE Email=N'demo.manager@example.invalid');

-- 2. Halls and shifts. Let SQL Server generate IDs and resolve foreign keys.
DECLARE @Halls TABLE(Name NVARCHAR(100),MinCapacity INT,MaxCapacity INT,BasePrice DECIMAL(18,2));
INSERT @Halls VALUES (N'Sảnh Kim Cương',450,650,60000000),(N'Sảnh Pha Lê',300,450,45000000),(N'Sảnh Hoàng Gia',600,900,80000000),(N'Sảnh Vườn Ngọc',200,350,35000000);
INSERT dbo.Halls(Name,MinCapacity,MaxCapacity,BasePrice,Status)
SELECT h.Name,h.MinCapacity,h.MaxCapacity,h.BasePrice,N'Active' FROM @Halls h WHERE NOT EXISTS(SELECT 1 FROM dbo.Halls x WHERE x.Name=h.Name);
INSERT dbo.Slots(HallId,SlotName,StartTime,EndTime)
SELECT h.HallId,s.Name,s.StartTime,s.EndTime FROM dbo.Halls h JOIN @Halls d ON h.Name=d.Name
CROSS JOIN (VALUES(N'Lunch',CAST('11:00' AS TIME),CAST('14:00' AS TIME)),(N'Evening',CAST('17:30' AS TIME),CAST('21:30' AS TIME)))s(Name,StartTime,EndTime)
WHERE NOT EXISTS(SELECT 1 FROM dbo.Slots x WHERE x.HallId=h.HallId AND x.SlotName=s.Name);

-- 3. Catalog, price frames and policies.
DECLARE @Catalog TABLE(Name NVARCHAR(100),Category NVARCHAR(30),Unit NVARCHAR(20),Price DECIMAL(18,2));
INSERT @Catalog VALUES (N'Thuê sảnh tiệc',N'Hall',N'Gói',25000000),(N'Thực đơn tiệc cưới',N'Catering',N'Bàn',3500000),(N'Trang trí sân khấu',N'Decor',N'Gói',12000000),(N'MC chương trình',N'MC',N'Người',4000000),(N'Âm thanh và ánh sáng',N'AV',N'Gói',8000000);
INSERT dbo.ServiceCatalogItems(Name,Category,Unit,DefaultPrice,Status)
SELECT c.Name,c.Category,c.Unit,c.Price,N'Active' FROM @Catalog c WHERE NOT EXISTS(SELECT 1 FROM dbo.ServiceCatalogItems x WHERE x.Name=c.Name AND x.Category=c.Category);
INSERT dbo.PriceFrames(ItemId,MinPrice,MaxPrice,EffectiveFrom,UpdatedBy,UpdatedAt)
SELECT i.ItemId,c.Price*0.8,c.Price*1.2,@Effective,@Admin,SYSUTCDATETIME() FROM @Catalog c JOIN dbo.ServiceCatalogItems i ON i.Name=c.Name AND i.Category=c.Category
WHERE NOT EXISTS(SELECT 1 FROM dbo.PriceFrames x WHERE x.ItemId=i.ItemId);
INSERT dbo.PolicyConfigs(PolicyType,TDayMin,TDayMax,RatePercent,EffectiveFrom,UpdatedBy,UpdatedAt)
SELECT p.Type,p.Minimum,p.Maximum,p.Rate,@Effective,@Admin,SYSUTCDATETIME()
FROM (VALUES(N'NRD_RATIO',CAST(NULL AS INT),CAST(NULL AS INT),30),(N'T30_RATIO',NULL,NULL,70),(N'CR_PENALTY',0,14,5))p(Type,Minimum,Maximum,Rate)
WHERE NOT EXISTS(SELECT 1 FROM dbo.PolicyConfigs x WHERE x.PolicyType=p.Type);

-- 4. Projects. Existing codes must belong to these demo identities; otherwise
-- roll back instead of silently attaching fixtures to someone's real project.
IF EXISTS(SELECT 1 FROM @Projects d JOIN dbo.WeddingProjects p ON p.ProjectCode=d.Code JOIN dbo.Users u ON u.UserId=p.CustomerId WHERE u.Email<>d.Email OR p.ManagerId<>@Manager)
    THROW 50002,N'Sample project codes already exist outside these demo accounts. Use an empty demo database; existing projects were not overwritten.',1;
INSERT dbo.WeddingProjects(ProjectCode,CustomerId,ManagerId,HallId,SlotId,EventDate,BaseContractValue,NRDAmount,CurrentTotal,Status,BaselineFrozenAt,CreatedAt)
SELECT d.Code,u.UserId,@Manager,h.HallId,s.SlotId,d.EventDate,d.BaseValue,d.NRD,d.BaseValue,d.Status,
CASE WHEN d.Status=N'Active Baseline' THEN NULL ELSE DATEADD(DAY,-30,CAST(d.EventDate AS DATETIME2)) END,
DATEADD(DAY,-90,CAST(d.EventDate AS DATETIME2))
FROM @Projects d JOIN dbo.Users u ON u.Email=d.Email JOIN dbo.Halls h ON h.Name=d.Hall JOIN dbo.Slots s ON s.HallId=h.HallId AND s.SlotName=d.Slot
WHERE NOT EXISTS(SELECT 1 FROM dbo.WeddingProjects x WHERE x.ProjectCode=d.Code);

-- 5. Baseline specification (computed LineTotal is intentionally omitted).
INSERT dbo.SpecItems(ProjectId,CatalogItemId,Description,Quantity,UnitPrice,IsBaseline,Version)
SELECT p.ProjectId,i.ItemId,c.Name,CASE WHEN c.Category=N'Catering' THEN 30 ELSE 1 END,c.Price,1,1
FROM @Projects d JOIN dbo.WeddingProjects p ON p.ProjectCode=d.Code CROSS JOIN @Catalog c
JOIN dbo.ServiceCatalogItems i ON i.Name=c.Name AND i.Category=c.Category
WHERE NOT EXISTS(SELECT 1 FROM dbo.SpecItems x WHERE x.ProjectId=p.ProjectId AND x.CatalogItemId=i.ItemId AND x.Version=1);

-- 6. Cash NRD records: no copied documents or private receipt data.
INSERT dbo.Payments(ProjectId,PaymentType,Status,DueDate,Amount,PaymentMethod,RecordedBy,PaidDate)
SELECT p.ProjectId,N'NRD',N'Paid',DATEADD(DAY,-90,d.EventDate),d.NRD,N'Cash',@Manager,DATEADD(DAY,-90,d.EventDate)
FROM @Projects d JOIN dbo.WeddingProjects p ON p.ProjectCode=d.Code
WHERE NOT EXISTS(SELECT 1 FROM dbo.Payments x WHERE x.ProjectId=p.ProjectId AND x.PaymentType=N'NRD');

-- 7. Four milestones per project.
INSERT dbo.RoadmapTasks(ProjectId,Milestone,Title,AssigneeRole,DueDate,Status,CompletedAt)
SELECT p.ProjectId,t.Milestone,t.Title,N'Manager',DATEADD(DAY,t.OffsetDays,d.EventDate),
CASE WHEN t.Milestone=N'T-90' OR (t.Milestone=N'T-30' AND d.Status<>N'Active Baseline') OR (t.OffsetDays>=-7 AND d.Status IN(N'Event Completed',N'Settled',N'Closed')) THEN N'Done' ELSE N'Pending' END,
CASE WHEN t.Milestone=N'T-90' OR (t.Milestone=N'T-30' AND d.Status<>N'Active Baseline') OR (t.OffsetDays>=-7 AND d.Status IN(N'Event Completed',N'Settled',N'Closed')) THEN DATEADD(HOUR,12,CAST(DATEADD(DAY,t.OffsetDays,d.EventDate) AS DATETIME2)) ELSE NULL END
FROM @Projects d JOIN dbo.WeddingProjects p ON p.ProjectCode=d.Code
CROSS JOIN (VALUES(N'T-90',N'Khởi tạo hợp đồng gốc & thu NRD',-90),(N'T-30',N'Đóng băng baseline & chuẩn bị BEO',-30),(N'T-7',N'Chốt số bàn & nhân sự điều phối',-7),(N'Event Day',N'Vận hành & giám sát tiệc cưới',0))t(Milestone,Title,OffsetDays)
WHERE NOT EXISTS(SELECT 1 FROM dbo.RoadmapTasks x WHERE x.ProjectId=p.ProjectId AND x.Milestone=t.Milestone);
COMMIT TRANSACTION;
PRINT N'Demo seed complete: 9 accounts, 4 halls, 8 slots, 5 catalog items, 7 projects, 35 specification rows, 7 NRD payments, 28 roadmap tasks on an empty database.';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
GO

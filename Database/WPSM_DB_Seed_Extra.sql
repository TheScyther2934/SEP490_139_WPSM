-- WPSM extra demo data. Run AFTER WPSM_DB_Schema.sql (and optionally WPSM_DB_Seed.sql).
-- Additive and re-runnable: never updates or deletes existing rows. Works on a database
-- seeded either by WPSM_DB_Seed.sql or by the API DevelopmentDatabaseSeeder, because
-- Admin/Manager are resolved by role, not by a fixed email.
-- Adds: CR penalty T-day ranges, 1 hall + 2 slots, 11 catalog items with price frames,
-- 5 resources, 6 customers and 6 projects in different states with spec, payments,
-- roadmap tasks and resource allocations.
-- Demo customers: demo.extra1@example.invalid .. demo.extra6@example.invalid
-- Initial password (DEMO accounts only): WpsmDemo@2026!  (must change on first login)
-- A project is skipped (with a message) when its code or its hall/slot/date is taken.
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
DECLARE @Today DATE=CAST(GETDATE() AS DATE);
DECLARE @Admin INT=(SELECT TOP 1 UserId FROM dbo.Users WHERE Role=N'Admin' AND Status=N'Active' ORDER BY UserId);
DECLARE @Manager INT=(SELECT TOP 1 UserId FROM dbo.Users WHERE Role=N'Manager' AND Status=N'Active' ORDER BY UserId);
IF @Admin IS NULL OR @Manager IS NULL
    THROW 50101,N'Cần có sẵn ít nhất một Admin và một Manager đang hoạt động. Chưa thay đổi dữ liệu nào.',1;

-- 1. CR penalty: add ranges to the latest rule set (a set = all rows with the same EffectiveFrom).
--    Ranges must not overlap the ones already in that set (A-08, MSG23).
DECLARE @CrSet DATE=ISNULL((SELECT MAX(EffectiveFrom) FROM dbo.PolicyConfigs WHERE PolicyType=N'CR_PENALTY'),@Effective);
IF NOT EXISTS(SELECT 1 FROM dbo.PolicyConfigs WHERE PolicyType=N'CR_PENALTY' AND EffectiveFrom=@CrSet)
    INSERT dbo.PolicyConfigs(PolicyType,TDayMin,TDayMax,RatePercent,EffectiveFrom,UpdatedBy) VALUES(N'CR_PENALTY',0,14,5,@CrSet,@Admin);
INSERT dbo.PolicyConfigs(PolicyType,TDayMin,TDayMax,RatePercent,EffectiveFrom,UpdatedBy)
SELECT N'CR_PENALTY',r.MinDay,r.MaxDay,r.Rate,@CrSet,@Admin
FROM (VALUES(15,30,CAST(3 AS DECIMAL(5,2))),(31,60,1))r(MinDay,MaxDay,Rate)
WHERE NOT EXISTS(SELECT 1 FROM dbo.PolicyConfigs x WHERE x.PolicyType=N'CR_PENALTY' AND x.EffectiveFrom=@CrSet
                 AND x.TDayMin<=r.MaxDay AND x.TDayMax>=r.MinDay);
DECLARE @NrdRatio DECIMAL(5,2)=ISNULL((SELECT TOP 1 RatePercent FROM dbo.PolicyConfigs WHERE PolicyType=N'NRD_RATIO' AND EffectiveFrom<=@Today ORDER BY EffectiveFrom DESC),30);
DECLARE @T30Ratio DECIMAL(5,2)=ISNULL((SELECT TOP 1 RatePercent FROM dbo.PolicyConfigs WHERE PolicyType=N'T30_RATIO' AND EffectiveFrom<=@Today ORDER BY EffectiveFrom DESC),70);

-- 2. Hall and slots.
INSERT dbo.Halls(Name,MinCapacity,MaxCapacity,BasePrice,Status)
SELECT N'Sảnh Ngọc Trai',250,400,40000000,N'Active' WHERE NOT EXISTS(SELECT 1 FROM dbo.Halls WHERE Name=N'Sảnh Ngọc Trai');
INSERT dbo.Slots(HallId,SlotName,StartTime,EndTime)
SELECT h.HallId,s.Name,s.StartTime,s.EndTime FROM dbo.Halls h
CROSS JOIN (VALUES(N'Lunch',CAST('11:00' AS TIME),CAST('14:00' AS TIME)),(N'Evening',CAST('17:30' AS TIME),CAST('21:30' AS TIME)))s(Name,StartTime,EndTime)
WHERE h.Name=N'Sảnh Ngọc Trai' AND NOT EXISTS(SELECT 1 FROM dbo.Slots x WHERE x.HallId=h.HallId AND x.SlotName=s.Name);

-- 3. Catalog items (Category is stored in English, L-10) and price frames ±20%.
DECLARE @Catalog TABLE(Name NVARCHAR(100),Category NVARCHAR(30),Unit NVARCHAR(20),Price DECIMAL(18,2));
INSERT @Catalog VALUES
(N'Thực đơn cao cấp',N'Catering',N'Bàn',5000000),
(N'Tiệc ngọt & bánh cưới',N'Catering',N'Gói',6000000),
(N'Hoa tươi bàn tiệc',N'Decor',N'Bàn',450000),
(N'Cổng hoa lối vào',N'Decor',N'Gói',9000000),
(N'Chụp ảnh phóng sự cưới',N'Photo/Video',N'Gói',15000000),
(N'Quay phim ngày cưới',N'Photo/Video',N'Gói',18000000),
(N'Trang điểm cô dâu',N'Makeup',N'Người',5000000),
(N'Xe hoa',N'Transport',N'Chiếc',4500000),
(N'Màn hình LED sân khấu',N'AV',N'Gói',10000000),
(N'Ban nhạc acoustic',N'AV',N'Gói',12000000),
(N'Áo dài cưới',N'Attire',N'Bộ',3000000);
INSERT dbo.ServiceCatalogItems(Name,Category,Unit,DefaultPrice,Status)
SELECT c.Name,c.Category,c.Unit,c.Price,N'Active' FROM @Catalog c
WHERE NOT EXISTS(SELECT 1 FROM dbo.ServiceCatalogItems x WHERE x.Name=c.Name AND x.Category=c.Category);
INSERT dbo.PriceFrames(ItemId,MinPrice,MaxPrice,EffectiveFrom,UpdatedBy)
SELECT i.ItemId,i.DefaultPrice*0.8,i.DefaultPrice*1.2,@Effective,@Admin
FROM @Catalog c JOIN dbo.ServiceCatalogItems i ON i.Name=c.Name AND i.Category=c.Category
WHERE NOT EXISTS(SELECT 1 FROM dbo.PriceFrames x WHERE x.ItemId=i.ItemId);

-- 4. Shared resources (M-10 scheduling).
INSERT dbo.Resources(Name,Type,TotalQuantity)
SELECT r.Name,r.Type,r.Qty FROM (VALUES
(N'Dàn âm thanh line array',N'Audio',3),(N'Đèn follow 1200W',N'Follow Light',4),(N'Khung backdrop 6m',N'Decor Frame',5),
(N'Kỹ thuật viên âm thanh',N'Technician',6),(N'Bếp nóng khu B',N'Kitchen',2))r(Name,Type,Qty)
WHERE NOT EXISTS(SELECT 1 FROM dbo.Resources x WHERE x.Type=r.Type AND x.Name=r.Name);

-- 5. Customers and projects.
DECLARE @Projects TABLE(Code NVARCHAR(30),Couple NVARCHAR(100),Email NVARCHAR(100),Phone NVARCHAR(11),Hall NVARCHAR(100),Slot NVARCHAR(20),EventDate DATE,NrdPercent DECIMAL(5,2),Status NVARCHAR(20));
INSERT @Projects VALUES
(N'WPSM-2026-101',N'Võ Đức Thịnh & Ngô Bảo Trâm',N'demo.extra1@example.invalid',N'0911000101',N'Sảnh Pha Lê',N'Evening',N'2026-12-06',30,N'Baseline Frozen'),
(N'WPSM-2026-102',N'Trịnh Gia Huy & Mai Khánh Linh',N'demo.extra2@example.invalid',N'0911000102',N'Sảnh Vườn Ngọc',N'Lunch',N'2026-12-27',30,N'Baseline Frozen'),
(N'WPSM-2027-103',N'Phan Thành Đạt & Lý Ngọc Hân',N'demo.extra3@example.invalid',N'0911000103',N'Sảnh Kim Cương',N'Evening',N'2027-03-14',35,N'Active Baseline'),
(N'WPSM-2027-104',N'Đỗ Minh Khôi & Hà Thu Trang',N'demo.extra4@example.invalid',N'0911000104',N'Sảnh Ngọc Trai',N'Lunch',N'2027-02-20',30,N'Active Baseline'),
(N'WPSM-2026-105',N'Bùi Anh Tuấn & Lâm Gia Hân',N'demo.extra5@example.invalid',N'0911000105',N'Sảnh Hoàng Gia',N'Lunch',N'2026-11-08',30,N'Event Completed'),
(N'WPSM-2026-106',N'Cao Văn Lực & Tạ Minh Thư',N'demo.extra6@example.invalid',N'0911000106',N'Sảnh Kim Cương',N'Lunch',N'2026-10-18',30,N'Settled');
IF @NrdRatio>30
    UPDATE @Projects SET NrdPercent=@NrdRatio WHERE NrdPercent<@NrdRatio;   -- keep NRD at or above the policy minimum (MSG14)

-- Baseline specification per project; BaseContractValue = sum of its lines.
DECLARE @Spec TABLE(Code NVARCHAR(30),Category NVARCHAR(30),Name NVARCHAR(100),Qty INT,Price DECIMAL(18,2));
INSERT @Spec VALUES
(N'WPSM-2026-101',N'Hall',N'Thuê sảnh tiệc',1,25000000),(N'WPSM-2026-101',N'Catering',N'Thực đơn tiệc cưới',35,3500000),
(N'WPSM-2026-101',N'Decor',N'Trang trí sân khấu',1,12000000),(N'WPSM-2026-101',N'MC',N'MC chương trình',1,4000000),
(N'WPSM-2026-101',N'AV',N'Âm thanh và ánh sáng',1,8000000),(N'WPSM-2026-101',N'Photo/Video',N'Chụp ảnh phóng sự cưới',1,15000000),
(N'WPSM-2026-101',N'Decor',N'Hoa tươi bàn tiệc',35,450000),
(N'WPSM-2026-102',N'Hall',N'Thuê sảnh tiệc',1,25000000),(N'WPSM-2026-102',N'Catering',N'Thực đơn tiệc cưới',25,3500000),
(N'WPSM-2026-102',N'Decor',N'Trang trí sân khấu',1,12000000),(N'WPSM-2026-102',N'MC',N'MC chương trình',1,4000000),
(N'WPSM-2026-102',N'AV',N'Âm thanh và ánh sáng',1,8000000),(N'WPSM-2026-102',N'Makeup',N'Trang điểm cô dâu',1,5000000),
(N'WPSM-2026-102',N'Transport',N'Xe hoa',1,4500000),
(N'WPSM-2027-103',N'Hall',N'Thuê sảnh tiệc',1,25000000),(N'WPSM-2027-103',N'Catering',N'Thực đơn cao cấp',50,5000000),
(N'WPSM-2027-103',N'Decor',N'Trang trí sân khấu',1,12000000),(N'WPSM-2027-103',N'Decor',N'Cổng hoa lối vào',1,9000000),
(N'WPSM-2027-103',N'MC',N'MC chương trình',1,4000000),(N'WPSM-2027-103',N'AV',N'Âm thanh và ánh sáng',1,8000000),
(N'WPSM-2027-103',N'AV',N'Màn hình LED sân khấu',1,10000000),(N'WPSM-2027-103',N'Photo/Video',N'Quay phim ngày cưới',1,18000000),
(N'WPSM-2027-103',N'Photo/Video',N'Chụp ảnh phóng sự cưới',1,15000000),
(N'WPSM-2027-104',N'Hall',N'Thuê sảnh tiệc',1,25000000),(N'WPSM-2027-104',N'Catering',N'Thực đơn tiệc cưới',30,3500000),
(N'WPSM-2027-104',N'Decor',N'Trang trí sân khấu',1,12000000),(N'WPSM-2027-104',N'MC',N'MC chương trình',1,4000000),
(N'WPSM-2027-104',N'AV',N'Âm thanh và ánh sáng',1,8000000),(N'WPSM-2027-104',N'Catering',N'Tiệc ngọt & bánh cưới',1,6000000),
(N'WPSM-2026-105',N'Hall',N'Thuê sảnh tiệc',1,25000000),(N'WPSM-2026-105',N'Catering',N'Thực đơn tiệc cưới',60,3500000),
(N'WPSM-2026-105',N'Decor',N'Trang trí sân khấu',1,12000000),(N'WPSM-2026-105',N'MC',N'MC chương trình',1,4000000),
(N'WPSM-2026-105',N'AV',N'Âm thanh và ánh sáng',1,8000000),(N'WPSM-2026-105',N'Photo/Video',N'Chụp ảnh phóng sự cưới',1,15000000),
(N'WPSM-2026-105',N'Photo/Video',N'Quay phim ngày cưới',1,18000000),
(N'WPSM-2026-106',N'Hall',N'Thuê sảnh tiệc',1,25000000),(N'WPSM-2026-106',N'Catering',N'Thực đơn tiệc cưới',40,3500000),
(N'WPSM-2026-106',N'Decor',N'Trang trí sân khấu',1,12000000),(N'WPSM-2026-106',N'MC',N'MC chương trình',1,4000000),
(N'WPSM-2026-106',N'AV',N'Âm thanh và ánh sáng',1,8000000),(N'WPSM-2026-106',N'Attire',N'Áo dài cưới',1,3000000);

IF EXISTS(SELECT 1 FROM @Spec s WHERE NOT EXISTS(SELECT 1 FROM dbo.ServiceCatalogItems i WHERE i.Category=s.Category AND i.Name=s.Name))
    THROW 50102,N'Thiếu dịch vụ gốc trong danh mục (Thuê sảnh tiệc, Thực đơn tiệc cưới...). Hãy chạy WPSM_DB_Seed.sql hoặc khởi động API một lần trước.',1;

INSERT dbo.Users(Email,PasswordHash,FullName,Phone,Role,Status,MustChangePassword)
SELECT d.Email,@Hash,d.Couple,d.Phone,N'Customer',N'Active',1 FROM @Projects d
WHERE NOT EXISTS(SELECT 1 FROM dbo.Users u WHERE u.Email=d.Email);

-- Resolve each sample: skip when the code belongs to someone else or the hall slot is taken.
DECLARE @Plan TABLE(Code NVARCHAR(30) PRIMARY KEY,CustomerId INT,HallId INT,SlotId INT,EventDate DATE,BaseValue DECIMAL(18,2),Nrd DECIMAL(18,2),T30 DECIMAL(18,2),Status NVARCHAR(20),CreatedAt DATETIME2(0),FrozenAt DATETIME2(0));
INSERT @Plan
SELECT d.Code,u.UserId,h.HallId,s.SlotId,d.EventDate,b.BaseValue,
       ROUND(b.BaseValue*d.NrdPercent/100,0),
       ROUND(b.BaseValue*@T30Ratio/100,0)-ROUND(b.BaseValue*d.NrdPercent/100,0),
       d.Status,
       CASE WHEN DATEADD(DAY,-120,d.EventDate)<DATEADD(DAY,-1,@Today) THEN DATEADD(DAY,-120,CAST(d.EventDate AS DATETIME2(0))) ELSE DATEADD(DAY,-1,CAST(@Today AS DATETIME2(0))) END,
       CASE WHEN d.Status=N'Active Baseline' THEN NULL
            WHEN DATEADD(DAY,-35,d.EventDate)<@Today THEN DATEADD(HOUR,10,DATEADD(DAY,-35,CAST(d.EventDate AS DATETIME2(0))))
            ELSE DATEADD(HOUR,10,DATEADD(DAY,-1,CAST(@Today AS DATETIME2(0)))) END
FROM @Projects d
JOIN dbo.Users u ON u.Email=d.Email AND u.Role=N'Customer'
JOIN dbo.Halls h ON h.Name=d.Hall
JOIN dbo.Slots s ON s.HallId=h.HallId AND s.SlotName=d.Slot
CROSS APPLY (SELECT SUM(x.Qty*x.Price) BaseValue FROM @Spec x WHERE x.Code=d.Code) b;

SELECT p.Code+N': bỏ qua vì mã dự án đã thuộc khách hàng khác hoặc ca sảnh đã có dự án khác.' AS Skipped
FROM @Plan p
WHERE EXISTS(SELECT 1 FROM dbo.WeddingProjects w WHERE w.ProjectCode=p.Code AND w.CustomerId<>p.CustomerId)
   OR EXISTS(SELECT 1 FROM dbo.WeddingProjects w WHERE w.HallId=p.HallId AND w.SlotId=p.SlotId AND w.EventDate=p.EventDate AND w.ProjectCode<>p.Code);
DELETE p FROM @Plan p
WHERE EXISTS(SELECT 1 FROM dbo.WeddingProjects w WHERE w.ProjectCode=p.Code AND w.CustomerId<>p.CustomerId)
   OR EXISTS(SELECT 1 FROM dbo.WeddingProjects w WHERE w.HallId=p.HallId AND w.SlotId=p.SlotId AND w.EventDate=p.EventDate AND w.ProjectCode<>p.Code);

INSERT dbo.WeddingProjects(ProjectCode,CustomerId,ManagerId,HallId,SlotId,EventDate,BaseContractValue,NRDAmount,CurrentTotal,Status,BaselineFrozenAt,CreatedAt)
SELECT p.Code,p.CustomerId,@Manager,p.HallId,p.SlotId,p.EventDate,p.BaseValue,p.Nrd,p.BaseValue,p.Status,p.FrozenAt,p.CreatedAt
FROM @Plan p WHERE NOT EXISTS(SELECT 1 FROM dbo.WeddingProjects w WHERE w.ProjectCode=p.Code);

-- 6. Baseline specification (computed LineTotal omitted).
INSERT dbo.SpecItems(ProjectId,CatalogItemId,Description,Quantity,UnitPrice,IsBaseline,Version)
SELECT w.ProjectId,i.ItemId,s.Name,s.Qty,s.Price,1,1
FROM @Plan p JOIN dbo.WeddingProjects w ON w.ProjectCode=p.Code
JOIN @Spec s ON s.Code=p.Code
JOIN dbo.ServiceCatalogItems i ON i.Category=s.Category AND i.Name=s.Name
WHERE NOT EXISTS(SELECT 1 FROM dbo.SpecItems x WHERE x.ProjectId=w.ProjectId AND x.CatalogItemId=i.ItemId AND x.Version=1);

-- 7. Payments. Paid rows use Cash (Bank needs a stored receipt file). The JOB-02/JOB-04 rules:
--    T30 = CurrentTotal x T30 ratio - NRD (cumulative); FINAL = remaining balance.
INSERT dbo.Payments(ProjectId,PaymentType,Status,DueDate,Amount,PaymentMethod,RecordedBy,PaidDate)
SELECT w.ProjectId,v.PaymentType,v.Status,v.DueDate,v.Amount,
       CASE WHEN v.Status=N'Paid' THEN N'Cash' END,CASE WHEN v.Status=N'Paid' THEN @Manager END,
       CASE WHEN v.Status=N'Paid' THEN v.PaidDate END
FROM @Plan p JOIN dbo.WeddingProjects w ON w.ProjectCode=p.Code
CROSS APPLY (VALUES
    (N'NRD',N'Paid',CAST(p.CreatedAt AS DATE),p.Nrd,CAST(p.CreatedAt AS DATE)),
    (N'T30',CASE WHEN p.Status IN(N'Event Completed',N'Settled',N'Closed') THEN N'Paid' ELSE N'Due' END,
            DATEADD(DAY,-30,p.EventDate),p.T30,DATEADD(DAY,-27,p.EventDate)),
    (N'FINAL',CASE WHEN p.Status IN(N'Settled',N'Closed') THEN N'Paid' ELSE N'Due' END,
            DATEADD(DAY,1,p.EventDate),p.BaseValue-p.Nrd-p.T30,DATEADD(DAY,2,p.EventDate))
)v(PaymentType,Status,DueDate,Amount,PaidDate)
WHERE (v.PaymentType=N'NRD'
    OR (v.PaymentType=N'T30' AND (p.Status IN(N'Event Completed',N'Settled',N'Closed') OR (p.Status=N'Baseline Frozen' AND DATEADD(DAY,-30,p.EventDate)<=@Today)))
    OR (v.PaymentType=N'FINAL' AND p.Status IN(N'Event Completed',N'Settled',N'Closed')))
  AND NOT EXISTS(SELECT 1 FROM dbo.Payments x WHERE x.ProjectId=w.ProjectId AND x.PaymentType=v.PaymentType);

-- 8. Roadmap: four milestones per project.
INSERT dbo.RoadmapTasks(ProjectId,Milestone,Title,AssigneeRole,DueDate,Status,CompletedAt)
SELECT w.ProjectId,t.Milestone,t.Title,N'Manager',DATEADD(DAY,t.OffsetDays,p.EventDate),
       CASE WHEN c.DoneAt IS NOT NULL THEN N'Done' ELSE N'Pending' END,c.DoneAt
FROM @Plan p JOIN dbo.WeddingProjects w ON w.ProjectCode=p.Code
CROSS JOIN (VALUES(N'T-90',N'Khởi tạo hợp đồng gốc & thu NRD',-90),(N'T-30',N'Đóng băng baseline & chuẩn bị BEO',-30),
                  (N'T-7',N'Chốt số bàn & nhân sự điều phối',-7),(N'Event Day',N'Vận hành & giám sát tiệc cưới',0))t(Milestone,Title,OffsetDays)
CROSS APPLY (SELECT CASE
    WHEN t.Milestone=N'T-90' THEN p.CreatedAt
    WHEN t.Milestone=N'T-30' THEN p.FrozenAt
    WHEN p.Status IN(N'Event Completed',N'Settled',N'Closed') THEN DATEADD(HOUR,12,CAST(DATEADD(DAY,t.OffsetDays,p.EventDate) AS DATETIME2(0)))
    END DoneAt) c
WHERE NOT EXISTS(SELECT 1 FROM dbo.RoadmapTasks x WHERE x.ProjectId=w.ProjectId AND x.Milestone=t.Milestone);

-- 9. Resource reservations for frozen upcoming events (within each resource's total quantity).
INSERT dbo.ResourceAllocations(ResourceId,HallId,ProjectId,StartTime,EndTime,AllocatedQuantity,Status)
SELECT r.ResourceId,w.HallId,w.ProjectId,
       DATEADD(MINUTE,DATEDIFF(MINUTE,CAST('00:00' AS TIME),s.StartTime)-90,CAST(w.EventDate AS DATETIME2(0))),
       DATEADD(MINUTE,DATEDIFF(MINUTE,CAST('00:00' AS TIME),s.EndTime),CAST(w.EventDate AS DATETIME2(0))),a.Qty,N'Reserved'
FROM @Plan p JOIN dbo.WeddingProjects w ON w.ProjectCode=p.Code
JOIN dbo.Slots s ON s.SlotId=w.SlotId
CROSS JOIN (VALUES(N'Audio',N'Dàn âm thanh line array',1),(N'Follow Light',N'Đèn follow 1200W',2),(N'Technician',N'Kỹ thuật viên âm thanh',2))a(Type,Name,Qty)
JOIN dbo.Resources r ON r.Type=a.Type AND r.Name=a.Name
WHERE p.Status=N'Baseline Frozen'
  AND NOT EXISTS(SELECT 1 FROM dbo.ResourceAllocations x WHERE x.ProjectId=w.ProjectId AND x.ResourceId=r.ResourceId);

COMMIT TRANSACTION;
PRINT N'Extra seed complete.';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
GO

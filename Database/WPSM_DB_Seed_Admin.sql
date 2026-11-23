-- WPSM demo data for the Admin screens (A-01 dashboard, A-09..A-12 reports, A-13/A-14 photo feed,
-- A-15 overdue alerts, A-16/A-17 escalated complaints, A-18 exception requests).
-- Run AFTER WPSM_DB_Schema.sql and WPSM_DB_Seed_Extra.sql (uses the demo projects WPSM-2025-*, WPSM-2026-10x).
-- Re-runnable: every row has a fixed *-DEMO-* code or a NOT EXISTS guard; projects created by users are not touched.
-- Totals stay consistent: an approved CR adds its TotalVariance to CurrentTotal once, and Due/Paid milestones are
-- derived from CurrentTotal (T30 = round(total x 70%) - NRD, FINAL = total - paid).
-- Proof photo files: src/API/App_Data/files/proofs/demo/*.png (ImageFilePath is the storage key).
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
DECLARE @Admin INT=(SELECT TOP 1 UserId FROM dbo.Users WHERE Role=N'Admin' AND Status=N'Active' ORDER BY UserId);
IF @Admin IS NULL THROW 50201,N'Cần một tài khoản Admin đang hoạt động.',1;

DECLARE @P TABLE(Code NVARCHAR(30) PRIMARY KEY, ProjectId INT, ManagerId INT, CustomerId INT, EventDate DATE);
INSERT @P SELECT ProjectCode, ProjectId, ManagerId, CustomerId, EventDate FROM dbo.WeddingProjects
WHERE ProjectCode IN (N'WPSM-2025-001',N'WPSM-2025-004',N'WPSM-2025-011',N'WPSM-2026-101',N'WPSM-2026-102',N'WPSM-2026-105',N'WPSM-2026-106');
IF (SELECT COUNT(*) FROM @P) < 7
    THROW 50202,N'Thiếu dự án demo. Hãy chạy WPSM_DB_Seed_Extra.sql và khởi động API (seeder) trước.',1;

-- 1. Change requests (A-01 KPI, A-12 CR statistics, A-11 approved variance). ------------------------------------
DECLARE @CR TABLE(Code NVARCHAR(30), Project NVARCHAR(30), Type NVARCHAR(30), Description NVARCHAR(500), Status NVARCHAR(20),
                  CreatedAt DATETIME2(0), Cost DECIMAL(18,2), Rate DECIMAL(5,2), Category NVARCHAR(30));
INSERT @CR VALUES
(N'CR-DEMO-01',N'WPSM-2025-001',N'Table count', N'Tăng thêm 5 bàn tiệc do khách mời phát sinh',N'Approved',  '2025-09-20 03:00',17500000,NULL,N'Catering'),
(N'CR-DEMO-02',N'WPSM-2025-001',N'Decor change',N'Đổi hoa cưới sang tông trắng – xanh',        N'Approved',  '2025-10-15 02:30', 4000000,5,   N'Decor'),
(N'CR-DEMO-03',N'WPSM-2026-101',N'Menu change', N'Nâng cấp thực đơn cho toàn bộ bàn tiệc',     N'Approved',  '2026-10-28 04:00',15000000,1,   N'Catering'),
(N'CR-DEMO-04',N'WPSM-2026-102',N'Table count', N'Giảm 3 bàn tiệc theo danh sách khách chốt',  N'Approved',  '2026-11-10 06:00',-10500000,1,  N'Catering'),
(N'CR-DEMO-05',N'WPSM-2026-101',N'Script edit', N'Thêm tiết mục múa lân mở màn',               N'Negotiating','2026-11-14 03:15', 6000000,3,   N'MC'),
(N'CR-DEMO-06',N'WPSM-2026-102',N'Decor change',N'Thay backdrop sân khấu sang mẫu hoa treo',   N'Negotiating','2026-11-15 08:00', 9000000,1,   N'Decor'),
(N'CR-DEMO-07',N'WPSM-2026-105',N'Other',       N'Bổ sung xe đưa đón khách từ khách sạn',      N'Rejected',  '2026-10-30 05:00', 4500000,5,   N'AV'),
(N'CR-DEMO-08',N'WPSM-2025-011',N'Menu change', N'Đổi món tráng miệng sang chè khúc bạch',     N'Rejected',  '2025-09-20 07:45', 2000000,3,   N'Catering'),
(N'CR-DEMO-09',N'WPSM-2026-102',N'Menu change', N'Thêm món chay cho 2 bàn',                    N'Draft',     '2026-11-16 09:00', 3000000,NULL,N'Catering'),
(N'CR-DEMO-10',N'WPSM-2026-105',N'Table count', N'Thêm 2 bàn tiệc cho đoàn khách họ nhà trai', N'Approved',  '2026-10-01 03:00', 7000000,1,   N'Catering');

DECLARE @NewCR TABLE(CRId INT, Code NVARCHAR(30));
INSERT dbo.ChangeRequests(CRCode,ProjectId,CreatedBy,Type,Description,CostImpact,PenaltyRate,PenaltyFee,Status,CustomerApprovedAt,ManagerApprovedAt,CreatedAt)
OUTPUT inserted.CRId, inserted.CRCode INTO @NewCR
SELECT c.Code, p.ProjectId, CASE WHEN c.Type IN (N'Table count',N'Menu change') THEN p.CustomerId ELSE p.ManagerId END,
       c.Type, c.Description, c.Cost, c.Rate, ROUND(ABS(c.Cost)*ISNULL(c.Rate,0)/100,0), c.Status,
       CASE WHEN c.Status IN (N'Approved',N'Negotiating') THEN DATEADD(DAY,1,c.CreatedAt) END,
       CASE WHEN c.Status=N'Approved' THEN DATEADD(DAY,2,c.CreatedAt) END,
       c.CreatedAt
FROM @CR c JOIN @P p ON p.Code=c.Project
WHERE NOT EXISTS(SELECT 1 FROM dbo.ChangeRequests x WHERE x.CRCode=c.Code);

-- CR lines against the baseline spec item of the CR category (Table count changes quantity, others the unit price).
INSERT dbo.CRItems(CRId,TargetSpecItemId,OldQuantity,OldUnitPrice,NewQuantity,NewUnitPrice)
SELECT n.CRId, s.SpecItemId, s.Quantity, s.UnitPrice,
       CASE WHEN c.Type=N'Table count' THEN s.Quantity + CAST(c.Cost/s.UnitPrice AS INT) ELSE s.Quantity END,
       CASE WHEN c.Type=N'Table count' THEN s.UnitPrice ELSE s.UnitPrice + ROUND(c.Cost/s.Quantity,2) END
FROM @NewCR n JOIN @CR c ON c.Code=n.Code JOIN @P p ON p.Code=c.Project
CROSS APPLY (SELECT TOP 1 si.SpecItemId, si.Quantity, si.UnitPrice FROM dbo.SpecItems si
             JOIN dbo.ServiceCatalogItems ci ON ci.ItemId=si.CatalogItemId
             WHERE si.ProjectId=p.ProjectId AND si.Version=1 AND ci.Category=c.Category ORDER BY si.SpecItemId) s;

-- Approved CRs change the contract total exactly once (only rows inserted by this run).
UPDATE w SET CurrentTotal = w.CurrentTotal + v.Variance
FROM dbo.WeddingProjects w
JOIN (SELECT cr.ProjectId, SUM(cr.CostImpact + cr.PenaltyFee) Variance FROM dbo.ChangeRequests cr JOIN @NewCR n ON n.CRId=cr.CRId
      WHERE cr.Status=N'Approved' GROUP BY cr.ProjectId) v ON v.ProjectId=w.ProjectId;

-- Addenda for approved CRs (C-09 / A-11).
INSERT dbo.ContractAddenda(AddendumCode,CRId,ProjectId,TotalVariance,FilePath,SignedAt,Status)
SELECT REPLACE(cr.CRCode,N'CR-',N'AD-'), cr.CRId, cr.ProjectId, cr.CostImpact + cr.PenaltyFee, NULL,
       CASE WHEN cr.CRCode IN (N'CR-DEMO-01',N'CR-DEMO-02',N'CR-DEMO-10') THEN DATEADD(DAY,3,cr.CreatedAt) END,
       CASE WHEN cr.CRCode IN (N'CR-DEMO-01',N'CR-DEMO-02',N'CR-DEMO-10') THEN N'Signed' ELSE N'Generated' END
FROM dbo.ChangeRequests cr
WHERE cr.CRCode LIKE N'CR-DEMO-%' AND cr.Status=N'Approved'
  AND NOT EXISTS(SELECT 1 FROM dbo.ContractAddenda a WHERE a.CRId=cr.CRId);

-- 2. Payments consistent with the new totals (A-11 net revenue). -------------------------------------------------
-- Settled / closed sample projects get their T30 and FINAL paid; Event Completed 2025-001 has T30 paid, FINAL due.
INSERT dbo.Payments(ProjectId,PaymentType,Status,DueDate,Amount,PaymentMethod,RecordedBy,PaidDate)
SELECT p.ProjectId, v.Type, v.Status, v.DueDate, v.Amount, CASE WHEN v.Status=N'Paid' THEN N'Cash' END,
       CASE WHEN v.Status=N'Paid' THEN p.ManagerId END, CASE WHEN v.Status=N'Paid' THEN v.PaidDate END
FROM @P p JOIN dbo.WeddingProjects w ON w.ProjectId=p.ProjectId
CROSS APPLY (VALUES
   (N'T30',  CASE WHEN p.Code=N'WPSM-2025-001' THEN N'Paid' ELSE N'Paid' END, DATEADD(DAY,-30,p.EventDate),
             ROUND((w.CurrentTotal - CASE WHEN p.Code=N'WPSM-2025-001' THEN 4200000 ELSE 0 END)*0.7,0) - w.NRDAmount, DATEADD(DAY,-28,p.EventDate)),
   (N'FINAL',CASE WHEN p.Code=N'WPSM-2025-001' THEN N'Due' ELSE N'Paid' END, DATEADD(DAY,1,p.EventDate), CAST(NULL AS DECIMAL(18,2)), DATEADD(DAY,2,p.EventDate))
) v(Type,Status,DueDate,Amount,PaidDate)
WHERE p.Code IN (N'WPSM-2025-001',N'WPSM-2025-004',N'WPSM-2025-011')
  AND NOT EXISTS(SELECT 1 FROM dbo.Payments x WHERE x.ProjectId=p.ProjectId AND x.PaymentType=v.Type)
  AND v.Type=N'T30';
-- FINAL = remaining balance (inserted after T30 so the paid sum is known).
INSERT dbo.Payments(ProjectId,PaymentType,Status,DueDate,Amount,PaymentMethod,RecordedBy,PaidDate)
SELECT p.ProjectId, N'FINAL', CASE WHEN p.Code=N'WPSM-2025-001' THEN N'Due' ELSE N'Paid' END, DATEADD(DAY,1,p.EventDate),
       w.CurrentTotal - ISNULL((SELECT SUM(x.Amount) FROM dbo.Payments x WHERE x.ProjectId=p.ProjectId AND x.Status=N'Paid'),0),
       CASE WHEN p.Code<>N'WPSM-2025-001' THEN N'Cash' END, CASE WHEN p.Code<>N'WPSM-2025-001' THEN p.ManagerId END,
       CASE WHEN p.Code<>N'WPSM-2025-001' THEN DATEADD(DAY,2,p.EventDate) END
FROM @P p JOIN dbo.WeddingProjects w ON w.ProjectId=p.ProjectId
WHERE p.Code IN (N'WPSM-2025-001',N'WPSM-2025-004',N'WPSM-2025-011')
  AND NOT EXISTS(SELECT 1 FROM dbo.Payments x WHERE x.ProjectId=p.ProjectId AND x.PaymentType=N'FINAL');
-- Due milestones follow the current total (idempotent: derived values).
UPDATE pay SET Amount = ROUND(w.CurrentTotal*0.7,0) - w.NRDAmount
FROM dbo.Payments pay JOIN dbo.WeddingProjects w ON w.ProjectId=pay.ProjectId
WHERE w.ProjectCode=N'WPSM-2026-101' AND pay.PaymentType=N'T30' AND pay.Status=N'Due';
UPDATE pay SET Amount = w.CurrentTotal - ISNULL((SELECT SUM(x.Amount) FROM dbo.Payments x WHERE x.ProjectId=w.ProjectId AND x.Status=N'Paid'),0)
FROM dbo.Payments pay JOIN dbo.WeddingProjects w ON w.ProjectId=pay.ProjectId
WHERE w.ProjectCode IN (N'WPSM-2026-105',N'WPSM-2025-001') AND pay.PaymentType=N'FINAL' AND pay.Status=N'Due';

-- 3. BEOs (complaints need one; Stale BEOs show in the Manager alert panel). -------------------------------------
DECLARE @BEO TABLE(Project NVARCHAR(30), Version INT, Status NVARCHAR(20), FrozenAt DATETIME2(0), Notes NVARCHAR(500));
INSERT @BEO VALUES
(N'WPSM-2025-001',1,N'Stale', '2025-09-25 03:00',N'Bản đầu, lỗi thời sau CR-DEMO-02'),
(N'WPSM-2025-001',2,N'Frozen','2025-10-18 03:00',N'Đã cập nhật hoa tông trắng – xanh'),
(N'WPSM-2025-004',1,N'Frozen','2025-08-25 03:00',NULL),
(N'WPSM-2025-011',1,N'Frozen','2025-09-10 03:00',NULL),
(N'WPSM-2026-105',1,N'Stale', '2026-09-20 03:00',N'Lỗi thời sau CR-DEMO-10'),
(N'WPSM-2026-105',2,N'Frozen','2026-10-05 03:00',N'Thêm 2 bàn cho đoàn nhà trai'),
(N'WPSM-2026-106',1,N'Frozen','2026-09-25 03:00',NULL),
(N'WPSM-2026-101',1,N'Stale', '2026-10-20 03:00',N'Cần duyệt lại sau CR-DEMO-03'),
(N'WPSM-2026-102',1,N'Draft', NULL,               N'Đang chờ chốt danh sách bàn');
INSERT dbo.BEOs(ProjectId,Version,Status,FrozenAt,ApprovedBy,Notes)
SELECT p.ProjectId, b.Version, b.Status, b.FrozenAt, CASE WHEN b.Status<>N'Draft' THEN p.ManagerId END, b.Notes
FROM @BEO b JOIN @P p ON p.Code=b.Project
WHERE NOT EXISTS(SELECT 1 FROM dbo.BEOs x WHERE x.ProjectId=p.ProjectId AND x.Version=b.Version);

-- 4. Complaints (A-16/A-17 escalations, M-18). ---------------------------------------------------------------------
DECLARE @CPL TABLE(Code NVARCHAR(30), Project NVARCHAR(30), BeoVersion INT, Description NVARCHAR(1000), Status NVARCHAR(20),
                   EscalationNote NVARCHAR(500), ResolutionNote NVARCHAR(1000), CreatedAt DATETIME2(0), EscalatedAt DATETIME2(0), ResolvedAt DATETIME2(0), ByAdmin BIT);
INSERT @CPL VALUES
(N'CPL-DEMO-01',N'WPSM-2025-001',2,N'Món chính ra chậm khoảng 30 phút so với kịch bản đã chốt.',N'Escalated',
   N'Khách yêu cầu giảm 10% phần ẩm thực, vượt thẩm quyền Manager.',NULL,'2025-10-26 02:00','2025-10-27 03:00',NULL,0),
(N'CPL-DEMO-02',N'WPSM-2026-105',2,N'Âm thanh bị rè trong phần phát biểu của hai họ.',N'Escalated',
   N'Khách đề nghị hoàn tiền gói âm thanh ánh sáng.',NULL,'2026-11-09 02:00','2026-11-10 04:00',NULL,0),
(N'CPL-DEMO-03',N'WPSM-2026-106',1,N'Hoa trên bàn tiệc khác mẫu đã chốt trong BEO.',N'Resolved',
   N'Khách yêu cầu bồi thường, cần Admin quyết định.',N'Nhà hàng xin lỗi và tặng voucher 2.000.000 đ cho lần đặt tiệc sau.','2026-10-19 02:00','2026-10-20 03:00','2026-10-22 03:00',1),
(N'CPL-DEMO-04',N'WPSM-2026-105',2,N'Thiếu nhân viên phục vụ ở dãy bàn số 12 trong 15 phút đầu.',N'Open',NULL,NULL,'2026-11-09 05:00',NULL,NULL,0),
(N'CPL-DEMO-05',N'WPSM-2025-011',1,N'Bánh cưới giao trễ 20 phút so với giờ cắt bánh.',N'Resolved',
   NULL,N'Manager đã xin lỗi và giảm 50% phí tiệc ngọt.','2025-10-06 02:00',NULL,'2025-10-07 03:00',0);
INSERT dbo.Complaints(ComplaintCode,ProjectId,BEOId,Description,Status,EscalationNote,ResolutionNote,CreatedAt,EscalatedAt,ResolvedBy,ResolvedAt)
SELECT c.Code, p.ProjectId, b.BEOId, c.Description, c.Status, c.EscalationNote, c.ResolutionNote, c.CreatedAt, c.EscalatedAt,
       CASE WHEN c.Status=N'Resolved' THEN CASE WHEN c.ByAdmin=1 THEN @Admin ELSE p.ManagerId END END, c.ResolvedAt
FROM @CPL c JOIN @P p ON p.Code=c.Project JOIN dbo.BEOs b ON b.ProjectId=p.ProjectId AND b.Version=c.BeoVersion
WHERE NOT EXISTS(SELECT 1 FROM dbo.Complaints x WHERE x.ComplaintCode=c.Code);

-- 5. Ratings for finished projects (A-01 satisfaction). ------------------------------------------------------------
INSERT dbo.Ratings(ProjectId,Module,Stars,Comment,CreatedAt)
SELECT p.ProjectId, r.Module, r.Stars, r.Comment, DATEADD(DAY,3,CAST(p.EventDate AS DATETIME2(0)))
FROM @P p JOIN (VALUES
  (N'WPSM-2026-106',N'Hall',5,N'Sảnh rộng, sạch sẽ'),(N'WPSM-2026-106',N'Menu',4,N'Món ăn ngon, ra món hơi chậm'),
  (N'WPSM-2026-106',N'Decor',3,N'Hoa bàn khác mẫu'),(N'WPSM-2026-106',N'MC',5,N'MC dẫn dắt tốt'),
  (N'WPSM-2025-011',N'Hall',4,NULL),(N'WPSM-2025-011',N'Menu',5,N'Rất hài lòng'),(N'WPSM-2025-011',N'Decor',4,NULL),(N'WPSM-2025-011',N'MC',4,NULL),
  (N'WPSM-2025-004',N'Hall',5,N'Sảnh Hoàng Gia rất sang trọng'),(N'WPSM-2025-004',N'Menu',5,NULL),(N'WPSM-2025-004',N'Decor',4,NULL),(N'WPSM-2025-004',N'MC',5,NULL)
) r(Project,Module,Stars,Comment) ON r.Project=p.Code
WHERE NOT EXISTS(SELECT 1 FROM dbo.Ratings x WHERE x.ProjectId=p.ProjectId AND x.Module=r.Module);

-- 6. Day-of tasks for 2026-105 / 2026-106 (A-15 overdue, A-13 photos). Deadlines are UTC (Vietnam time - 7h). ---------
-- Overdue rows are stored already flagged (OverdueFlaggedAt) so JOB-05 does not alert or email again.
DECLARE @T TABLE(Project NVARCHAR(30), TaskName NVARCHAR(200), DeadlineUtc DATETIME2(0), Status NVARCHAR(20), PhotoRequired BIT, CompletedUtc DATETIME2(0));
INSERT @T VALUES
(N'WPSM-2026-105',N'Kiểm tra âm thanh & ánh sáng',  '2026-11-08 03:00',N'Done',   1,'2026-11-08 02:50'),
(N'WPSM-2026-105',N'Set up hoa & backdrop sân khấu','2026-11-08 03:30',N'Done',   1,'2026-11-08 04:10'),
(N'WPSM-2026-105',N'Đón khách & bàn lễ tân',        '2026-11-08 04:00',N'Done',   0,'2026-11-08 03:55'),
(N'WPSM-2026-105',N'Phục vụ món chính',             '2026-11-08 05:30',N'Overdue',0,NULL),
(N'WPSM-2026-105',N'Cắt bánh & rót rượu',           '2026-11-08 06:00',N'Overdue',1,NULL),
(N'WPSM-2026-106',N'Kiểm tra âm thanh & ánh sáng',  '2026-10-18 03:00',N'Done',   1,'2026-10-18 02:40'),
(N'WPSM-2026-106',N'Set up hoa bàn tiệc',           '2026-10-18 03:30',N'Done',   1,'2026-10-18 03:20'),
(N'WPSM-2026-106',N'Đón khách & bàn lễ tân',        '2026-10-18 04:00',N'Done',   0,'2026-10-18 03:58'),
(N'WPSM-2026-106',N'Cắt bánh & rót rượu',           '2026-10-18 06:00',N'Done',   1,'2026-10-18 06:15');
INSERT dbo.DayOfTasks(ProjectId,TaskName,DeadlineTime,CompletedAt,CompletedBy,OverdueFlaggedAt,Status,PhotoRequired)
SELECT p.ProjectId, t.TaskName, t.DeadlineUtc, t.CompletedUtc, CASE WHEN t.Status=N'Done' THEN p.ManagerId END,
       CASE WHEN t.Status=N'Overdue' OR t.CompletedUtc>t.DeadlineUtc THEN DATEADD(MINUTE,5,t.DeadlineUtc) END, t.Status, t.PhotoRequired
FROM @T t JOIN @P p ON p.Code=t.Project
WHERE NOT EXISTS(SELECT 1 FROM dbo.DayOfTasks x WHERE x.ProjectId=p.ProjectId AND x.TaskName=t.TaskName);

-- 7. Proof photos (1-3 per proof task). Keys resolve under Files:RootPath (src/API/App_Data/files by default). --------
DECLARE @Ph TABLE(Project NVARCHAR(30), TaskName NVARCHAR(200), FileKey NVARCHAR(500), UploadedUtc DATETIME2(0));
INSERT @Ph VALUES
(N'WPSM-2026-105',N'Kiểm tra âm thanh & ánh sáng',  N'proofs/demo/105_1.png','2026-11-08 02:48'),
(N'WPSM-2026-105',N'Set up hoa & backdrop sân khấu',N'proofs/demo/105_2.png','2026-11-08 04:08'),
(N'WPSM-2026-105',N'Set up hoa & backdrop sân khấu',N'proofs/demo/105_3.png','2026-11-08 04:09'),
(N'WPSM-2026-106',N'Kiểm tra âm thanh & ánh sáng',  N'proofs/demo/106_1.png','2026-10-18 02:38'),
(N'WPSM-2026-106',N'Set up hoa bàn tiệc',           N'proofs/demo/106_2.png','2026-10-18 03:15'),
(N'WPSM-2026-106',N'Set up hoa bàn tiệc',           N'proofs/demo/106_3.png','2026-10-18 03:16'),
(N'WPSM-2026-106',N'Set up hoa bàn tiệc',           N'proofs/demo/106_4.png','2026-10-18 03:18'),
(N'WPSM-2026-106',N'Cắt bánh & rót rượu',           N'proofs/demo/106_5.png','2026-10-18 06:14');
INSERT dbo.ExecutionPhotos(DayOfTaskId,UploaderId,ImageFilePath,UploadedAt)
SELECT d.DayOfTaskId, p.ManagerId, ph.FileKey, ph.UploadedUtc
FROM @Ph ph JOIN @P p ON p.Code=ph.Project JOIN dbo.DayOfTasks d ON d.ProjectId=p.ProjectId AND d.TaskName=ph.TaskName
WHERE NOT EXISTS(SELECT 1 FROM dbo.ExecutionPhotos x WHERE x.ImageFilePath=ph.FileKey);
-- Sample projects from the API seeder: one photo on their first done proof task.
INSERT dbo.ExecutionPhotos(DayOfTaskId,UploaderId,ImageFilePath,UploadedAt)
SELECT d.DayOfTaskId, p.ManagerId, f.FileKey, ISNULL(d.CompletedAt, d.DeadlineTime)
FROM (VALUES(N'WPSM-2025-001',N'proofs/demo/001_1.png'),(N'WPSM-2025-011',N'proofs/demo/011_1.png'),(N'WPSM-2025-004',N'proofs/demo/004_1.png')) f(Project,FileKey)
JOIN @P p ON p.Code=f.Project
CROSS APPLY (SELECT TOP 1 x.DayOfTaskId, x.CompletedAt, x.DeadlineTime FROM dbo.DayOfTasks x
             WHERE x.ProjectId=p.ProjectId AND x.Status=N'Done' ORDER BY x.DeadlineTime) d
WHERE NOT EXISTS(SELECT 1 FROM dbo.ExecutionPhotos x WHERE x.ImageFilePath=f.FileKey);

-- 8. Exception requests (A-18): penalty waivers and resource conflicts. --------------------------------------------
INSERT dbo.ExceptionRequests(RequestCode,SourceType,ProjectId,CRId,AllocationId,RequestedBy,RequestNote,Status,DecisionBy,DecisionNote,DecidedAt,CreatedAt)
SELECT e.Code, N'CrPenaltyWaiver', cr.ProjectId, cr.CRId, NULL, w.ManagerId, e.Note, e.Status,
       CASE WHEN e.Status<>N'Pending' THEN @Admin END, e.Decision, CASE WHEN e.Status<>N'Pending' THEN DATEADD(DAY,1,cr.CreatedAt) END,
       DATEADD(HOUR,4,cr.CreatedAt)
FROM (VALUES
  (N'EXC-DEMO-01',N'CR-DEMO-05',N'Khách thân thiết, xin miễn phí phạt thay đổi kịch bản.',N'Pending',CAST(NULL AS NVARCHAR(300))),
  (N'EXC-DEMO-02',N'CR-DEMO-06',N'Lỗi do nhà cung cấp backdrop, đề nghị miễn phí phạt.',N'Pending',NULL),
  (N'EXC-DEMO-03',N'CR-DEMO-07',N'Xin miễn phí phạt do khách đổi lịch đón.',N'Rejected',N'Không thuộc trường hợp được miễn theo chính sách.')
) e(Code,CrCode,Note,Status,Decision)
JOIN dbo.ChangeRequests cr ON cr.CRCode=e.CrCode JOIN dbo.WeddingProjects w ON w.ProjectId=cr.ProjectId
WHERE NOT EXISTS(SELECT 1 FROM dbo.ExceptionRequests x WHERE x.RequestCode=e.Code);
INSERT dbo.ExceptionRequests(RequestCode,SourceType,ProjectId,CRId,AllocationId,RequestedBy,RequestNote,Status,CreatedAt)
SELECT e.Code, N'ResourceConflict', a.ProjectId, NULL, a.AllocationId, w.ManagerId, e.Note, N'Pending', SYSUTCDATETIME()
FROM (VALUES(N'EXC-DEMO-04',N'WPSM-2026-101',N'Audio',N'Dàn âm thanh trùng ca với sảnh bên cạnh, xin dùng vượt số lượng.'),
            (N'EXC-DEMO-05',N'WPSM-2026-102',N'Follow Light',N'Thiếu đèn follow cho phần rước dâu, xin mượn thêm 1 bộ.')) e(Code,Project,ResourceType,Note)
JOIN dbo.WeddingProjects w ON w.ProjectCode=e.Project
CROSS APPLY (SELECT TOP 1 ra.AllocationId, ra.ProjectId FROM dbo.ResourceAllocations ra JOIN dbo.Resources r ON r.ResourceId=ra.ResourceId
             WHERE ra.ProjectId=w.ProjectId AND r.Type=e.ResourceType ORDER BY ra.AllocationId) a
WHERE NOT EXISTS(SELECT 1 FROM dbo.ExceptionRequests x WHERE x.RequestCode=e.Code);

COMMIT TRANSACTION;
PRINT N'Admin demo seed complete.';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
GO

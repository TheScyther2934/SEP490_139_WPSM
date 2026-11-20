-- ============================================================================
-- 2026-11-20  Coordination tasks per department, with proof from the lock onwards
--
-- DayOfTasks now holds every coordination task of an event, not only the event-day timeline:
--   preparation tasks generated from the frozen coordination sheet (build the stage, receive the furniture...),
--   the event-day timeline, and tasks the operator adds by hand. They can be completed from the lock until settlement.
--   Department  : Kitchen / Service / Decor / Tech-MC
--   SourceKey   : what generated the task (spec:<id>, addon:<id>, dayof:<n>); NULL for a task added by hand
--   Note        : operator note (what was done, who delivered)
--   CreatedBy   : operator who added the task by hand
-- ExecutionPhotos also takes PDF proof (delivery note, acceptance record):
--   FileName, ContentType (NULL = legacy rows, all images)
--
-- Run with: sqlcmd -S localhost -E -b -I -f 65001 -d <db> -i <this file>
-- Idempotent: every block checks the current state first.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'dbo.DayOfTasks', N'Department') IS NULL
    ALTER TABLE dbo.DayOfTasks ADD Department NVARCHAR(20) NOT NULL CONSTRAINT DF_DayOf_Department DEFAULT (N'Service');
GO
IF COL_LENGTH(N'dbo.DayOfTasks', N'SourceKey') IS NULL
    ALTER TABLE dbo.DayOfTasks ADD SourceKey NVARCHAR(50) NULL;
GO
IF COL_LENGTH(N'dbo.DayOfTasks', N'Note') IS NULL
    ALTER TABLE dbo.DayOfTasks ADD Note NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.DayOfTasks', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.DayOfTasks ADD CreatedBy INT NULL;
    ALTER TABLE dbo.DayOfTasks ADD CONSTRAINT FK_DayOf_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DayOf_Department')
    ALTER TABLE dbo.DayOfTasks ADD CONSTRAINT CK_DayOf_Department CHECK (Department IN (N'Kitchen', N'Service', N'Decor', N'Tech-MC'));
GO

-- Existing event-day timelines: department and source of the five template tasks, so they are not generated twice.
UPDATE t SET Department = m.Department, SourceKey = m.SourceKey
FROM dbo.DayOfTasks t
JOIN (VALUES (N'Dọn sảnh và sắp xếp bàn tiệc', N'Service', N'dayof:0'),
             (N'Hoàn thiện trang trí sân khấu', N'Decor', N'dayof:1'),
             (N'Kiểm tra âm thanh, ánh sáng', N'Tech-MC', N'dayof:2'),
             (N'Bàn đón khách và đồ uống chào mừng', N'Service', N'dayof:3'),
             (N'Bắt đầu nghi lễ, tín hiệu cho MC', N'Tech-MC', N'dayof:4')) m(TaskName, Department, SourceKey)
  ON m.TaskName = t.TaskName
WHERE t.SourceKey IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.DayOfTasks o WHERE o.ProjectId = t.ProjectId AND o.SourceKey = m.SourceKey);
GO
-- Other legacy tasks: guess the department from the name.
UPDATE dbo.DayOfTasks SET Department = CASE
        WHEN TaskName LIKE N'%trang trí%' OR TaskName LIKE N'%hoa%' OR TaskName LIKE N'%sân khấu%' THEN N'Decor'
        WHEN TaskName LIKE N'%âm thanh%' OR TaskName LIKE N'%ánh sáng%' OR TaskName LIKE N'%MC%' OR TaskName LIKE N'%múa%' THEN N'Tech-MC'
        WHEN TaskName LIKE N'%bếp%' OR TaskName LIKE N'%món%' OR TaskName LIKE N'%thực đơn%' THEN N'Kitchen'
        ELSE N'Service' END
WHERE SourceKey IS NULL AND Department = N'Service';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_DayOf_Project_Source' AND object_id = OBJECT_ID(N'dbo.DayOfTasks'))
    CREATE UNIQUE INDEX UQ_DayOf_Project_Source ON dbo.DayOfTasks(ProjectId, SourceKey) WHERE SourceKey IS NOT NULL;
GO

IF COL_LENGTH(N'dbo.ExecutionPhotos', N'FileName') IS NULL
    ALTER TABLE dbo.ExecutionPhotos ADD FileName NVARCHAR(255) NULL;
GO
IF COL_LENGTH(N'dbo.ExecutionPhotos', N'ContentType') IS NULL
    ALTER TABLE dbo.ExecutionPhotos ADD ContentType VARCHAR(100) NULL;
GO
PRINT N'20261120_CoordinationTasks applied.';
GO

-- Additive schema compatibility for CRItems, Complaints and DayOfTasks
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CRItems') AND name = N'CatalogItemId' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.CRItems ALTER COLUMN CatalogItemId INT NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Complaints') AND name = N'CustomerId' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.Complaints ALTER COLUMN CustomerId INT NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Complaints') AND name = N'Title' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.Complaints ALTER COLUMN Title NVARCHAR(200) NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DayOfTasks') AND name = N'Department' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.DayOfTasks ALTER COLUMN Department NVARCHAR(50) NULL;
END
GO

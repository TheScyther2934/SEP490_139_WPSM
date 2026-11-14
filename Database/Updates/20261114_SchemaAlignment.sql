-- Additive columns required by the current EF model but absent from WPSM_DB_Schema.sql.
-- Without them every read of Complaints fails with "Invalid column name 'CustomerId'/'Title'",
-- which breaks M-02/M-04 project reads and the complaint screens (C-14..C-16, M-18..M-20).
-- Idempotent: safe to run on fresh and existing databases; no data is deleted.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Complaints', 'CustomerId') IS NULL
    ALTER TABLE dbo.Complaints ADD CustomerId INT NULL;
GO

IF COL_LENGTH('dbo.Complaints', 'Title') IS NULL
    ALTER TABLE dbo.Complaints ADD Title NVARCHAR(200) NULL;
GO

IF COL_LENGTH('dbo.CRItems', 'CatalogItemId') IS NULL
    ALTER TABLE dbo.CRItems ADD CatalogItemId INT NULL;
GO

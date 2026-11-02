-- Additive update. Run against the WPSM database used by the application.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF COL_LENGTH(N'dbo.Payments', N'Status') IS NULL
    ALTER TABLE dbo.Payments ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Payments_PortalStatus DEFAULT 'Paid';
IF COL_LENGTH(N'dbo.Payments', N'DueDate') IS NULL
    ALTER TABLE dbo.Payments ADD DueDate DATE NULL;
IF COL_LENGTH(N'dbo.RoadmapTasks', N'RemindedAt') IS NULL
    ALTER TABLE dbo.RoadmapTasks ADD RemindedAt DATETIME2 NULL;
IF COL_LENGTH(N'dbo.SpecItems', N'LineTotal') IS NULL
    ALTER TABLE dbo.SpecItems ADD LineTotal AS (Quantity * UnitPrice) PERSISTED;
IF COL_LENGTH(N'dbo.ChangeRequests', N'PenaltyRate') IS NULL
    ALTER TABLE dbo.ChangeRequests ADD PenaltyRate DECIMAL(5,2) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'ComplaintCode') IS NULL
    ALTER TABLE dbo.Complaints ADD ComplaintCode NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'EscalationNote') IS NULL
    ALTER TABLE dbo.Complaints ADD EscalationNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'CreatedAt') IS NULL
    ALTER TABLE dbo.Complaints ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Complaints_PortalCreated DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'dbo.CRHistories', N'CreatedAt') IS NULL
    ALTER TABLE dbo.CRHistories ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CRHistories_PortalCreated DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'dbo.ExecutionPhotos', N'UploadedAt') IS NULL
    ALTER TABLE dbo.ExecutionPhotos ADD UploadedAt DATETIME2 NOT NULL CONSTRAINT DF_ExecutionPhotos_PortalCreated DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'dbo.Notifications', N'CreatedAt') IS NULL
    ALTER TABLE dbo.Notifications ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Notifications_PortalCreated DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'dbo.Ratings', N'CreatedAt') IS NULL
    ALTER TABLE dbo.Ratings ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Ratings_PortalCreated DEFAULT SYSUTCDATETIME();
-- Older SQL Server deployments required receipt/payment fields before a bank
-- receipt could be uploaded. Allow the pending payment state used by the portal.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.WeddingProjects') AND name=N'NRDReceiptUrl' AND is_nullable=0)
    ALTER TABLE dbo.WeddingProjects ALTER COLUMN NRDReceiptUrl NVARCHAR(500) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Payments') AND name=N'PaymentMethod' AND is_nullable=0)
    ALTER TABLE dbo.Payments ALTER COLUMN PaymentMethod NVARCHAR(50) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Payments') AND name=N'ReceiptFilePath' AND is_nullable=0)
    ALTER TABLE dbo.Payments ALTER COLUMN ReceiptFilePath NVARCHAR(500) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Payments') AND name=N'RecordedBy' AND is_nullable=0)
    ALTER TABLE dbo.Payments ALTER COLUMN RecordedBy INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Payments') AND name=N'PaidDate' AND is_nullable=0)
    ALTER TABLE dbo.Payments ALTER COLUMN PaidDate DATE NULL;
IF COL_LENGTH(N'dbo.Users', N'RefreshToken') IS NULL
    ALTER TABLE dbo.Users ADD RefreshToken NVARCHAR(255) NULL;
IF COL_LENGTH(N'dbo.Users', N'RefreshTokenExpiryTime') IS NULL
    ALTER TABLE dbo.Users ADD RefreshTokenExpiryTime DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.Payments', N'ReceiptContent') IS NULL
    ALTER TABLE dbo.Payments ADD ReceiptContent VARBINARY(MAX) NULL;
IF COL_LENGTH(N'dbo.Payments', N'ReceiptFileName') IS NULL
    ALTER TABLE dbo.Payments ADD ReceiptFileName NVARCHAR(255) NULL;
IF COL_LENGTH(N'dbo.Payments', N'ReceiptContentType') IS NULL
    ALTER TABLE dbo.Payments ADD ReceiptContentType NVARCHAR(100) NULL;
IF OBJECT_ID(N'dbo.EmailDeliveries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailDeliveries (
        EmailDeliveryId INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId INT NOT NULL UNIQUE REFERENCES dbo.WeddingProjects(ProjectId),
        Recipient NVARCHAR(100) NOT NULL,
        ProtectedPayload NVARCHAR(MAX) NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        Attempts INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL,
        NextAttemptAt DATETIME2 NOT NULL,
        SentAt DATETIME2 NULL,
        CONSTRAINT CK_EmailDeliveries_Status CHECK (Status IN ('Pending','Sent','Failed'))
    );
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'dbo.WeddingProjects') AND name=N'UX_Projects_PortalHallSlotDate'
)
AND NOT EXISTS (
    SELECT HallId,SlotId,EventDate FROM dbo.WeddingProjects GROUP BY HallId,SlotId,EventDate HAVING COUNT(*)>1
)
    CREATE UNIQUE INDEX UX_Projects_PortalHallSlotDate ON dbo.WeddingProjects(HallId,SlotId,EventDate);
GO

-- Add integration tables and baseline protection without deleting existing data.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- H. INTEGRATION ENTITIES (API-01..04, JOB-07; SDS 3.27 and 3.28)
-- ============================================================================

IF OBJECT_ID(N'dbo.StoredFiles', N'U') IS NULL
CREATE TABLE dbo.StoredFiles (
    FileId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StoredFiles PRIMARY KEY CONSTRAINT DF_StoredFiles_Id DEFAULT NEWSEQUENTIALID(),
    ProjectId    INT            NULL CONSTRAINT FK_StoredFiles_Project REFERENCES dbo.WeddingProjects(ProjectId),
    Purpose      VARCHAR(20)    NOT NULL,
    StorageKey   NVARCHAR(300)  NOT NULL CONSTRAINT UQ_StoredFiles_Key UNIQUE,
    OriginalName NVARCHAR(200)  NOT NULL,
    ContentType  VARCHAR(100)   NOT NULL,
    SizeBytes    BIGINT         NOT NULL,
    UploadedBy   INT            NULL CONSTRAINT FK_StoredFiles_User REFERENCES dbo.Users(UserId),
    UploadedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_StoredFiles_UploadedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_StoredFiles_Purpose CHECK (Purpose IN ('NrdReceipt','PaymentReceipt','ProofPhoto','Thumbnail','AddendumPdf','SignedAddendum','BeoSheet','SettlementReceipt')),
    CONSTRAINT CK_StoredFiles_Size CHECK (SizeBytes <= 10485760)
);
GO

IF OBJECT_ID(N'dbo.EmailOutbox', N'U') IS NULL
CREATE TABLE dbo.EmailOutbox (
    EmailId           BIGINT IDENTITY(1,1) CONSTRAINT PK_EmailOutbox PRIMARY KEY,
    ToEmail           NVARCHAR(100)  NOT NULL,
    Subject           NVARCHAR(200)  NOT NULL,
    BodyHtml          NVARCHAR(MAX)  NOT NULL,
    Kind              VARCHAR(20)    NOT NULL,
    Status            VARCHAR(10)    NOT NULL CONSTRAINT DF_EmailOutbox_Status DEFAULT 'Queued',
    RetryCount        INT            NOT NULL CONSTRAINT DF_EmailOutbox_Retry DEFAULT 0,
    NextAttemptAt     DATETIME2(0)   NOT NULL,
    ProviderMessageId VARCHAR(100)   NULL,
    LastError         NVARCHAR(300)  NULL,
    RelatedProjectId  INT            NULL CONSTRAINT FK_EmailOutbox_Project REFERENCES dbo.WeddingProjects(ProjectId),
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_EmailOutbox_CreatedAt DEFAULT SYSUTCDATETIME(),
    SentAt            DATETIME2(0)   NULL,
    DeliveredAt       DATETIME2(0)   NULL,
    CONSTRAINT CK_EmailOutbox_Kind CHECK (Kind IN ('Credentials','Reminder','PaymentRequest','OverdueAlert')),
    CONSTRAINT CK_EmailOutbox_Status CHECK (Status IN ('Queued','Sent','Failed'))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_EmailOutbox_Dispatch' AND object_id=OBJECT_ID('dbo.EmailOutbox'))
    CREATE INDEX IX_EmailOutbox_Dispatch ON dbo.EmailOutbox(Status, NextAttemptAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_EmailOutbox_Provider' AND object_id=OBJECT_ID('dbo.EmailOutbox'))
    CREATE INDEX IX_EmailOutbox_Provider ON dbo.EmailOutbox(ProviderMessageId);
GO

-- GBR-01: baseline rows are read-only once the project has left Active Baseline.
CREATE OR ALTER TRIGGER dbo.TR_SpecItems_BaselineImmutable ON dbo.SpecItems AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d JOIN dbo.WeddingProjects w ON w.ProjectId = d.ProjectId
               WHERE d.IsBaseline = 1 AND d.Version = 1 AND w.Status <> 'Active Baseline')
        THROW 51001, 'The baseline is frozen and cannot be edited.', 1;
END;
GO

-- Additive update for A-16/A-17 (UC-46) and A-18 (UC-47). Run against the WPSM database used by the application.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF COL_LENGTH(N'dbo.Complaints', N'EscalatedAt') IS NULL
    ALTER TABLE dbo.Complaints ADD EscalatedAt DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'ResolvedBy') IS NULL
    ALTER TABLE dbo.Complaints ADD ResolvedBy INT NULL CONSTRAINT FK_Complaint_ResolvedBy REFERENCES dbo.Users(UserId);
IF COL_LENGTH(N'dbo.Complaints', N'ResolvedAt') IS NULL
    ALTER TABLE dbo.Complaints ADD ResolvedAt DATETIME2(0) NULL;
GO
-- SDS 3.14: one request per CR penalty waiver or resource conflict, decided by an Admin.
IF OBJECT_ID(N'dbo.ExceptionRequests', N'U') IS NULL
CREATE TABLE dbo.ExceptionRequests (
    ExceptionRequestId INT IDENTITY(1,1) CONSTRAINT PK_ExceptionRequests PRIMARY KEY,
    RequestCode   NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Exception_Code UNIQUE,
    SourceType    NVARCHAR(20)  NOT NULL,
    ProjectId     INT           NOT NULL CONSTRAINT FK_Exception_Project     REFERENCES dbo.WeddingProjects(ProjectId),
    CRId          INT           NULL     CONSTRAINT FK_Exception_CR          REFERENCES dbo.ChangeRequests(CRId),
    AllocationId  INT           NULL     CONSTRAINT FK_Exception_Allocation  REFERENCES dbo.ResourceAllocations(AllocationId),
    RequestedBy   INT           NOT NULL CONSTRAINT FK_Exception_RequestedBy REFERENCES dbo.Users(UserId),
    RequestNote   NVARCHAR(300) NOT NULL,
    Status        NVARCHAR(10)  NOT NULL CONSTRAINT DF_Exception_Status DEFAULT 'Pending',
    DecisionBy    INT           NULL     CONSTRAINT FK_Exception_DecisionBy  REFERENCES dbo.Users(UserId),
    DecisionNote  NVARCHAR(300) NULL,
    DecidedAt     DATETIME2(0)  NULL,
    CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Exception_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Exception_Source  CHECK (SourceType IN ('CrPenaltyWaiver','ResourceConflict')),
    CONSTRAINT CK_Exception_Target  CHECK ((SourceType = 'CrPenaltyWaiver' AND CRId IS NOT NULL AND AllocationId IS NULL)
                                        OR (SourceType = 'ResourceConflict' AND AllocationId IS NOT NULL AND CRId IS NULL)),
    CONSTRAINT CK_Exception_Status  CHECK (Status IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_Exception_Decided CHECK (Status = 'Pending' OR (DecisionBy IS NOT NULL AND DecisionNote IS NOT NULL AND DecidedAt IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Exception_Status' AND object_id=OBJECT_ID('dbo.ExceptionRequests'))
    CREATE INDEX IX_Exception_Status ON dbo.ExceptionRequests(Status, CreatedAt);
GO

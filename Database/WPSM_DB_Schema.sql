-- ============================================================================
-- WPSM_DB (Wedding Planning and Service Management) - SEP490_G139
-- Target: Microsoft SQL Server 2022
-- Source of truth: Report 3 (SRS) - section I.3 (Conceptual Data Model, 23 entities),
--                  III (screen field tables), V.1 (GBR-01..GBR-15)
-- Script is idempotent (safe to re-run). The last section moves older databases to the event stage flow.
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.Databases WHERE name = N'WPSM_DB')
    CREATE DATABASE [WPSM_DB];
GO

USE [WPSM_DB];
GO

-- ============================================================================
-- A. VENUE AND ACCESS ENTITIES
-- ============================================================================

-- 1. User
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserId             INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    Email              NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,            -- GBR: unique email
    PasswordHash       NVARCHAR(255) NOT NULL,
    FullName           NVARCHAR(100) NOT NULL,
    Phone              NVARCHAR(11)  NULL,
    Role               NVARCHAR(20)  NOT NULL,
    Status             NVARCHAR(20)  NOT NULL CONSTRAINT DF_Users_Status DEFAULT 'Active',  -- never deleted, only deactivated
    MustChangePassword BIT           NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 1, -- forced on first login (G-03)
    CreatedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Users_Role   CHECK (Role IN ('Customer','Manager','Admin')),
    CONSTRAINT CK_Users_Status CHECK (Status IN ('Active','Inactive')),
    CONSTRAINT CK_Users_Phone  CHECK (Phone IS NULL OR (Phone NOT LIKE '%[^0-9]%' AND LEN(Phone) BETWEEN 9 AND 11))
);
GO

-- 2. Hall
IF OBJECT_ID(N'dbo.Halls', N'U') IS NULL
CREATE TABLE dbo.Halls (
    HallId      INT IDENTITY(1,1) CONSTRAINT PK_Halls PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Halls_Name UNIQUE,
    MinCapacity INT           NOT NULL,
    MaxCapacity INT           NOT NULL,
    BasePrice   DECIMAL(18,2) NOT NULL,
    Status      NVARCHAR(20)  NOT NULL CONSTRAINT DF_Halls_Status DEFAULT 'Active',
    CONSTRAINT CK_Halls_Status    CHECK (Status IN ('Active','Inactive')),
    CONSTRAINT CK_Halls_Capacity  CHECK (MinCapacity > 0 AND MaxCapacity >= MinCapacity),
    CONSTRAINT CK_Halls_BasePrice CHECK (BasePrice >= 0)
);
GO

-- 3. Slot (a hall has at most one Slot of each name)
IF OBJECT_ID(N'dbo.Slots', N'U') IS NULL
CREATE TABLE dbo.Slots (
    SlotId    INT IDENTITY(1,1) CONSTRAINT PK_Slots PRIMARY KEY,
    HallId    INT          NOT NULL CONSTRAINT FK_Slots_Halls REFERENCES dbo.Halls(HallId),
    SlotName  NVARCHAR(20) NOT NULL,
    StartTime TIME(0)      NOT NULL,
    EndTime   TIME(0)      NOT NULL,
    CONSTRAINT CK_Slots_SlotName CHECK (SlotName IN ('Lunch','Evening')),
    CONSTRAINT CK_Slots_Time     CHECK (EndTime > StartTime),
    CONSTRAINT UQ_Slots_Hall_SlotName UNIQUE (HallId, SlotName),
    CONSTRAINT UQ_Slots_Slot_Hall     UNIQUE (SlotId, HallId)   -- target of composite FK from WeddingProjects
);
GO

-- 4. Notification
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
CREATE TABLE dbo.Notifications (
    NotificationId INT IDENTITY(1,1) CONSTRAINT PK_Notifications PRIMARY KEY,
    RecipientId    INT           NOT NULL CONSTRAINT FK_Notifications_Recipient REFERENCES dbo.Users(UserId),
    Type           NVARCHAR(30)  NOT NULL,
    Message        NVARCHAR(300) NOT NULL,
    LinkUrl        NVARCHAR(255) NULL,            -- "links to the related screen" (G-04)
    IsRead         BIT           NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
    CreatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Notifications_Type CHECK (Type IN ('Roadmap reminder','Payment','CR update','BEO','Overdue alert','Complaint','Delegation'))
);
GO

-- ============================================================================
-- B. CONFIGURATION ENTITIES (maintained by Admin)
-- ============================================================================

-- 5. Service Catalog Item
IF OBJECT_ID(N'dbo.ServiceCatalogItems', N'U') IS NULL
CREATE TABLE dbo.ServiceCatalogItems (
    ItemId       INT IDENTITY(1,1) CONSTRAINT PK_ServiceCatalogItems PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL,
    Category     NVARCHAR(30)  NOT NULL,
    Unit         NVARCHAR(20)  NOT NULL,
    DefaultPrice DECIMAL(18,2) NOT NULL,
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_Catalog_Status DEFAULT 'Active',  -- deactivated, not deleted
    CONSTRAINT UQ_Catalog_Category_Name UNIQUE (Category, Name),                       -- unique within category
    CONSTRAINT CK_Catalog_Category CHECK (Category IN ('Hall','Catering','Decor','MC','AV','Photo/Video','Attire','Makeup','Transport')),
    CONSTRAINT CK_Catalog_Price    CHECK (DefaultPrice > 0),
    CONSTRAINT CK_Catalog_Status   CHECK (Status IN ('Active','Inactive'))
);
GO

-- 6. Price Frame (versioned by effective-from; one per item per effective date)
IF OBJECT_ID(N'dbo.PriceFrames', N'U') IS NULL
CREATE TABLE dbo.PriceFrames (
    PriceFrameId  INT IDENTITY(1,1) CONSTRAINT PK_PriceFrames PRIMARY KEY,
    ItemId        INT           NOT NULL CONSTRAINT FK_PriceFrames_Item REFERENCES dbo.ServiceCatalogItems(ItemId),
    MinPrice      DECIMAL(18,2) NOT NULL,
    MaxPrice      DECIMAL(18,2) NOT NULL,
    EffectiveFrom DATE          NOT NULL,
    UpdatedBy     INT           NOT NULL CONSTRAINT FK_PriceFrames_UpdatedBy REFERENCES dbo.Users(UserId),
    UpdatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_PriceFrames_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_PriceFrames_Item_Effective UNIQUE (ItemId, EffectiveFrom),
    CONSTRAINT CK_PriceFrames_MinMax CHECK (MinPrice >= 0 AND MinPrice <= MaxPrice)
);
GO

-- 7. Policy Config (stand-alone; NRD ratio / T-30 ratio / CR penalty by T-day range)
IF OBJECT_ID(N'dbo.PolicyConfigs', N'U') IS NULL
CREATE TABLE dbo.PolicyConfigs (
    PolicyId      INT IDENTITY(1,1) CONSTRAINT PK_PolicyConfigs PRIMARY KEY,
    PolicyType    NVARCHAR(20)  NOT NULL,
    TDayMin       INT           NULL,
    TDayMax       INT           NULL,
    RatePercent   DECIMAL(5,2)  NOT NULL,
    EffectiveFrom DATE          NOT NULL,
    UpdatedBy     INT           NOT NULL CONSTRAINT FK_Policy_UpdatedBy REFERENCES dbo.Users(UserId),
    UpdatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Policy_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Policy_Type_Range_Effective UNIQUE (PolicyType, TDayMin, TDayMax, EffectiveFrom),
    CONSTRAINT CK_Policy_Type CHECK (PolicyType IN ('NRD_RATIO','T30_RATIO','CR_PENALTY')),
    CONSTRAINT CK_Policy_Rate CHECK (RatePercent BETWEEN 0 AND 100),
    CONSTRAINT CK_Policy_TDay CHECK (
        (PolicyType = 'CR_PENALTY' AND TDayMin IS NOT NULL AND TDayMax IS NOT NULL AND TDayMin >= 0 AND TDayMin <= TDayMax)
     OR (PolicyType <> 'CR_PENALTY' AND TDayMin IS NULL AND TDayMax IS NULL))
);
GO

-- ============================================================================
-- C. WEDDING PROJECT CORE ENTITIES
-- ============================================================================

-- 8. Wedding Project (one project per hall + slot + event date - GBR-09)
IF OBJECT_ID(N'dbo.WeddingProjects', N'U') IS NULL
CREATE TABLE dbo.WeddingProjects (
    ProjectId         INT IDENTITY(1,1) CONSTRAINT PK_WeddingProjects PRIMARY KEY,
    ProjectCode       NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Projects_Code UNIQUE,
    CustomerId        INT           NOT NULL CONSTRAINT FK_Projects_Customer REFERENCES dbo.Users(UserId),
    ManagerId         INT           NOT NULL CONSTRAINT FK_Projects_Manager  REFERENCES dbo.Users(UserId),
    HallId            INT           NOT NULL CONSTRAINT FK_Projects_Hall     REFERENCES dbo.Halls(HallId),
    SlotId            INT           NOT NULL,
    EventDate         DATE          NOT NULL,
    BaseContractValue DECIMAL(18,2) NOT NULL,
    NRDAmount         DECIMAL(18,2) NOT NULL,
    CurrentTotal      DECIMAL(18,2) NOT NULL,   -- = BaseContractValue + approved CR impact
    Status            NVARCHAR(20)  NOT NULL CONSTRAINT DF_Projects_Status DEFAULT 'Active Baseline',
    BaselineFrozenAt  DATETIME2(0)  NULL,
    CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_Projects_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Projects_Slot FOREIGN KEY (SlotId, HallId) REFERENCES dbo.Slots(SlotId, HallId), -- slot must belong to the hall
    CONSTRAINT UQ_Projects_Hall_Slot_Date UNIQUE (HallId, SlotId, EventDate),
    CONSTRAINT CK_Projects_Status CHECK (Status IN ('Active Baseline','Baseline Frozen','Event Completed','Settled','Closed')),
    CONSTRAINT CK_Projects_Money  CHECK (BaseContractValue > 0 AND NRDAmount >= 0 AND CurrentTotal >= 0),
    CONSTRAINT CK_Projects_Frozen CHECK (Status = 'Active Baseline' OR BaselineFrozenAt IS NOT NULL)
);
GO

-- Accountability: who created the project (Admin oversight). Older rows: the creating Manager is the assigned one.
IF COL_LENGTH(N'dbo.WeddingProjects', N'CreatedBy') IS NULL
    ALTER TABLE dbo.WeddingProjects ADD CreatedBy INT NULL CONSTRAINT FK_Projects_CreatedBy REFERENCES dbo.Users(UserId);
GO
UPDATE dbo.WeddingProjects SET CreatedBy = ManagerId WHERE CreatedBy IS NULL;
GO

-- 9. Spec Item (unique per project, item and version)
IF OBJECT_ID(N'dbo.SpecItems', N'U') IS NULL
CREATE TABLE dbo.SpecItems (
    SpecItemId    INT IDENTITY(1,1) CONSTRAINT PK_SpecItems PRIMARY KEY,
    ProjectId     INT           NOT NULL CONSTRAINT FK_Spec_Project REFERENCES dbo.WeddingProjects(ProjectId),
    CatalogItemId INT           NOT NULL CONSTRAINT FK_Spec_Catalog REFERENCES dbo.ServiceCatalogItems(ItemId),
    Description   NVARCHAR(500) NOT NULL,
    Quantity      INT           NOT NULL,
    UnitPrice     DECIMAL(18,2) NOT NULL,
    LineTotal     AS (Quantity * UnitPrice) PERSISTED,
    IsBaseline    BIT           NOT NULL CONSTRAINT DF_Spec_IsBaseline DEFAULT 1,  -- Baseline (frozen) vs Revised (from approved CR)
    Version       INT           NOT NULL CONSTRAINT DF_Spec_Version DEFAULT 1,
    CONSTRAINT UQ_Spec_Project_Item_Version UNIQUE (ProjectId, CatalogItemId, Version),
    CONSTRAINT CK_Spec_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_Spec_Price    CHECK (UnitPrice >= 0),
    CONSTRAINT CK_Spec_Version  CHECK (Version >= 1)
);
GO

-- 11. Resource (name unique within type)
IF OBJECT_ID(N'dbo.Resources', N'U') IS NULL
CREATE TABLE dbo.Resources (
    ResourceId    INT IDENTITY(1,1) CONSTRAINT PK_Resources PRIMARY KEY,
    Name          NVARCHAR(100) NOT NULL,
    Type          NVARCHAR(20)  NOT NULL,
    TotalQuantity INT           NOT NULL,
    CONSTRAINT UQ_Resources_Type_Name UNIQUE (Type, Name),
    CONSTRAINT CK_Resources_Type     CHECK (Type IN ('Audio','Follow Light','Decor Frame','Technician','Kitchen')),
    CONSTRAINT CK_Resources_Quantity CHECK (TotalQuantity > 0)
);
GO

-- 12. Resource Allocation (GBR-09 audit flags for Admin special exception)
IF OBJECT_ID(N'dbo.ResourceAllocations', N'U') IS NULL
CREATE TABLE dbo.ResourceAllocations (
    AllocationId        INT IDENTITY(1,1) CONSTRAINT PK_ResourceAllocations PRIMARY KEY,
    ResourceId          INT           NOT NULL CONSTRAINT FK_Alloc_Resource REFERENCES dbo.Resources(ResourceId),
    HallId              INT           NOT NULL CONSTRAINT FK_Alloc_Hall     REFERENCES dbo.Halls(HallId),
    ProjectId           INT           NOT NULL CONSTRAINT FK_Alloc_Project  REFERENCES dbo.WeddingProjects(ProjectId),
    StartTime           DATETIME2(0)  NOT NULL,
    EndTime             DATETIME2(0)  NOT NULL,
    AllocatedQuantity   INT           NOT NULL,
    Status              NVARCHAR(30)  NOT NULL CONSTRAINT DF_Alloc_Status DEFAULT 'Reserved',
    IsExceptionApproved BIT           NOT NULL CONSTRAINT DF_Alloc_IsException DEFAULT 0,
    ExceptionApprovedBy INT           NULL CONSTRAINT FK_Alloc_ExceptionBy REFERENCES dbo.Users(UserId),
    ExceptionApprovedAt DATETIME2(0)  NULL,
    ExceptionNote       NVARCHAR(300) NULL,
    CONSTRAINT CK_Alloc_Time     CHECK (EndTime > StartTime),
    CONSTRAINT CK_Alloc_Quantity CHECK (AllocatedQuantity > 0),
    CONSTRAINT CK_Alloc_Status   CHECK (Status IN ('Reserved','PendingException','Exception Approved','Released')),
    CONSTRAINT CK_Alloc_Exception CHECK (
        (IsExceptionApproved = 0 AND Status <> 'Exception Approved')
     OR (IsExceptionApproved = 1 AND Status IN ('Exception Approved','Released')
         AND ExceptionApprovedBy IS NOT NULL AND ExceptionApprovedAt IS NOT NULL AND ExceptionNote IS NOT NULL))
);
GO

-- ============================================================================
-- D. CHANGE, BEO AND EXECUTION ENTITIES
-- ============================================================================

-- 17. BEO (unique per project and version)
IF OBJECT_ID(N'dbo.BEOs', N'U') IS NULL
CREATE TABLE dbo.BEOs (
    BEOId      INT IDENTITY(1,1) CONSTRAINT PK_BEOs PRIMARY KEY,
    ProjectId  INT           NOT NULL CONSTRAINT FK_BEO_Project REFERENCES dbo.WeddingProjects(ProjectId),
    Version    INT           NOT NULL CONSTRAINT DF_BEO_Version DEFAULT 1,
    Status     NVARCHAR(20)  NOT NULL CONSTRAINT DF_BEO_Status DEFAULT 'Draft',
    FrozenAt   DATETIME2(0)  NULL,
    ApprovedBy INT           NULL CONSTRAINT FK_BEO_ApprovedBy REFERENCES dbo.Users(UserId),
    Notes      NVARCHAR(500) NULL,            -- Manager notes while Draft (M-10)
    CONSTRAINT UQ_BEO_Project_Version UNIQUE (ProjectId, Version),
    CONSTRAINT UQ_BEO_Id_Project      UNIQUE (BEOId, ProjectId),   -- target of composite FK from Complaints
    CONSTRAINT CK_BEO_Status  CHECK (Status IN ('Draft','Frozen','Stale')),
    CONSTRAINT CK_BEO_Version CHECK (Version >= 1),
    CONSTRAINT CK_BEO_Frozen  CHECK (Status = 'Draft' OR (FrozenAt IS NOT NULL AND ApprovedBy IS NOT NULL))
);
GO

-- 18. BEO Department Sheet (one sheet per BEO and department)
IF OBJECT_ID(N'dbo.BEODepartmentSheets', N'U') IS NULL
CREATE TABLE dbo.BEODepartmentSheets (
    SheetId     INT IDENTITY(1,1) CONSTRAINT PK_BEODepartmentSheets PRIMARY KEY,
    BEOId       INT           NOT NULL CONSTRAINT FK_Sheet_BEO REFERENCES dbo.BEOs(BEOId),
    Department  NVARCHAR(20)  NOT NULL,
    PdfFilePath NVARCHAR(500) NOT NULL,
    GeneratedAt DATETIME2(0)  NOT NULL CONSTRAINT DF_Sheet_GeneratedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Sheet_BEO_Department UNIQUE (BEOId, Department),
    CONSTRAINT CK_Sheet_Department CHECK (Department IN ('Kitchen','Service','Decor','Tech-MC'))
);
GO

-- 19. Day-of Task (late completion stays recorded: CompletedAt > DeadlineTime)
IF OBJECT_ID(N'dbo.DayOfTasks', N'U') IS NULL
CREATE TABLE dbo.DayOfTasks (
    DayOfTaskId      INT IDENTITY(1,1) CONSTRAINT PK_DayOfTasks PRIMARY KEY,
    ProjectId        INT           NOT NULL CONSTRAINT FK_DayOf_Project REFERENCES dbo.WeddingProjects(ProjectId),
    TaskName         NVARCHAR(200) NOT NULL,
    DeadlineTime     DATETIME2(0)  NOT NULL,
    CompletedAt      DATETIME2(0)  NULL,
    CompletedBy      INT           NULL CONSTRAINT FK_DayOf_CompletedBy REFERENCES dbo.Users(UserId),
    OverdueFlaggedAt DATETIME2(0)  NULL,
    Status           NVARCHAR(20)  NOT NULL CONSTRAINT DF_DayOf_Status DEFAULT 'Pending',
    PhotoRequired    BIT           NOT NULL CONSTRAINT DF_DayOf_PhotoRequired DEFAULT 0,
    RowVersion       ROWVERSION    NOT NULL,
    CONSTRAINT CK_DayOf_Status    CHECK (Status IN ('Pending','Done','Overdue')),
    CONSTRAINT CK_DayOf_Completed CHECK (Status <> 'Done' OR CompletedAt IS NOT NULL)
);
GO

-- Upgrade databases created before CompletedBy / OverdueFlaggedAt / RowVersion existed (CREATE TABLE above only runs once).
IF COL_LENGTH(N'dbo.DayOfTasks', N'CompletedBy') IS NULL
    ALTER TABLE dbo.DayOfTasks ADD CompletedBy INT NULL CONSTRAINT FK_DayOf_CompletedBy REFERENCES dbo.Users(UserId);
IF COL_LENGTH(N'dbo.DayOfTasks', N'OverdueFlaggedAt') IS NULL
    ALTER TABLE dbo.DayOfTasks ADD OverdueFlaggedAt DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.DayOfTasks', N'RowVersion') IS NULL
    ALTER TABLE dbo.DayOfTasks ADD RowVersion ROWVERSION NOT NULL;
GO

-- 20. Execution Photo (1-3 per task that requires proof - enforced by triggers below)
IF OBJECT_ID(N'dbo.ExecutionPhotos', N'U') IS NULL
CREATE TABLE dbo.ExecutionPhotos (
    PhotoId       INT IDENTITY(1,1) CONSTRAINT PK_ExecutionPhotos PRIMARY KEY,
    DayOfTaskId   INT           NOT NULL CONSTRAINT FK_Photo_Task     REFERENCES dbo.DayOfTasks(DayOfTaskId),
    UploaderId    INT           NOT NULL CONSTRAINT FK_Photo_Uploader REFERENCES dbo.Users(UserId),
    ImageFilePath NVARCHAR(500) NOT NULL,
    UploadedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Photo_UploadedAt DEFAULT SYSUTCDATETIME()
);
GO

-- ============================================================================
-- E. FINANCIAL AND FEEDBACK ENTITIES
-- ============================================================================

-- 21. Payment (at most one per project and type; Status/DueDate support C-10 and JOB-02 milestones)
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
CREATE TABLE dbo.Payments (
    PaymentId       INT IDENTITY(1,1) CONSTRAINT PK_Payments PRIMARY KEY,
    ProjectId       INT           NOT NULL CONSTRAINT FK_Payment_Project    REFERENCES dbo.WeddingProjects(ProjectId),
    PaymentType     NVARCHAR(10)  NOT NULL,
    Status          NVARCHAR(10)  NOT NULL CONSTRAINT DF_Payment_Status DEFAULT 'Upcoming',
    DueDate         DATE          NULL,
    Amount          DECIMAL(18,2) NULL,
    PaymentMethod   NVARCHAR(10)  NULL,
    ReceiptFilePath NVARCHAR(500) NULL,
    RecordedBy      INT           NULL CONSTRAINT FK_Payment_RecordedBy REFERENCES dbo.Users(UserId),
    PaidDate        DATE          NULL,
    CONSTRAINT UQ_Payment_Project_Type UNIQUE (ProjectId, PaymentType),
    CONSTRAINT CK_Payment_Type   CHECK (PaymentType IN ('NRD','T30','FINAL')),
    CONSTRAINT CK_Payment_Status CHECK (Status IN ('Upcoming','Due','Paid')),
    CONSTRAINT CK_Payment_Method CHECK (PaymentMethod IS NULL OR PaymentMethod IN ('Bank','Cash')),
    CONSTRAINT CK_Payment_Amount CHECK (Amount IS NULL OR Amount > 0),
    CONSTRAINT CK_Payment_Paid   CHECK (Status <> 'Paid' OR (
        Amount IS NOT NULL AND PaymentMethod IS NOT NULL AND PaidDate IS NOT NULL AND RecordedBy IS NOT NULL
        AND (PaymentMethod = 'Cash' OR ReceiptFilePath IS NOT NULL)))      -- receipt required for Bank, optional for Cash
);
GO

-- 22. Rating (one per project and module; immutable - GBR-10, enforced by trigger below)
IF OBJECT_ID(N'dbo.Ratings', N'U') IS NULL
CREATE TABLE dbo.Ratings (
    RatingId  INT IDENTITY(1,1) CONSTRAINT PK_Ratings PRIMARY KEY,
    ProjectId INT           NOT NULL CONSTRAINT FK_Rating_Project REFERENCES dbo.WeddingProjects(ProjectId),
    Module    NVARCHAR(10)  NOT NULL,
    Stars     TINYINT       NOT NULL,
    Comment   NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0)  NOT NULL CONSTRAINT DF_Rating_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Rating_Project_Module UNIQUE (ProjectId, Module),
    CONSTRAINT CK_Rating_Module CHECK (Module IN ('Hall','Menu','Decor','MC')),
    CONSTRAINT CK_Rating_Stars  CHECK (Stars BETWEEN 1 AND 5)
);
GO

-- 23. Complaint (disputed BEO must belong to the same project)
IF OBJECT_ID(N'dbo.Complaints', N'U') IS NULL
CREATE TABLE dbo.Complaints (
    ComplaintId    INT IDENTITY(1,1) CONSTRAINT PK_Complaints PRIMARY KEY,
    ComplaintCode  NVARCHAR(30)   NOT NULL CONSTRAINT UQ_Complaint_Code UNIQUE,   -- ticket code shown on C-14
    ProjectId      INT            NOT NULL CONSTRAINT FK_Complaint_Project REFERENCES dbo.WeddingProjects(ProjectId),
    BEOId          INT            NOT NULL,
    Description    NVARCHAR(1000) NOT NULL,
    Status         NVARCHAR(20)   NOT NULL CONSTRAINT DF_Complaint_Status DEFAULT 'Open',
    EscalationNote NVARCHAR(500)  NULL,                                           -- required when escalating (M-20)
    ResolutionNote NVARCHAR(1000) NULL,
    CreatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_Complaint_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Complaint_BEO FOREIGN KEY (BEOId, ProjectId) REFERENCES dbo.BEOs(BEOId, ProjectId),
    CONSTRAINT CK_Complaint_Status     CHECK (Status IN ('Open','Escalated','Resolved')),
    CONSTRAINT CK_Complaint_Resolved   CHECK (Status <> 'Resolved'  OR ResolutionNote IS NOT NULL),
    CONSTRAINT CK_Complaint_Escalated  CHECK (Status <> 'Escalated' OR EscalationNote IS NOT NULL)
);
GO

-- Filed-by customer and a short title (written by the C-14 complaint flow).
IF COL_LENGTH(N'dbo.Complaints', N'CustomerId') IS NULL
    ALTER TABLE dbo.Complaints ADD CustomerId INT NULL CONSTRAINT FK_Complaint_Customer REFERENCES dbo.Users(UserId);
IF COL_LENGTH(N'dbo.Complaints', N'Title') IS NULL
    ALTER TABLE dbo.Complaints ADD Title NVARCHAR(200) NULL;
GO

-- ============================================================================
-- INDEXES (foreign keys / frequent filters)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Projects_Status_Date' AND object_id=OBJECT_ID('dbo.WeddingProjects'))
    CREATE INDEX IX_Projects_Status_Date ON dbo.WeddingProjects(Status, EventDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Projects_Customer' AND object_id=OBJECT_ID('dbo.WeddingProjects'))
    CREATE INDEX IX_Projects_Customer ON dbo.WeddingProjects(CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Projects_Manager' AND object_id=OBJECT_ID('dbo.WeddingProjects'))
    CREATE INDEX IX_Projects_Manager ON dbo.WeddingProjects(ManagerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Alloc_Resource_Time' AND object_id=OBJECT_ID('dbo.ResourceAllocations'))
    CREATE INDEX IX_Alloc_Resource_Time ON dbo.ResourceAllocations(ResourceId, StartTime, EndTime);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Alloc_Project' AND object_id=OBJECT_ID('dbo.ResourceAllocations'))
    CREATE INDEX IX_Alloc_Project ON dbo.ResourceAllocations(ProjectId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DayOf_Project_Deadline' AND object_id=OBJECT_ID('dbo.DayOfTasks'))
    CREATE INDEX IX_DayOf_Project_Deadline ON dbo.DayOfTasks(ProjectId, DeadlineTime, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DayOfTasks_Status_Deadline' AND object_id=OBJECT_ID('dbo.DayOfTasks'))
    CREATE INDEX IX_DayOfTasks_Status_Deadline ON dbo.DayOfTasks(Status, DeadlineTime);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_WeddingProjects_Manager_Status' AND object_id=OBJECT_ID('dbo.WeddingProjects'))
    CREATE INDEX IX_WeddingProjects_Manager_Status ON dbo.WeddingProjects(ManagerId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Photo_Task' AND object_id=OBJECT_ID('dbo.ExecutionPhotos'))
    CREATE INDEX IX_Photo_Task ON dbo.ExecutionPhotos(DayOfTaskId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Notifications_Recipient' AND object_id=OBJECT_ID('dbo.Notifications'))
    CREATE INDEX IX_Notifications_Recipient ON dbo.Notifications(RecipientId, IsRead, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Complaint_Project' AND object_id=OBJECT_ID('dbo.Complaints'))
    CREATE INDEX IX_Complaint_Project ON dbo.Complaints(ProjectId, Status);
GO

-- Portal persistence update
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
IF COL_LENGTH(N'dbo.SpecItems', N'LineTotal') IS NULL
    ALTER TABLE dbo.SpecItems ADD LineTotal AS (Quantity * UnitPrice) PERSISTED;
IF COL_LENGTH(N'dbo.Complaints', N'ComplaintCode') IS NULL
    ALTER TABLE dbo.Complaints ADD ComplaintCode NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'EscalationNote') IS NULL
    ALTER TABLE dbo.Complaints ADD EscalationNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'CreatedAt') IS NULL
    ALTER TABLE dbo.Complaints ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Complaints_PortalCreated DEFAULT SYSUTCDATETIME();
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
IF COL_LENGTH(N'dbo.Payments', N'ReceiptContent') IS NULL
    ALTER TABLE dbo.Payments ADD ReceiptContent VARBINARY(MAX) NULL;
IF COL_LENGTH(N'dbo.Payments', N'ReceiptFileName') IS NULL
    ALTER TABLE dbo.Payments ADD ReceiptFileName NVARCHAR(255) NULL;
IF COL_LENGTH(N'dbo.Payments', N'ReceiptContentType') IS NULL
    ALTER TABLE dbo.Payments ADD ReceiptContentType NVARCHAR(100) NULL;
-- Legacy credential queue (replaced by EmailOutbox / JOB-07): dropped together with its encrypted payloads.
IF OBJECT_ID(N'dbo.EmailDeliveries', N'U') IS NOT NULL
    DROP TABLE dbo.EmailDeliveries;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'dbo.WeddingProjects') AND name=N'UX_Projects_PortalHallSlotDate'
)
AND NOT EXISTS (
    SELECT HallId,SlotId,EventDate FROM dbo.WeddingProjects GROUP BY HallId,SlotId,EventDate HAVING COUNT(*)>1
)
    CREATE UNIQUE INDEX UX_Projects_PortalHallSlotDate ON dbo.WeddingProjects(HallId,SlotId,EventDate);
GO

-- ============================================================================
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

-- ============================================================================
-- ADMIN SCREENS: A-16/A-17 (UC-46) and A-18 (UC-47); same as Updates/20261103_AdminScreens.sql
-- ============================================================================
IF COL_LENGTH(N'dbo.Complaints', N'EscalatedAt') IS NULL
    ALTER TABLE dbo.Complaints ADD EscalatedAt DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.Complaints', N'ResolvedBy') IS NULL
    ALTER TABLE dbo.Complaints ADD ResolvedBy INT NULL CONSTRAINT FK_Complaint_ResolvedBy REFERENCES dbo.Users(UserId);
IF COL_LENGTH(N'dbo.Complaints', N'ResolvedAt') IS NULL
    ALTER TABLE dbo.Complaints ADD ResolvedAt DATETIME2(0) NULL;
GO
-- SDS 3.14: one request per unresolvable resource conflict, decided by an Admin.
IF OBJECT_ID(N'dbo.ExceptionRequests', N'U') IS NULL
CREATE TABLE dbo.ExceptionRequests (
    ExceptionRequestId INT IDENTITY(1,1) CONSTRAINT PK_ExceptionRequests PRIMARY KEY,
    RequestCode   NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Exception_Code UNIQUE,
    SourceType    NVARCHAR(20)  NOT NULL,
    ProjectId     INT           NOT NULL CONSTRAINT FK_Exception_Project     REFERENCES dbo.WeddingProjects(ProjectId),
    AllocationId  INT           NOT NULL CONSTRAINT FK_Exception_Allocation  REFERENCES dbo.ResourceAllocations(AllocationId),
    RequestedBy   INT           NOT NULL CONSTRAINT FK_Exception_RequestedBy REFERENCES dbo.Users(UserId),
    RequestNote   NVARCHAR(300) NOT NULL,
    Status        NVARCHAR(10)  NOT NULL CONSTRAINT DF_Exception_Status DEFAULT 'Pending',
    DecisionBy    INT           NULL     CONSTRAINT FK_Exception_DecisionBy  REFERENCES dbo.Users(UserId),
    DecisionNote  NVARCHAR(300) NULL,
    DecidedAt     DATETIME2(0)  NULL,
    CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Exception_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Exception_Source  CHECK (SourceType = 'ResourceConflict'),
    CONSTRAINT CK_Exception_Status  CHECK (Status IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_Exception_Decided CHECK (Status = 'Pending' OR (DecisionBy IS NOT NULL AND DecisionNote IS NOT NULL AND DecidedAt IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Exception_Status' AND object_id=OBJECT_ID('dbo.ExceptionRequests'))
    CREATE INDEX IX_Exception_Status ON dbo.ExceptionRequests(Status, CreatedAt);
GO

-- ============================================================================
-- JWT SIGN-IN: refresh tokens (SDS 3.2); same as Updates/20261104_RefreshTokens.sql
-- ============================================================================
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
CREATE TABLE dbo.RefreshTokens (
    RefreshTokenId    INT IDENTITY(1,1) CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId            INT          NOT NULL CONSTRAINT FK_RefreshTokens_User REFERENCES dbo.Users(UserId),
    TokenHash         CHAR(64)     NOT NULL CONSTRAINT UQ_RefreshTokens_Hash UNIQUE,
    ExpiresAt         DATETIME2(0) NOT NULL,
    AbsoluteExpiresAt DATETIME2(0) NOT NULL,
    RevokedAt         DATETIME2(0) NULL,
    ReplacedByTokenId INT          NULL CONSTRAINT FK_RefreshTokens_ReplacedBy REFERENCES dbo.RefreshTokens(RefreshTokenId),
    CreatedAt         DATETIME2(0) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedByIp       NVARCHAR(45) NULL,
    CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAt <= AbsoluteExpiresAt)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RefreshTokens_User' AND object_id=OBJECT_ID('dbo.RefreshTokens'))
    CREATE INDEX IX_RefreshTokens_User ON dbo.RefreshTokens(UserId, RevokedAt);
GO

-- ============================================================================
-- AUDIT LOG: who changed what (Admin oversight). Append-only: rows are never updated or deleted.
-- Action: Create / Update / Delete / Login. Impact: Service, Upload, Account, Project, Payment, ChangeRequest,
-- Policy, Complaint, Exception, Other.
-- ============================================================================
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
CREATE TABLE dbo.AuditLogs (
    AuditId     BIGINT IDENTITY(1,1) CONSTRAINT PK_AuditLogs PRIMARY KEY,
    OccurredAt  DATETIME2(0)  NOT NULL CONSTRAINT DF_AuditLogs_OccurredAt DEFAULT SYSUTCDATETIME(),
    UserId      INT           NULL CONSTRAINT FK_AuditLogs_User REFERENCES dbo.Users(UserId),   -- NULL = system job
    AccountName NVARCHAR(150) NULL,
    Email       NVARCHAR(256) NULL,
    Role        NVARCHAR(20)  NULL,
    Action      NVARCHAR(20)  NOT NULL,
    Impact      NVARCHAR(30)  NOT NULL,
    EntityType  NVARCHAR(60)  NOT NULL,
    EntityId    NVARCHAR(60)  NULL,
    ProjectId   INT           NULL,
    ProjectCode NVARCHAR(30)  NULL,
    Summary     NVARCHAR(500) NOT NULL,
    Changes     NVARCHAR(MAX) NULL,                                                            -- JSON {field: {old, new}}
    IpAddress   NVARCHAR(45)  NULL,
    CONSTRAINT CK_AuditLogs_Action CHECK (Action IN ('Create','Update','Delete','Login'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_OccurredAt' AND object_id=OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_OccurredAt ON dbo.AuditLogs(OccurredAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_User' AND object_id=OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_User ON dbo.AuditLogs(UserId, OccurredAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_Project' AND object_id=OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Project ON dbo.AuditLogs(ProjectId, OccurredAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_Impact' AND object_id=OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Impact ON dbo.AuditLogs(Impact, Action, OccurredAt DESC);
GO
CREATE OR ALTER TRIGGER dbo.TR_AuditLogs_AppendOnly ON dbo.AuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51010, N'Audit log is append-only.', 1;
END;
GO

-- ============================================================================
-- PROJECT MANAGERS: one Primary (the creator, = WeddingProjects.ManagerId) plus any number of co-managers added by the
-- Primary or an Admin. Removed rows stay (RemovedAt) so Admin can see everyone who operated the project.
-- ============================================================================
IF OBJECT_ID(N'dbo.ProjectManagers', N'U') IS NULL
CREATE TABLE dbo.ProjectManagers (
    ProjectManagerId INT IDENTITY(1,1) CONSTRAINT PK_ProjectManagers PRIMARY KEY,
    ProjectId        INT           NOT NULL CONSTRAINT FK_ProjectManagers_Project REFERENCES dbo.WeddingProjects(ProjectId),
    ManagerId        INT           NOT NULL CONSTRAINT FK_ProjectManagers_Manager REFERENCES dbo.Users(UserId),
    Role             NVARCHAR(20)  NOT NULL,
    AssignedBy       INT           NULL CONSTRAINT FK_ProjectManagers_AssignedBy REFERENCES dbo.Users(UserId),
    AssignedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_ProjectManagers_AssignedAt DEFAULT SYSUTCDATETIME(),
    Reason           NVARCHAR(500) NULL,
    RemovedBy        INT           NULL CONSTRAINT FK_ProjectManagers_RemovedBy REFERENCES dbo.Users(UserId),
    RemovedAt        DATETIME2(0)  NULL,
    RemovalReason    NVARCHAR(500) NULL,
    CONSTRAINT CK_ProjectManagers_Role CHECK (Role IN ('Primary','CoManager')),
    CONSTRAINT CK_ProjectManagers_PrimaryKept CHECK (Role = 'CoManager' OR RemovedAt IS NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_ProjectManagers_Active' AND object_id=OBJECT_ID('dbo.ProjectManagers'))
    CREATE UNIQUE INDEX UQ_ProjectManagers_Active ON dbo.ProjectManagers(ProjectId, ManagerId) WHERE RemovedAt IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_ProjectManagers_Primary' AND object_id=OBJECT_ID('dbo.ProjectManagers'))
    CREATE UNIQUE INDEX UQ_ProjectManagers_Primary ON dbo.ProjectManagers(ProjectId) WHERE Role = 'Primary' AND RemovedAt IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ProjectManagers_Manager' AND object_id=OBJECT_ID('dbo.ProjectManagers'))
    CREATE INDEX IX_ProjectManagers_Manager ON dbo.ProjectManagers(ManagerId, RemovedAt) INCLUDE (ProjectId);
GO
-- Every project has its Primary row (projects created before this table).
INSERT dbo.ProjectManagers(ProjectId, ManagerId, Role, AssignedBy, AssignedAt, Reason)
SELECT p.ProjectId, p.ManagerId, 'Primary', p.CreatedBy, p.CreatedAt, N'Người tạo dự án'
FROM dbo.WeddingProjects p
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProjectManagers m WHERE m.ProjectId = p.ProjectId AND m.Role = 'Primary' AND m.RemovedAt IS NULL);
GO

-- Notification type for co-management (existing databases created before it).
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Notifications_Type' AND definition LIKE '%Delegation%')
BEGIN
    IF OBJECT_ID(N'dbo.CK_Notifications_Type', N'C') IS NOT NULL ALTER TABLE dbo.Notifications DROP CONSTRAINT CK_Notifications_Type;
    ALTER TABLE dbo.Notifications ADD CONSTRAINT CK_Notifications_Type CHECK (Type IN ('Roadmap reminder','Payment','CR update','BEO','Overdue alert','Complaint','Delegation'));
END
GO

-- Legacy columns from the first token design; refresh tokens live in dbo.RefreshTokens.
IF COL_LENGTH(N'dbo.Users', N'RefreshToken') IS NOT NULL ALTER TABLE dbo.Users DROP COLUMN RefreshToken;
IF COL_LENGTH(N'dbo.Users', N'RefreshTokenExpiryTime') IS NOT NULL ALTER TABLE dbo.Users DROP COLUMN RefreshTokenExpiryTime;
GO

-- ============================================================================
-- EVENT STAGE FLOW (same as Updates/20261118_EventStageFlow.sql) (replaces the T-90 / T-30 / T-7 milestones)
--
-- Stage 1  Sign and deposit      : event date 15..90 days after signing, 30% deposit, never refunded on cancel.
-- Stage 2  Adjusting             : until 10 days before; the operator edits services with a note, total >= 70% of signed value.
-- Stage 3  Locked                : the system locks at 10 days before; suppliers booked with evidence before 8 days.
-- Stage 4  Event day             : day-of tasks and proof photos (unchanged).
-- Stage 5  Settlement            : one invoice = adjusted contract + confirmed addon contracts - deposit, due 7 days after.
--
-- Removed: the change-request / addendum / penalty flow, roadmap milestones, the T-30 payment.
-- Run with: sqlcmd -S localhost -E -b -I -f 65001 -d <db> -i <this file>   (-b stops on the first error)
-- Idempotent: every block checks the current state first.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ── 1. Change-request flow ──────────────────────────────────────────────────
IF COL_LENGTH(N'dbo.ExceptionRequests', N'CRId') IS NOT NULL
BEGIN
    EXEC(N'DELETE FROM dbo.ExceptionRequests WHERE SourceType = ''CrPenaltyWaiver'';');
    ALTER TABLE dbo.ExceptionRequests DROP CONSTRAINT CK_Exception_Target;
    ALTER TABLE dbo.ExceptionRequests DROP CONSTRAINT CK_Exception_Source;
    ALTER TABLE dbo.ExceptionRequests DROP CONSTRAINT FK_Exception_CR;
    ALTER TABLE dbo.ExceptionRequests DROP COLUMN CRId;
    EXEC(N'ALTER TABLE dbo.ExceptionRequests ALTER COLUMN AllocationId INT NOT NULL;');
    ALTER TABLE dbo.ExceptionRequests ADD CONSTRAINT CK_Exception_Source CHECK (SourceType = 'ResourceConflict');
END
GO
IF OBJECT_ID(N'dbo.CRItems', N'U') IS NOT NULL DROP TABLE dbo.CRItems;
IF OBJECT_ID(N'dbo.CRHistories', N'U') IS NOT NULL DROP TABLE dbo.CRHistories;
IF OBJECT_ID(N'dbo.ContractAddenda', N'U') IS NOT NULL DROP TABLE dbo.ContractAddenda;
IF OBJECT_ID(N'dbo.ChangeRequests', N'U') IS NOT NULL DROP TABLE dbo.ChangeRequests;
IF OBJECT_ID(N'dbo.RoadmapTasks', N'U') IS NOT NULL DROP TABLE dbo.RoadmapTasks;
GO

-- ── 2. Spec items: one row per service, no baseline versions ────────────────
IF OBJECT_ID(N'dbo.TR_SpecItems_BaselineImmutable', N'TR') IS NOT NULL DROP TRIGGER dbo.TR_SpecItems_BaselineImmutable;
GO
IF COL_LENGTH(N'dbo.SpecItems', N'Version') IS NOT NULL
BEGIN
    -- Keep the latest version of each service line.
    EXEC(N'DELETE s FROM dbo.SpecItems s WHERE EXISTS (SELECT 1 FROM dbo.SpecItems n
           WHERE n.ProjectId = s.ProjectId AND n.CatalogItemId = s.CatalogItemId AND n.Version > s.Version);');
    ALTER TABLE dbo.SpecItems DROP CONSTRAINT UQ_Spec_Project_Item_Version;
    ALTER TABLE dbo.SpecItems DROP CONSTRAINT CK_Spec_Version;
    ALTER TABLE dbo.SpecItems DROP CONSTRAINT DF_Spec_Version;
    ALTER TABLE dbo.SpecItems DROP CONSTRAINT DF_Spec_IsBaseline;
    ALTER TABLE dbo.SpecItems DROP COLUMN Version, IsBaseline;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Spec_Project' AND object_id = OBJECT_ID(N'dbo.SpecItems'))
    CREATE INDEX IX_Spec_Project ON dbo.SpecItems(ProjectId);
GO

-- ── 3. Events (WeddingProjects): new statuses, deposit / lock / cancel columns ─
-- CHECK constraints block a column rename: drop them first, they are re-created below.
IF COL_LENGTH(N'dbo.WeddingProjects', N'NRDAmount') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Money')
        ALTER TABLE dbo.WeddingProjects DROP CONSTRAINT CK_Projects_Money;
    EXEC sp_rename N'dbo.WeddingProjects.NRDAmount', N'DepositAmount', N'COLUMN';
END
IF COL_LENGTH(N'dbo.WeddingProjects', N'BaselineFrozenAt') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Frozen')
        ALTER TABLE dbo.WeddingProjects DROP CONSTRAINT CK_Projects_Frozen;
    EXEC sp_rename N'dbo.WeddingProjects.BaselineFrozenAt', N'LockedAt', N'COLUMN';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Money')
    EXEC(N'ALTER TABLE dbo.WeddingProjects ADD CONSTRAINT CK_Projects_Money
          CHECK (BaseContractValue > 0 AND DepositAmount >= 0 AND CurrentTotal >= 0);');
GO
IF COL_LENGTH(N'dbo.WeddingProjects', N'CancelledAt') IS NULL
    ALTER TABLE dbo.WeddingProjects ADD
        CancelledAt  DATETIME2(0)  NULL,
        CancelledBy  INT           NULL CONSTRAINT FK_Projects_CancelledBy REFERENCES dbo.Users(UserId),
        CancelReason NVARCHAR(500) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Frozen')
    ALTER TABLE dbo.WeddingProjects DROP CONSTRAINT CK_Projects_Frozen;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Status' AND definition LIKE N'%Baseline%')
BEGIN
    ALTER TABLE dbo.WeddingProjects DROP CONSTRAINT CK_Projects_Status;
    ALTER TABLE dbo.WeddingProjects DROP CONSTRAINT DF_Projects_Status;
END
GO
UPDATE dbo.WeddingProjects SET Status = CASE Status
    WHEN 'Active Baseline' THEN 'Adjusting' WHEN 'Baseline Frozen' THEN 'Locked' WHEN 'Event Completed' THEN 'Completed' ELSE Status END
WHERE Status IN ('Active Baseline', 'Baseline Frozen', 'Event Completed');
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Status')
BEGIN
    ALTER TABLE dbo.WeddingProjects ADD CONSTRAINT DF_Projects_Status DEFAULT 'Adjusting' FOR Status;
    ALTER TABLE dbo.WeddingProjects ADD CONSTRAINT CK_Projects_Status
        CHECK (Status IN ('Adjusting','Locked','Completed','Settled','Closed','Cancelled'));
END
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Locked')
    ALTER TABLE dbo.WeddingProjects ADD CONSTRAINT CK_Projects_Locked
        CHECK (Status IN ('Adjusting','Cancelled') OR LockedAt IS NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Projects_Cancelled')
    ALTER TABLE dbo.WeddingProjects ADD CONSTRAINT CK_Projects_Cancelled
        CHECK (Status <> 'Cancelled' OR (CancelledAt IS NOT NULL AND CancelReason IS NOT NULL));
GO
-- A cancelled event frees its hall, slot and date.
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Projects_Hall_Slot_Date')
    ALTER TABLE dbo.WeddingProjects DROP CONSTRAINT UQ_Projects_Hall_Slot_Date;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Projects_PortalHallSlotDate' AND object_id = OBJECT_ID(N'dbo.WeddingProjects'))
    DROP INDEX UX_Projects_PortalHallSlotDate ON dbo.WeddingProjects;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Projects_HallSlotDate_Active' AND object_id = OBJECT_ID(N'dbo.WeddingProjects'))
    CREATE UNIQUE INDEX UX_Projects_HallSlotDate_Active ON dbo.WeddingProjects(HallId, SlotId, EventDate) WHERE Status <> 'Cancelled';
GO

-- ── 4. Payments: only the deposit and the settlement ────────────────────────
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Payment_Type' AND definition LIKE N'%T30%')
BEGIN
    ALTER TABLE dbo.Payments DROP CONSTRAINT CK_Payment_Type;
    DELETE FROM dbo.Payments WHERE PaymentType = 'T30';
    UPDATE dbo.Payments SET PaymentType = CASE PaymentType WHEN 'NRD' THEN 'Deposit' WHEN 'FINAL' THEN 'Settlement' ELSE PaymentType END;
    ALTER TABLE dbo.Payments ADD CONSTRAINT CK_Payment_Type CHECK (PaymentType IN ('Deposit','Settlement'));
END
GO

-- ── 5. Policies: deposit ratio and the stage windows ────────────────────────
IF COL_LENGTH(N'dbo.PolicyConfigs', N'TDayMin') IS NOT NULL
BEGIN
    ALTER TABLE dbo.PolicyConfigs DROP CONSTRAINT CK_Policy_TDay;
    ALTER TABLE dbo.PolicyConfigs DROP CONSTRAINT CK_Policy_Type;
    ALTER TABLE dbo.PolicyConfigs DROP CONSTRAINT CK_Policy_Rate;
    ALTER TABLE dbo.PolicyConfigs DROP CONSTRAINT UQ_Policy_Type_Range_Effective;
    DELETE FROM dbo.PolicyConfigs WHERE PolicyType IN ('T30_RATIO', 'CR_PENALTY');
    UPDATE dbo.PolicyConfigs SET PolicyType = 'DEPOSIT_RATIO' WHERE PolicyType = 'NRD_RATIO';
    ALTER TABLE dbo.PolicyConfigs DROP COLUMN TDayMin, TDayMax;
    EXEC sp_rename N'dbo.PolicyConfigs.RatePercent', N'Value', N'COLUMN';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Policy_Type')
BEGIN
    ALTER TABLE dbo.PolicyConfigs ALTER COLUMN PolicyType NVARCHAR(30) NOT NULL;
    ALTER TABLE dbo.PolicyConfigs ADD CONSTRAINT UQ_Policy_Type_Effective UNIQUE (PolicyType, EffectiveFrom);
    ALTER TABLE dbo.PolicyConfigs ADD CONSTRAINT CK_Policy_Type CHECK (PolicyType IN
        ('DEPOSIT_RATIO','MAX_DECREASE_RATIO','BOOKING_MIN_DAYS','BOOKING_MAX_DAYS','LOCK_DAYS','SUPPLIER_DEADLINE_DAYS','SETTLEMENT_DUE_DAYS'));
    EXEC(N'ALTER TABLE dbo.PolicyConfigs ADD CONSTRAINT CK_Policy_Value CHECK (Value >= 0 AND (PolicyType NOT LIKE ''%RATIO'' OR Value <= 100));');
END
GO
EXEC(N'DECLARE @admin INT = (SELECT MIN(UserId) FROM dbo.Users WHERE Role = ''Admin'');
INSERT dbo.PolicyConfigs(PolicyType, Value, EffectiveFrom, UpdatedBy)
SELECT v.PolicyType, v.Value, ''2026-01-01'', @admin
FROM (VALUES (''DEPOSIT_RATIO'', 30), (''MAX_DECREASE_RATIO'', 30), (''BOOKING_MIN_DAYS'', 15), (''BOOKING_MAX_DAYS'', 90),
             (''LOCK_DAYS'', 10), (''SUPPLIER_DEADLINE_DAYS'', 8), (''SETTLEMENT_DUE_DAYS'', 7)) v(PolicyType, Value)
WHERE @admin IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.PolicyConfigs p WHERE p.PolicyType = v.PolicyType);');
GO

-- ── 6. Notifications ─────────────────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Notifications_Type' AND definition LIKE N'%Roadmap%')
BEGIN
    ALTER TABLE dbo.Notifications DROP CONSTRAINT CK_Notifications_Type;
    UPDATE dbo.Notifications SET Type = CASE Type WHEN 'Roadmap reminder' THEN 'Stage reminder' WHEN 'CR update' THEN 'Addon contract' ELSE Type END;
    ALTER TABLE dbo.Notifications ADD CONSTRAINT CK_Notifications_Type CHECK (Type IN
        ('Stage reminder','Payment','Addon contract','BEO','Overdue alert','Complaint','Delegation'));
END
GO

-- ── 7. Service catalog: new categories, which services are booked from a supplier ─
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Catalog_Category' AND definition NOT LIKE N'%Ceremony%')
BEGIN
    ALTER TABLE dbo.ServiceCatalogItems DROP CONSTRAINT CK_Catalog_Category;
    ALTER TABLE dbo.ServiceCatalogItems ADD CONSTRAINT CK_Catalog_Category CHECK (Category IN
        ('Hall','Catering','Decor','MC','AV','Photo/Video','Attire','Makeup','Transport','Ceremony','Performance','Furniture'));
END
IF COL_LENGTH(N'dbo.ServiceCatalogItems', N'RequiresSupplier') IS NULL
    ALTER TABLE dbo.ServiceCatalogItems ADD RequiresSupplier BIT NOT NULL CONSTRAINT DF_Catalog_RequiresSupplier DEFAULT 0;
GO
UPDATE dbo.ServiceCatalogItems SET RequiresSupplier = 1
WHERE RequiresSupplier = 0 AND Category IN ('Catering','Decor','Attire','Photo/Video','Ceremony','Performance','Furniture');
GO
DECLARE @admin INT = (SELECT MIN(UserId) FROM dbo.Users WHERE Role = 'Admin');
INSERT dbo.ServiceCatalogItems(Name, Category, Unit, DefaultPrice, Status, RequiresSupplier)
SELECT v.Name, v.Category, v.Unit, v.Price, 'Active', 1
FROM (VALUES (N'Mâm quả bê lễ 7 tráp', 'Ceremony', N'Bộ', 3500000),
             (N'Đội bê tráp 8 người', 'Ceremony', N'Đội', 2000000),
             (N'Tiết mục văn nghệ', 'Performance', N'Tiết mục', 1500000),
             (N'Múa khai tiệc', 'Performance', N'Tiết mục', 2500000),
             (N'Bàn ghế Chiavari', 'Furniture', N'Bộ', 150000),
             (N'Sofa khu đón khách', 'Furniture', N'Bộ', 2500000)) v(Name, Category, Unit, Price)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ServiceCatalogItems c WHERE c.Category = v.Category AND c.Name = v.Name);
INSERT dbo.PriceFrames(ItemId, MinPrice, MaxPrice, EffectiveFrom, UpdatedBy)
SELECT c.ItemId, c.DefaultPrice * 0.8, c.DefaultPrice * 1.2, '2026-01-01', @admin
FROM dbo.ServiceCatalogItems c
WHERE @admin IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.PriceFrames f WHERE f.ItemId = c.ItemId);
GO

-- ── 8. Stored files: addendum files are gone, supplier evidence is new ──────
DELETE FROM dbo.StoredFiles WHERE Purpose IN ('AddendumPdf', 'SignedAddendum');
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_StoredFiles_Purpose' AND definition NOT LIKE N'%SupplierEvidence%')
BEGIN
    ALTER TABLE dbo.StoredFiles DROP CONSTRAINT CK_StoredFiles_Purpose;
    ALTER TABLE dbo.StoredFiles ADD CONSTRAINT CK_StoredFiles_Purpose CHECK (Purpose IN
        ('DepositReceipt','PaymentReceipt','ProofPhoto','Thumbnail','BeoSheet','SettlementReceipt','SupplierEvidence'));
END
GO

-- ── 9. New tables ────────────────────────────────────────────────────────────
-- Notes the operator writes per stage: tasting, fitting, adjustments (with the total before / after), general.
IF OBJECT_ID(N'dbo.StageNotes', N'U') IS NULL
CREATE TABLE dbo.StageNotes (
    StageNoteId INT IDENTITY(1,1) CONSTRAINT PK_StageNotes PRIMARY KEY,
    ProjectId   INT            NOT NULL CONSTRAINT FK_StageNotes_Project REFERENCES dbo.WeddingProjects(ProjectId),
    Stage       TINYINT        NOT NULL,
    NoteType    NVARCHAR(20)   NOT NULL,
    Content     NVARCHAR(1000) NOT NULL,
    ValueBefore DECIMAL(18,2)  NULL,
    ValueAfter  DECIMAL(18,2)  NULL,
    CreatedBy   INT            NOT NULL CONSTRAINT FK_StageNotes_CreatedBy REFERENCES dbo.Users(UserId),
    CreatedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_StageNotes_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_StageNotes_Stage CHECK (Stage BETWEEN 1 AND 5),
    CONSTRAINT CK_StageNotes_Type  CHECK (NoteType IN ('Tasting','Fitting','Adjustment','Supplier','General')),
    CONSTRAINT CK_StageNotes_Value CHECK (NoteType <> 'Adjustment' OR (ValueBefore IS NOT NULL AND ValueAfter IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StageNotes_Project' AND object_id = OBJECT_ID(N'dbo.StageNotes'))
    CREATE INDEX IX_StageNotes_Project ON dbo.StageNotes(ProjectId, CreatedAt DESC);
GO

-- Addon contract: extra services for a locked event, no deposit, counted only once the customer confirms.
IF OBJECT_ID(N'dbo.AddonContracts', N'U') IS NULL
CREATE TABLE dbo.AddonContracts (
    AddonId      INT IDENTITY(1,1) CONSTRAINT PK_AddonContracts PRIMARY KEY,
    AddonCode    NVARCHAR(30)   NOT NULL CONSTRAINT UQ_AddonContracts_Code UNIQUE,
    ProjectId    INT            NOT NULL CONSTRAINT FK_AddonContracts_Project REFERENCES dbo.WeddingProjects(ProjectId),
    Title        NVARCHAR(200)  NOT NULL,
    Note         NVARCHAR(1000) NULL,
    Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_AddonContracts_Status DEFAULT 'PendingCustomer',
    CreatedBy    INT            NOT NULL CONSTRAINT FK_AddonContracts_CreatedBy REFERENCES dbo.Users(UserId),
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_AddonContracts_CreatedAt DEFAULT SYSUTCDATETIME(),
    ConfirmedAt  DATETIME2(0)   NULL,
    CancelledAt  DATETIME2(0)   NULL,
    CancelReason NVARCHAR(500)  NULL,
    RemindedAt   DATETIME2(0)   NULL,
    CONSTRAINT CK_AddonContracts_Status    CHECK (Status IN ('PendingCustomer','Confirmed','Cancelled')),
    CONSTRAINT CK_AddonContracts_Confirmed CHECK (Status <> 'Confirmed' OR ConfirmedAt IS NOT NULL),
    CONSTRAINT CK_AddonContracts_Cancelled CHECK (Status <> 'Cancelled' OR (CancelledAt IS NOT NULL AND CancelReason IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AddonContracts_Project' AND object_id = OBJECT_ID(N'dbo.AddonContracts'))
    CREATE INDEX IX_AddonContracts_Project ON dbo.AddonContracts(ProjectId, Status);
GO
IF OBJECT_ID(N'dbo.AddonContractItems', N'U') IS NULL
CREATE TABLE dbo.AddonContractItems (
    AddonItemId   INT IDENTITY(1,1) CONSTRAINT PK_AddonContractItems PRIMARY KEY,
    AddonId       INT           NOT NULL CONSTRAINT FK_AddonItems_Addon   REFERENCES dbo.AddonContracts(AddonId) ON DELETE CASCADE,
    CatalogItemId INT           NULL     CONSTRAINT FK_AddonItems_Catalog REFERENCES dbo.ServiceCatalogItems(ItemId),
    Description   NVARCHAR(300) NOT NULL,
    Quantity      INT           NOT NULL,
    UnitPrice     DECIMAL(18,2) NOT NULL,
    LineTotal     AS (Quantity * UnitPrice) PERSISTED,
    CONSTRAINT CK_AddonItems_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_AddonItems_Price    CHECK (UnitPrice >= 0)
);
GO

-- Evidence that a supplier was booked for the event (transfer note, invoice or contract). No purchase-order tracking.
IF OBJECT_ID(N'dbo.SupplierEvidences', N'U') IS NULL
CREATE TABLE dbo.SupplierEvidences (
    EvidenceId   INT IDENTITY(1,1) CONSTRAINT PK_SupplierEvidences PRIMARY KEY,
    ProjectId    INT              NOT NULL CONSTRAINT FK_SupplierEvidences_Project REFERENCES dbo.WeddingProjects(ProjectId),
    SpecItemId   INT              NULL     CONSTRAINT FK_SupplierEvidences_Spec    REFERENCES dbo.SpecItems(SpecItemId),
    SupplierName NVARCHAR(150)    NOT NULL,
    EvidenceType NVARCHAR(20)     NOT NULL,
    FileId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SupplierEvidences_File REFERENCES dbo.StoredFiles(FileId),
    Note         NVARCHAR(500)    NULL,
    UploadedBy   INT              NOT NULL CONSTRAINT FK_SupplierEvidences_User REFERENCES dbo.Users(UserId),
    UploadedAt   DATETIME2(0)     NOT NULL CONSTRAINT DF_SupplierEvidences_UploadedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_SupplierEvidences_Type CHECK (EvidenceType IN ('Transfer','Invoice','Contract','Other'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupplierEvidences_Project' AND object_id = OBJECT_ID(N'dbo.SupplierEvidences'))
    CREATE INDEX IX_SupplierEvidences_Project ON dbo.SupplierEvidences(ProjectId);
GO

-- Final invoice snapshot: one per event, the lines show the contract, each confirmed addon and the deposit.
IF OBJECT_ID(N'dbo.SettlementInvoices', N'U') IS NULL
CREATE TABLE dbo.SettlementInvoices (
    InvoiceId     INT IDENTITY(1,1) CONSTRAINT PK_SettlementInvoices PRIMARY KEY,
    InvoiceCode   NVARCHAR(30)  NOT NULL CONSTRAINT UQ_SettlementInvoices_Code UNIQUE,
    ProjectId     INT           NOT NULL CONSTRAINT UQ_SettlementInvoices_Project UNIQUE
                                         CONSTRAINT FK_SettlementInvoices_Project REFERENCES dbo.WeddingProjects(ProjectId),
    ContractTotal DECIMAL(18,2) NOT NULL,
    AddonTotal    DECIMAL(18,2) NOT NULL,
    DepositPaid   DECIMAL(18,2) NOT NULL,
    AmountDue     AS (ContractTotal + AddonTotal - DepositPaid) PERSISTED,
    DueDate       DATE          NOT NULL,
    Status        NVARCHAR(10)  NOT NULL CONSTRAINT DF_SettlementInvoices_Status DEFAULT 'Issued',
    IssuedBy      INT           NULL CONSTRAINT FK_SettlementInvoices_IssuedBy REFERENCES dbo.Users(UserId),  -- NULL = system
    IssuedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_SettlementInvoices_IssuedAt DEFAULT SYSUTCDATETIME(),
    PaidAt        DATETIME2(0)  NULL,
    CONSTRAINT CK_SettlementInvoices_Status CHECK (Status IN ('Issued','Paid')),
    CONSTRAINT CK_SettlementInvoices_Paid   CHECK (Status <> 'Paid' OR PaidAt IS NOT NULL),
    CONSTRAINT CK_SettlementInvoices_Money  CHECK (ContractTotal >= 0 AND AddonTotal >= 0 AND DepositPaid >= 0)
);
GO
IF OBJECT_ID(N'dbo.SettlementInvoiceLines', N'U') IS NULL
CREATE TABLE dbo.SettlementInvoiceLines (
    LineId      INT IDENTITY(1,1) CONSTRAINT PK_SettlementInvoiceLines PRIMARY KEY,
    InvoiceId   INT           NOT NULL CONSTRAINT FK_InvoiceLines_Invoice REFERENCES dbo.SettlementInvoices(InvoiceId) ON DELETE CASCADE,
    LineType    NVARCHAR(10)  NOT NULL,
    AddonId     INT           NULL CONSTRAINT FK_InvoiceLines_Addon REFERENCES dbo.AddonContracts(AddonId),
    Description NVARCHAR(300) NOT NULL,
    Amount      DECIMAL(18,2) NOT NULL,              -- the deposit line is negative
    CONSTRAINT CK_InvoiceLines_Type  CHECK (LineType IN ('Contract','Addon','Deposit')),
    CONSTRAINT CK_InvoiceLines_Addon CHECK (LineType <> 'Addon' OR AddonId IS NOT NULL)
);
GO

-- Shared contact details shown to customers (hotline).
IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
CREATE TABLE dbo.SystemSettings (
    SettingKey   NVARCHAR(50)  NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
    SettingValue NVARCHAR(500) NOT NULL,
    UpdatedBy    INT           NULL CONSTRAINT FK_SystemSettings_User REFERENCES dbo.Users(UserId),
    UpdatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_SystemSettings_UpdatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = N'Hotline')
    INSERT dbo.SystemSettings(SettingKey, SettingValue) VALUES (N'Hotline', N'0976586668');
GO

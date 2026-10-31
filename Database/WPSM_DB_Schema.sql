-- ============================================================================
-- WPSM_DB (Wedding Planning and Service Management) - SEP490_G139
-- Target: Microsoft SQL Server 2022
-- Source of truth: Report 3 (SRS) - section I.3 (Conceptual Data Model, 23 entities),
--                  III (screen field tables), V.1 (GBR-01..GBR-15)
-- Script is idempotent (safe to re-run); it never drops anything.
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'WPSM_DB')
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
    CONSTRAINT CK_Notifications_Type CHECK (Type IN ('Roadmap reminder','Payment','CR update','BEO','Overdue alert','Complaint'))
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

-- 10. Roadmap Task (RemindedAt supports JOB-01 "not yet reminded")
IF OBJECT_ID(N'dbo.RoadmapTasks', N'U') IS NULL
CREATE TABLE dbo.RoadmapTasks (
    TaskId       INT IDENTITY(1,1) CONSTRAINT PK_RoadmapTasks PRIMARY KEY,
    ProjectId    INT           NOT NULL CONSTRAINT FK_Roadmap_Project REFERENCES dbo.WeddingProjects(ProjectId),
    Milestone    NVARCHAR(20)  NOT NULL,
    Title        NVARCHAR(200) NOT NULL,
    AssigneeRole NVARCHAR(20)  NOT NULL,
    DueDate      DATE          NOT NULL,
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_Roadmap_Status DEFAULT 'Pending',
    CompletedAt  DATETIME2(0)  NULL,
    RemindedAt   DATETIME2(0)  NULL,
    CONSTRAINT CK_Roadmap_Milestone CHECK (Milestone IN ('T-90','T-30','T-7','Event Day')),
    CONSTRAINT CK_Roadmap_Assignee  CHECK (AssigneeRole IN ('Customer','Manager')),
    CONSTRAINT CK_Roadmap_Status    CHECK (Status IN ('Pending','Done','Overdue')),
    CONSTRAINT CK_Roadmap_Completed CHECK (Status <> 'Done' OR CompletedAt IS NOT NULL)
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
    CONSTRAINT CK_Alloc_Status   CHECK (Status IN ('Reserved','Exception Approved','Released')),
    CONSTRAINT CK_Alloc_Exception CHECK (
        (IsExceptionApproved = 0 AND Status <> 'Exception Approved')
     OR (IsExceptionApproved = 1 AND Status IN ('Exception Approved','Released')
         AND ExceptionApprovedBy IS NOT NULL AND ExceptionApprovedAt IS NOT NULL AND ExceptionNote IS NOT NULL))
);
GO

-- ============================================================================
-- D. CHANGE, BEO AND EXECUTION ENTITIES
-- ============================================================================

-- 13. Change Request (TotalVariance = price delta + penalty, GBR-03; Approved needs both parties, GBR-04)
IF OBJECT_ID(N'dbo.ChangeRequests', N'U') IS NULL
CREATE TABLE dbo.ChangeRequests (
    CRId               INT IDENTITY(1,1) CONSTRAINT PK_ChangeRequests PRIMARY KEY,
    CRCode             NVARCHAR(30)  NOT NULL CONSTRAINT UQ_CR_Code UNIQUE,
    ProjectId          INT           NOT NULL CONSTRAINT FK_CR_Project   REFERENCES dbo.WeddingProjects(ProjectId),
    CreatedBy          INT           NOT NULL CONSTRAINT FK_CR_CreatedBy REFERENCES dbo.Users(UserId),   -- Customer or Manager
    Type               NVARCHAR(30)  NOT NULL,
    Description        NVARCHAR(500) NOT NULL,
    CostImpact         DECIMAL(18,2) NOT NULL CONSTRAINT DF_CR_CostImpact  DEFAULT 0,   -- CR Price Delta (+ surcharge / - deduction)
    PenaltyRate        DECIMAL(5,2)  NULL,                                              -- rate applied from T-day rule (C-07)
    PenaltyFee         DECIMAL(18,2) NOT NULL CONSTRAINT DF_CR_PenaltyFee  DEFAULT 0,
    TotalVariance      AS (CostImpact + PenaltyFee) PERSISTED,                          -- Total CR Impact
    Status             NVARCHAR(20)  NOT NULL CONSTRAINT DF_CR_Status DEFAULT 'Draft',
    CustomerApprovedAt DATETIME2(0)  NULL,
    ManagerApprovedAt  DATETIME2(0)  NULL,
    CreatedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_CR_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_CR_Type    CHECK (Type IN ('Menu change','Decor change','Table count','Script edit','Other')),
    CONSTRAINT CK_CR_Status  CHECK (Status IN ('Draft','Negotiating','Approved','Rejected')),
    CONSTRAINT CK_CR_Penalty CHECK (PenaltyFee >= 0 AND (PenaltyRate IS NULL OR PenaltyRate BETWEEN 0 AND 100)),
    CONSTRAINT CK_CR_BothApproved CHECK (Status <> 'Approved' OR (CustomerApprovedAt IS NOT NULL AND ManagerApprovedAt IS NOT NULL))
);
GO

-- 14. CR Item (unique per CR and Spec Item; DeltaAmount per GBR-03)
IF OBJECT_ID(N'dbo.CRItems', N'U') IS NULL
CREATE TABLE dbo.CRItems (
    CRItemId         INT IDENTITY(1,1) CONSTRAINT PK_CRItems PRIMARY KEY,
    CRId             INT           NOT NULL CONSTRAINT FK_CRItems_CR       REFERENCES dbo.ChangeRequests(CRId),
    TargetSpecItemId INT           NOT NULL CONSTRAINT FK_CRItems_SpecItem REFERENCES dbo.SpecItems(SpecItemId),
    OldQuantity      INT           NOT NULL,
    OldUnitPrice     DECIMAL(18,2) NOT NULL,
    NewQuantity      INT           NOT NULL,
    NewUnitPrice     DECIMAL(18,2) NOT NULL,
    DeltaAmount      AS ((NewUnitPrice * NewQuantity) - (OldUnitPrice * OldQuantity)) PERSISTED,
    CONSTRAINT UQ_CRItems_CR_SpecItem UNIQUE (CRId, TargetSpecItemId),
    CONSTRAINT CK_CRItems_Values CHECK (OldQuantity >= 0 AND NewQuantity >= 0 AND OldUnitPrice >= 0 AND NewUnitPrice >= 0)
);
GO

-- 15. CR History (append-only, GBR-13 - enforced by trigger below)
IF OBJECT_ID(N'dbo.CRHistories', N'U') IS NULL
CREATE TABLE dbo.CRHistories (
    HistoryId INT IDENTITY(1,1) CONSTRAINT PK_CRHistories PRIMARY KEY,
    CRId      INT           NOT NULL CONSTRAINT FK_CRHistory_CR    REFERENCES dbo.ChangeRequests(CRId),
    ActorId   INT           NOT NULL CONSTRAINT FK_CRHistory_Actor REFERENCES dbo.Users(UserId),
    Action    NVARCHAR(50)  NOT NULL,
    Note      NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0)  NOT NULL CONSTRAINT DF_CRHistory_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

-- 16. Contract Addendum (at most one per approved CR)
IF OBJECT_ID(N'dbo.ContractAddenda', N'U') IS NULL
CREATE TABLE dbo.ContractAddenda (
    AddendumId    INT IDENTITY(1,1) CONSTRAINT PK_ContractAddenda PRIMARY KEY,
    AddendumCode  NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Addenda_Code UNIQUE,
    CRId          INT           NOT NULL CONSTRAINT UQ_Addenda_CR UNIQUE CONSTRAINT FK_Addenda_CR REFERENCES dbo.ChangeRequests(CRId),
    ProjectId     INT           NOT NULL CONSTRAINT FK_Addenda_Project REFERENCES dbo.WeddingProjects(ProjectId),
    TotalVariance DECIMAL(18,2) NOT NULL,
    FilePath      NVARCHAR(500) NULL,
    SignedAt      DATETIME2(0)  NULL,           -- blank until signed copy is attached (C-09)
    Status        NVARCHAR(20)  NOT NULL CONSTRAINT DF_Addenda_Status DEFAULT 'Generated',
    CONSTRAINT CK_Addenda_Status CHECK (Status IN ('Generated','Signed')),
    CONSTRAINT CK_Addenda_Signed CHECK (Status <> 'Signed' OR SignedAt IS NOT NULL)
);
GO

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
    DayOfTaskId   INT IDENTITY(1,1) CONSTRAINT PK_DayOfTasks PRIMARY KEY,
    ProjectId     INT           NOT NULL CONSTRAINT FK_DayOf_Project REFERENCES dbo.WeddingProjects(ProjectId),
    TaskName      NVARCHAR(200) NOT NULL,
    DeadlineTime  DATETIME2(0)  NOT NULL,
    CompletedAt   DATETIME2(0)  NULL,
    Status        NVARCHAR(20)  NOT NULL CONSTRAINT DF_DayOf_Status DEFAULT 'Pending',
    PhotoRequired BIT           NOT NULL CONSTRAINT DF_DayOf_PhotoRequired DEFAULT 0,
    CONSTRAINT CK_DayOf_Status    CHECK (Status IN ('Pending','Done','Overdue')),
    CONSTRAINT CK_DayOf_Completed CHECK (Status <> 'Done' OR CompletedAt IS NOT NULL)
);
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
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Roadmap_Project_Milestone' AND object_id=OBJECT_ID('dbo.RoadmapTasks'))
    CREATE INDEX IX_Roadmap_Project_Milestone ON dbo.RoadmapTasks(ProjectId, Milestone);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CR_Project_Status' AND object_id=OBJECT_ID('dbo.ChangeRequests'))
    CREATE INDEX IX_CR_Project_Status ON dbo.ChangeRequests(ProjectId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CRHistory_CR' AND object_id=OBJECT_ID('dbo.CRHistories'))
    CREATE INDEX IX_CRHistory_CR ON dbo.CRHistories(CRId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DayOf_Project_Deadline' AND object_id=OBJECT_ID('dbo.DayOfTasks'))
    CREATE INDEX IX_DayOf_Project_Deadline ON dbo.DayOfTasks(ProjectId, DeadlineTime, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Photo_Task' AND object_id=OBJECT_ID('dbo.ExecutionPhotos'))
    CREATE INDEX IX_Photo_Task ON dbo.ExecutionPhotos(DayOfTaskId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Notifications_Recipient' AND object_id=OBJECT_ID('dbo.Notifications'))
    CREATE INDEX IX_Notifications_Recipient ON dbo.Notifications(RecipientId, IsRead, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Complaint_Project' AND object_id=OBJECT_ID('dbo.Complaints'))
    CREATE INDEX IX_Complaint_Project ON dbo.Complaints(ProjectId, Status);
GO

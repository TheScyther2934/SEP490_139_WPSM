-- ============================================================================
-- 2026-11-18  Event stage flow (replaces the T-90 / T-30 / T-7 milestones)
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

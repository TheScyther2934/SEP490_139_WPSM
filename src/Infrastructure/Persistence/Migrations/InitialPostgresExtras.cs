using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Rules the EF model does not describe, carried over from the SQL Server schema (Database/WPSM_DB_Schema.sql and Updates):
/// the CHECK constraints, the append-only audit log trigger, and row level security on every table.
/// RLS has no policies: the API connects as the table owner (bypasses RLS), while Supabase's Data API roles see nothing.
/// </summary>
internal static class InitialPostgresExtras
{
    public const string Up = """
ALTER TABLE "AddonContractItems" ADD CONSTRAINT "CK_AddonItems_Price" CHECK ("UnitPrice">=(0));
ALTER TABLE "AddonContractItems" ADD CONSTRAINT "CK_AddonItems_Quantity" CHECK ("Quantity">(0));
ALTER TABLE "AddonContracts" ADD CONSTRAINT "CK_AddonContracts_Cancelled" CHECK ("Status"<>'Cancelled' OR "CancelledAt" IS NOT NULL AND "CancelReason" IS NOT NULL);
ALTER TABLE "AddonContracts" ADD CONSTRAINT "CK_AddonContracts_Confirmed" CHECK ("Status"<>'Confirmed' OR "ConfirmedAt" IS NOT NULL);
ALTER TABLE "AddonContracts" ADD CONSTRAINT "CK_AddonContracts_Status" CHECK ("Status"='Cancelled' OR "Status"='Confirmed' OR "Status"='PendingCustomer');
ALTER TABLE "AuditLogs" ADD CONSTRAINT "CK_AuditLogs_Action" CHECK ("Action"='Login' OR "Action"='Delete' OR "Action"='Update' OR "Action"='Create');
ALTER TABLE "BEODepartmentSheets" ADD CONSTRAINT "CK_Sheet_Department" CHECK ("Department"='Tech-MC' OR "Department"='Decor' OR "Department"='Service' OR "Department"='Kitchen');
ALTER TABLE "BEOs" ADD CONSTRAINT "CK_BEO_Frozen" CHECK ("Status"='Draft' OR "FrozenAt" IS NOT NULL AND "ApprovedBy" IS NOT NULL);
ALTER TABLE "BEOs" ADD CONSTRAINT "CK_BEO_Status" CHECK ("Status"='Stale' OR "Status"='Frozen' OR "Status"='Draft');
ALTER TABLE "BEOs" ADD CONSTRAINT "CK_BEO_Version" CHECK ("Version">=(1));
ALTER TABLE "Complaints" ADD CONSTRAINT "CK_Complaint_Escalated" CHECK ("Status"<>'Escalated' OR "EscalationNote" IS NOT NULL);
ALTER TABLE "Complaints" ADD CONSTRAINT "CK_Complaint_Resolved" CHECK ("Status"<>'Resolved' OR "ResolutionNote" IS NOT NULL);
ALTER TABLE "Complaints" ADD CONSTRAINT "CK_Complaint_Status" CHECK ("Status"='Resolved' OR "Status"='Escalated' OR "Status"='Open');
ALTER TABLE "DayOfTasks" ADD CONSTRAINT "CK_DayOf_Completed" CHECK ("Status"<>'Done' OR "CompletedAt" IS NOT NULL);
ALTER TABLE "DayOfTasks" ADD CONSTRAINT "CK_DayOf_Department" CHECK ("Department"='Tech-MC' OR "Department"='Decor' OR "Department"='Service' OR "Department"='Kitchen');
ALTER TABLE "DayOfTasks" ADD CONSTRAINT "CK_DayOf_Status" CHECK ("Status"='Overdue' OR "Status"='Done' OR "Status"='Pending');
ALTER TABLE "EmailOutbox" ADD CONSTRAINT "CK_EmailOutbox_Kind" CHECK ("Kind"='OverdueAlert' OR "Kind"='PaymentRequest' OR "Kind"='Reminder' OR "Kind"='Credentials');
ALTER TABLE "EmailOutbox" ADD CONSTRAINT "CK_EmailOutbox_Status" CHECK ("Status"='Failed' OR "Status"='Sent' OR "Status"='Queued');
ALTER TABLE "ExceptionRequests" ADD CONSTRAINT "CK_Exception_Decided" CHECK ("Status"='Pending' OR "DecisionBy" IS NOT NULL AND "DecisionNote" IS NOT NULL AND "DecidedAt" IS NOT NULL);
ALTER TABLE "ExceptionRequests" ADD CONSTRAINT "CK_Exception_Source" CHECK ("SourceType"='ResourceConflict');
ALTER TABLE "ExceptionRequests" ADD CONSTRAINT "CK_Exception_Status" CHECK ("Status"='Rejected' OR "Status"='Approved' OR "Status"='Pending');
ALTER TABLE "Halls" ADD CONSTRAINT "CK_Halls_BasePrice" CHECK ("BasePrice">=(0));
ALTER TABLE "Halls" ADD CONSTRAINT "CK_Halls_Capacity" CHECK ("MinCapacity">(0) AND "MaxCapacity">="MinCapacity");
ALTER TABLE "Halls" ADD CONSTRAINT "CK_Halls_Status" CHECK ("Status"='Inactive' OR "Status"='Active');
ALTER TABLE "Notifications" ADD CONSTRAINT "CK_Notifications_Type" CHECK ("Type"='Delegation' OR "Type"='Complaint' OR "Type"='Overdue alert' OR "Type"='BEO' OR "Type"='Addon contract' OR "Type"='Payment' OR "Type"='Stage reminder');
ALTER TABLE "Payments" ADD CONSTRAINT "CK_Payment_Amount" CHECK ("Amount" IS NULL OR "Amount">(0));
ALTER TABLE "Payments" ADD CONSTRAINT "CK_Payment_Method" CHECK ("PaymentMethod" IS NULL OR ("PaymentMethod"='Cash' OR "PaymentMethod"='Bank'));
ALTER TABLE "Payments" ADD CONSTRAINT "CK_Payment_Paid" CHECK ("Status"<>'Paid' OR "Amount" IS NOT NULL AND "PaymentMethod" IS NOT NULL AND "PaidDate" IS NOT NULL AND "RecordedBy" IS NOT NULL AND ("PaymentMethod"='Cash' OR "ReceiptFilePath" IS NOT NULL));
ALTER TABLE "Payments" ADD CONSTRAINT "CK_Payment_Status" CHECK ("Status"='Paid' OR "Status"='Due' OR "Status"='Upcoming');
ALTER TABLE "Payments" ADD CONSTRAINT "CK_Payment_Type" CHECK ("PaymentType"='Settlement' OR "PaymentType"='Deposit');
ALTER TABLE "PolicyConfigs" ADD CONSTRAINT "CK_Policy_Type" CHECK ("PolicyType"='SETTLEMENT_DUE_DAYS' OR "PolicyType"='SUPPLIER_DEADLINE_DAYS' OR "PolicyType"='LOCK_DAYS' OR "PolicyType"='BOOKING_MAX_DAYS' OR "PolicyType"='BOOKING_MIN_DAYS' OR "PolicyType"='MAX_DECREASE_RATIO' OR "PolicyType"='DEPOSIT_RATIO');
ALTER TABLE "PolicyConfigs" ADD CONSTRAINT "CK_Policy_Value" CHECK ("Value">=(0) AND (NOT "PolicyType" LIKE '%RATIO' OR "Value"<=(100)));
ALTER TABLE "PriceFrames" ADD CONSTRAINT "CK_PriceFrames_MinMax" CHECK ("MinPrice">=(0) AND "MinPrice"<="MaxPrice");
ALTER TABLE "ProjectManagers" ADD CONSTRAINT "CK_ProjectManagers_PrimaryKept" CHECK ("Role"='CoManager' OR "RemovedAt" IS NULL);
ALTER TABLE "ProjectManagers" ADD CONSTRAINT "CK_ProjectManagers_Role" CHECK ("Role"='CoManager' OR "Role"='Primary');
ALTER TABLE "Ratings" ADD CONSTRAINT "CK_Rating_Module" CHECK ("Module"='MC' OR "Module"='Decor' OR "Module"='Menu' OR "Module"='Hall');
ALTER TABLE "Ratings" ADD CONSTRAINT "CK_Rating_Stars" CHECK ("Stars">=(1) AND "Stars"<=(5));
ALTER TABLE "RefreshTokens" ADD CONSTRAINT "CK_RefreshTokens_Expiry" CHECK ("ExpiresAt"<="AbsoluteExpiresAt");
ALTER TABLE "ResourceAllocations" ADD CONSTRAINT "CK_Alloc_Exception" CHECK ("IsExceptionApproved"=false AND "Status"<>'Exception Approved' OR "IsExceptionApproved"=true AND ("Status"='Released' OR "Status"='Exception Approved') AND "ExceptionApprovedBy" IS NOT NULL AND "ExceptionApprovedAt" IS NOT NULL AND "ExceptionNote" IS NOT NULL);
ALTER TABLE "ResourceAllocations" ADD CONSTRAINT "CK_Alloc_Quantity" CHECK ("AllocatedQuantity">(0));
ALTER TABLE "ResourceAllocations" ADD CONSTRAINT "CK_Alloc_Status" CHECK ("Status"='Released' OR "Status"='Exception Approved' OR "Status"='Reserved');
ALTER TABLE "ResourceAllocations" ADD CONSTRAINT "CK_Alloc_Time" CHECK ("EndTime">"StartTime");
ALTER TABLE "Resources" ADD CONSTRAINT "CK_Resources_Quantity" CHECK ("TotalQuantity">(0));
ALTER TABLE "Resources" ADD CONSTRAINT "CK_Resources_Type" CHECK ("Type"='Kitchen' OR "Type"='Technician' OR "Type"='Decor Frame' OR "Type"='Follow Light' OR "Type"='Audio');
ALTER TABLE "ServiceCatalogItems" ADD CONSTRAINT "CK_Catalog_Category" CHECK ("Category"='Furniture' OR "Category"='Performance' OR "Category"='Ceremony' OR "Category"='Transport' OR "Category"='Makeup' OR "Category"='Attire' OR "Category"='Photo/Video' OR "Category"='AV' OR "Category"='MC' OR "Category"='Decor' OR "Category"='Catering' OR "Category"='Hall');
ALTER TABLE "ServiceCatalogItems" ADD CONSTRAINT "CK_Catalog_Price" CHECK ("DefaultPrice">(0));
ALTER TABLE "ServiceCatalogItems" ADD CONSTRAINT "CK_Catalog_Status" CHECK ("Status"='Inactive' OR "Status"='Active');
ALTER TABLE "SettlementInvoiceLines" ADD CONSTRAINT "CK_InvoiceLines_Addon" CHECK ("LineType"<>'Addon' OR "AddonId" IS NOT NULL);
ALTER TABLE "SettlementInvoiceLines" ADD CONSTRAINT "CK_InvoiceLines_Type" CHECK ("LineType"='Deposit' OR "LineType"='Addon' OR "LineType"='Contract');
ALTER TABLE "SettlementInvoices" ADD CONSTRAINT "CK_SettlementInvoices_Money" CHECK ("ContractTotal">=(0) AND "AddonTotal">=(0) AND "DepositPaid">=(0));
ALTER TABLE "SettlementInvoices" ADD CONSTRAINT "CK_SettlementInvoices_Paid" CHECK ("Status"<>'Paid' OR "PaidAt" IS NOT NULL);
ALTER TABLE "SettlementInvoices" ADD CONSTRAINT "CK_SettlementInvoices_Status" CHECK ("Status"='Paid' OR "Status"='Issued');
ALTER TABLE "Slots" ADD CONSTRAINT "CK_Slots_SlotName" CHECK ("SlotName"='Evening' OR "SlotName"='Lunch');
ALTER TABLE "Slots" ADD CONSTRAINT "CK_Slots_Time" CHECK ("EndTime">"StartTime");
ALTER TABLE "SpecItems" ADD CONSTRAINT "CK_Spec_Price" CHECK ("UnitPrice">=(0));
ALTER TABLE "SpecItems" ADD CONSTRAINT "CK_Spec_Quantity" CHECK ("Quantity">(0));
ALTER TABLE "StageNotes" ADD CONSTRAINT "CK_StageNotes_Stage" CHECK ("Stage">=(1) AND "Stage"<=(5));
ALTER TABLE "StageNotes" ADD CONSTRAINT "CK_StageNotes_Type" CHECK ("NoteType"='General' OR "NoteType"='Supplier' OR "NoteType"='Adjustment' OR "NoteType"='Fitting' OR "NoteType"='Tasting');
ALTER TABLE "StageNotes" ADD CONSTRAINT "CK_StageNotes_Value" CHECK ("NoteType"<>'Adjustment' OR "ValueBefore" IS NOT NULL AND "ValueAfter" IS NOT NULL);
ALTER TABLE "StoredFiles" ADD CONSTRAINT "CK_StoredFiles_Purpose" CHECK ("Purpose"='SupplierEvidence' OR "Purpose"='SettlementReceipt' OR "Purpose"='BeoSheet' OR "Purpose"='Thumbnail' OR "Purpose"='ProofPhoto' OR "Purpose"='PaymentReceipt' OR "Purpose"='DepositReceipt');
ALTER TABLE "StoredFiles" ADD CONSTRAINT "CK_StoredFiles_Size" CHECK ("SizeBytes"<=(10485760));
ALTER TABLE "SupplierEvidences" ADD CONSTRAINT "CK_SupplierEvidences_Type" CHECK ("EvidenceType"='Other' OR "EvidenceType"='Contract' OR "EvidenceType"='Invoice' OR "EvidenceType"='Transfer');
ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_Phone" CHECK ("Phone" IS NULL OR "Phone" !~ '[^0-9]' AND (length("Phone")>=(9) AND length("Phone")<=(11)));
ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_Role" CHECK ("Role"='Admin' OR "Role"='Manager' OR "Role"='Customer');
ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_Status" CHECK ("Status"='Inactive' OR "Status"='Active');
ALTER TABLE "WeddingProjects" ADD CONSTRAINT "CK_Projects_Cancelled" CHECK ("Status"<>'Cancelled' OR "CancelledAt" IS NOT NULL AND "CancelReason" IS NOT NULL);
ALTER TABLE "WeddingProjects" ADD CONSTRAINT "CK_Projects_Locked" CHECK ("Status"='Cancelled' OR "Status"='Adjusting' OR "LockedAt" IS NOT NULL);
ALTER TABLE "WeddingProjects" ADD CONSTRAINT "CK_Projects_Money" CHECK ("BaseContractValue">(0) AND "DepositAmount">=(0) AND "CurrentTotal">=(0));
ALTER TABLE "WeddingProjects" ADD CONSTRAINT "CK_Projects_Status" CHECK ("Status"='Cancelled' OR "Status"='Closed' OR "Status"='Settled' OR "Status"='Completed' OR "Status"='Locked' OR "Status"='Adjusting');

CREATE OR REPLACE FUNCTION audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Audit log is append-only.';
END
$$;
CREATE TRIGGER "TR_AuditLogs_AppendOnly" BEFORE UPDATE OR DELETE ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION audit_logs_append_only();

ALTER TABLE "__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AddonContractItems" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AddonContracts" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AuditLogs" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "BEODepartmentSheets" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "BEOs" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Complaints" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "DayOfTasks" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "EmailOutbox" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ExceptionRequests" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ExecutionPhotos" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Halls" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Notifications" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Payments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PolicyConfigs" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PriceFrames" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ProjectManagers" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Ratings" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "RefreshTokens" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ResourceAllocations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Resources" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ServiceCatalogItems" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SettlementInvoiceLines" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SettlementInvoices" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Slots" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SpecItems" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "StageNotes" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "StoredFiles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupplierEvidences" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SystemSettings" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Users" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "WeddingProjects" ENABLE ROW LEVEL SECURITY;
""";

    public const string Down = """
DROP TRIGGER IF EXISTS "TR_AuditLogs_AppendOnly" ON "AuditLogs";
DROP FUNCTION IF EXISTS audit_logs_append_only();
""";
}
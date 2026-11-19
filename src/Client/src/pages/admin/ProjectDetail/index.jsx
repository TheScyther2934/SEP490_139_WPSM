import { Link as RouterLink, useParams, useSearchParams } from "react-router-dom";
import { Box, Button, Card, Stack, Tab, Tabs, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import OverviewTab from "./components/OverviewTab";
import ContractTab from "./components/ContractTab";
import PaymentsTab from "./components/PaymentsTab";
import EvidenceTab from "./components/EvidenceTab";
import AuditLogTable from "../AuditLogs/components/AuditLogTable";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";
import { PATHS } from "../../../routes/paths";
import { dateVi, slotLabel } from "../../../utils/format";

const TABS = [
  { key: "overview", label: "Tổng quan" },
  { key: "contract", label: "Hợp đồng & dịch vụ" },
  { key: "payments", label: "Thanh toán & hóa đơn" },
  { key: "evidence", label: "Dẫn chứng" },
  { key: "history", label: "Lịch sử thay đổi" },
];

// Admin read-only view of one project: who created and operates it, its status, contract, invoices and field evidence.
export default function AdminProjectDetail() {
  const { projectCode } = useParams();
  const [params, setParams] = useSearchParams();
  const tab = TABS.some((t) => t.key === params.get("tab")) ? params.get("tab") : "overview";
  const { data, loading, error, reload } = useAsync(() => adminService.getProject(projectCode), [projectCode]);

  return (
    <>
      <PageHeader title={data ? `${data.projectCode} · ${data.coupleName}` : projectCode}
        subtitle={data ? `${dateVi(data.eventDate)} · ${data.hall} · Ca ${slotLabel(data.slot)}${data.slotTime ? ` (${data.slotTime})` : ""}` : ""}
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.admin.projects}>Danh sách sự kiện</Button>} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <>
            <Card sx={{ mb: 2, px: 2, py: 1.5 }}>
              <Stack direction={{ xs: "column", md: "row" }} spacing={{ xs: 1, md: 4 }} alignItems={{ md: "center" }}>
                <StatusChip kind="project" status={data.status} size="medium" />
                <Info label="Người tạo" value={data.createdBy?.name ?? "—"} />
                <Info label="Người điều hành" value={data.managers.map((m) => m.name).join(", ") || "—"} />
                <Info label="Khách hàng" value={`${data.customerEmail}${data.customerPhone ? ` · ${data.customerPhone}` : ""}`} />
              </Stack>
            </Card>
            <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
              <Tabs value={tab} onChange={(_, value) => setParams(value === "overview" ? {} : { tab: value }, { replace: true })}
                variant="scrollable" allowScrollButtonsMobile>
                {TABS.map((t) => <Tab key={t.key} value={t.key} label={t.label} id={`tab-${t.key}`} />)}
              </Tabs>
            </Box>
            {tab === "overview" && <OverviewTab project={data} onChanged={() => reload({ silent: true })} />}
            {tab === "contract" && <ContractTab project={data} />}
            {tab === "payments" && <PaymentsTab projectCode={data.projectCode} />}
            {tab === "evidence" && <EvidenceTab project={data} />}
            {tab === "history" && <AuditLogTable fixedFilters={{ projectCode: data.projectCode }} />}
          </>
        )}
      </DataState>
    </>
  );
}

function Info({ label, value }) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
      <Typography variant="body2" fontWeight={600} sx={{ overflowWrap: "anywhere" }}>{value}</Typography>
    </Box>
  );
}

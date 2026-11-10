import { Box, Button, Chip } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import OverdueTable from "./components/OverdueTable";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";

// A-15: Overdue Alerts (Admin) - UC-44, UC-45
export default function OverdueAlerts() {
  const { data, loading, error, reload } = useAsync(() => adminService.getOverdueAlerts());
  usePolling(() => reload({ silent: true }), 30000);

  const alerts = data ?? [];

  return (
    <>
      <PageHeader
        code="A-15"
        title="Cảnh báo quá hạn"
        subtitle="Giám sát toàn chuỗi: các nhiệm vụ ngày sự kiện đã quá hạn tại tất cả các sảnh và dự án."
        actions={
          <Button
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => reload()}
            disabled={loading}
          >
            Làm mới
          </Button>
        }
      />
      <Box sx={{ mb: 2 }}>
        <Chip
          icon={<WarningAmberIcon />}
          label={`Toàn hệ thống: ${alerts.length} nhiệm vụ đang quá hạn`}
          color={alerts.length > 0 ? "error" : "success"}
          variant="outlined"
          sx={{ fontWeight: "bold" }}
        />
      </Box>
      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!alerts.length}
        emptyMessage="Hiện tại không có nhiệm vụ nào bị quá hạn trên toàn hệ thống."
      >
        <OverdueTable alerts={alerts} />
      </DataState>
    </>
  );
}

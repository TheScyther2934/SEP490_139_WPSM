import { Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import KpiGrid from "./components/KpiGrid";
import OverdueAlertPanel from "./components/OverdueAlertPanel";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";

// A-01: aggregates only, refreshed every 30 s while the tab is visible.
// KPI cards (3 × 2) and the live overdue strip; proof photos are reviewed per project (Admin project detail).
export default function AdminDashboard() {
  const { data, loading, error, reload } = useAsync(() => adminService.getDashboard());
  usePolling(() => reload({ silent: true }));
  return (
    <>
      <PageHeader code="A-01" title="Tổng quan điều hành" subtitle="Số liệu toàn trung tâm, tự cập nhật mỗi 30 giây." />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Stack spacing={3}>
            <KpiGrid data={data} />
            <OverdueAlertPanel alerts={data.overdueAlerts} total={data.overdueTasks} />
          </Stack>
        )}
      </DataState>
    </>
  );
}

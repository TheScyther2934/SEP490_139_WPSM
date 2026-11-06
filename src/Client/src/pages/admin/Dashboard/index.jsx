import { Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import KpiGrid from "./components/KpiGrid";
import OverdueAlertPanel from "./components/OverdueAlertPanel";
import LatestPhotos from "./components/LatestPhotos";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";

// A-01: aggregates only (GBR-12), refreshed every 30 s while the tab is visible.
// Mockup order: KPI cards (3 × 2), live overdue strip, latest proof photos.
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
            <LatestPhotos photos={data.latestPhotos} />
          </Stack>
        )}
      </DataState>
    </>
  );
}

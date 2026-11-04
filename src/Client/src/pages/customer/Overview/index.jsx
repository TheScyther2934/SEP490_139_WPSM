import { Alert, Box, Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CountdownBanner from "./components/CountdownBanner";
import UpcomingTasks from "./components/UpcomingTasks";
import ProgressCards from "./components/ProgressCards";
import LatestPhoto from "./components/LatestPhoto";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";

// C-01
export default function Overview() {
  const { data, loading, error, reload } = useAsync(() => customerService.getDashboard());
  return (
    <>
      <PageHeader code="C-01" title="Tổng quan hôn lễ" />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data === null ? (
          <Alert severity="info">Chưa có dự án hôn lễ nào được gắn với tài khoản của bạn.</Alert>
        ) : data && (
          <>
            <CountdownBanner dashboard={data} />
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" } }}>
              <Stack spacing={2}>
                <UpcomingTasks tasks={data.upcomingTasks ?? []} />
                <LatestPhoto photo={data.latestProofPhoto} />
              </Stack>
              <ProgressCards dashboard={data} />
            </Box>
          </>
        )}
      </DataState>
    </>
  );
}

import { Alert, Box, Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import EventStages from "../../../components/EventStages";
import ContactCard from "../../../components/ContactCard";
import CountdownBanner from "./components/CountdownBanner";
import ProgressCards from "./components/ProgressCards";
import LatestPhoto from "./components/LatestPhoto";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";

// Customer home: countdown, the five stages, money, addon contracts to confirm, who to contact, the latest proof photo.
export default function Overview() {
  const { data, loading, error, reload } = useAsync(() => customerService.getDashboard());
  return (
    <>
      <PageHeader title="Tổng quan sự kiện" />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data === null ? (
          <Alert severity="info">Chưa có sự kiện nào được gắn với tài khoản của bạn.</Alert>
        ) : data && (
          <>
            <CountdownBanner dashboard={data} />
            <Stack spacing={2}>
              <EventStages stages={data.stages ?? []} />
              <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
                <Stack spacing={2}>
                  <ContactCard contacts={data.contacts} hotline={data.hotline} locked={data.currentStage >= 3} />
                  <LatestPhoto photo={data.latestProofPhoto} />
                </Stack>
                <ProgressCards dashboard={data} />
              </Box>
            </Stack>
          </>
        )}
      </DataState>
    </>
  );
}

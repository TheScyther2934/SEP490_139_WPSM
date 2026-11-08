import { Link as RouterLink, useParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import { Alert, Box, Button, Stack } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import AllocationSummary from "./components/AllocationSummary";
import ClashingTable from "./components/ClashingTable";
import ResolveCard from "./components/ResolveCard";
import ExceptionCard from "./components/ExceptionCard";
import { useAsync } from "../../../hooks/useAsync";
import { resourceCalendarService } from "../../../services/resourceCalendarService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";

// M-22 Conflict Alert Detail (UC-26, UC-47). Shows the clashing events of one allocation and the
// reschedule / substitute / release / exception actions.
export default function ConflictDetail() {
  const { allocationId } = useParams();
  const dispatch = useDispatch();
  const { data: allocation, loading, error, reload, setData } = useAsync(() => resourceCalendarService.getAllocation(allocationId));
  const { data: resources } = useAsync(() => resourceCalendarService.getResources());

  const apply = (detail, message) => {
    setData(detail);
    dispatch(notify(message));
  };

  const save = async (body) => {
    const detail = await resourceCalendarService.update(allocationId, body);
    apply(detail, "Đã cập nhật phân bổ tài nguyên.");
  };

  const release = async () => {
    const detail = await resourceCalendarService.release(allocationId);
    apply(detail, "Đã thu hồi phân bổ tài nguyên.");
  };

  const requestException = async (note) => {
    const detail = await resourceCalendarService.requestException(allocationId, note);
    apply(detail, "Đã gửi yêu cầu ngoại lệ tới Admin.");
  };

  return (
    <>
      <PageHeader code="M-22" title={allocation ? `${allocation.resourceName} · ${allocation.projectCode}` : "Chi tiết phân bổ & xung đột"}
        subtitle={allocation ? `${allocation.coupleName} · ${allocation.hallName}` : "Xem các sự kiện chồng lấn và xử lý xung đột tài nguyên."}
        actions={(
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.resourceCalendar}>Lịch tài nguyên</Button>
            {allocation && (
              <Button startIcon={<OpenInNewIcon />} component={RouterLink} to={PATHS.manager.project(allocation.projectCode)}>Mở dự án</Button>
            )}
          </Stack>
        )} />

      <DataState loading={loading} error={error} onRetry={reload}>
        {allocation && (
          <Stack spacing={2}>
            {allocation.hasConflict && (
              <Alert severity="error">
                Xung đột công suất: {allocation.resourceName} đang bị giữ {allocation.otherQuantity}/{allocation.totalQuantity} bởi các phân bổ khác
                trong khung giờ này, thiếu <strong>{allocation.shortage}</strong> so với phân bổ hiện tại.
              </Alert>
            )}
            {allocation.status === "PendingException" && (
              <Alert severity="warning">
                Phân bổ đang chờ Admin quyết định ngoại lệ{allocation.pendingRequestCode ? ` (${allocation.pendingRequestCode})` : ""} nên tạm thời không chiếm công suất.
              </Alert>
            )}
            {allocation.status === "Exception Approved" && (
              <Alert severity="info">Admin đã chấp thuận ngoại lệ cho phân bổ vượt công suất này (GBR-09).</Alert>
            )}
            {allocation.status === "Released" && <Alert severity="info">Phân bổ đã được thu hồi và không còn chiếm công suất.</Alert>}

            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
              <Stack spacing={2}>
                <AllocationSummary allocation={allocation} />
                <ClashingTable clashing={allocation.clashing} />
              </Stack>
              <Stack spacing={2}>
                <ResolveCard allocation={allocation} resources={resources ?? []} onSave={save} onRelease={release} />
                <ExceptionCard allocation={allocation} onRequest={requestException} />
              </Stack>
            </Box>
          </Stack>
        )}
      </DataState>
    </>
  );
}

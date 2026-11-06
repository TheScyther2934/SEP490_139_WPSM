import { useSearchParams } from "react-router-dom";
import { MenuItem, Stack, TextField, Typography } from "@mui/material";
import DataState from "../../../components/DataState";
import ComplaintTable from "../../../components/complaints/ComplaintTable";
import { useAsync } from "../../../hooks/useAsync";
import { useProject } from "../../../hooks/useProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// M-18 (UC-35): complaints of the project, filtered by status on the query string.
export default function Complaints() {
  const { project } = useProject();
  const [params, setParams] = useSearchParams();
  const status = params.get("status") || "";
  const { data, loading, error, reload } = useAsync(() => feedbackService.complaints(project.id), [project.id]);
  const rows = (data ?? []).filter((c) => !status || c.status === status);
  return (
    <>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, justifyContent: "space-between", alignItems: { sm: "center" } }}>
        <Typography variant="h6">Khiếu nại (M-18)</Typography>
        <TextField select size="small" label="Trạng thái" value={status} sx={{ minWidth: 180 }}
          onChange={(e) => setParams(e.target.value ? { status: e.target.value } : {}, { replace: true })}>
          <MenuItem value="">Tất cả</MenuItem>
          <MenuItem value="Open">Đang mở</MenuItem>
          <MenuItem value="Escalated">Đã chuyển Admin</MenuItem>
          <MenuItem value="Resolved">Đã giải quyết</MenuItem>
        </TextField>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload} empty={!rows.length} emptyText="Không có dữ liệu (MSG01).">
        <ComplaintTable rows={rows} linkTo={(c) => PATHS.manager.complaint(project.id, c.code)} />
      </DataState>
    </>
  );
}

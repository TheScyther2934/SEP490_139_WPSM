import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { Box, Button, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DataState from "../../../components/DataState";
import TextDialog from "../../../components/TextDialog";
import ComplaintProgress from "../../../components/complaints/ComplaintProgress";
import EvidencePanel from "./components/EvidencePanel";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// M-19 (UC-35): compare with the BEO and photos, record the response; M-20 escalates to the Admin (UC-36).
export default function ComplaintDetail() {
  const { complaintCode } = useParams();
  const { project } = useProject();
  const { data, loading, error, reload, setData } = useAsync(() => feedbackService.complaint(project.id, complaintCode), [project.id, complaintCode]);
  const { busy, run } = useAction();
  const [dialog, setDialog] = useState(null);
  const act = async (call, message) => {
    const updated = await run(call, message);
    if (updated) { setData(updated); setDialog(null); }
  };
  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6">Khiếu nại {complaintCode} (M-19)</Typography>
        <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.complaints(project.id)}>Danh sách khiếu nại</Button>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload} empty={!data} emptyText="Không tìm thấy khiếu nại.">
        {data && (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "1fr 1fr" }, alignItems: "start" }}>
            <Stack spacing={2}>
              <ComplaintProgress complaint={data} />
              {data.status === "Open" && (
                <Stack direction="row" spacing={1} id="complaintActions">
                  <Button variant="contained" onClick={() => setDialog("resolve")}>Phản hồi và đóng</Button>
                  <Button color="error" variant="outlined" onClick={() => setDialog("escalate")}>Chuyển Admin</Button>
                </Stack>
              )}
            </Stack>
            <EvidencePanel complaint={data} />
          </Box>
        )}
      </DataState>
      <TextDialog open={dialog === "resolve"} title="Phản hồi khiếu nại" label="Phản hồi / hướng giải quyết" required maxLength={1000} busy={busy}
        confirmText="Đã giải quyết" onClose={() => setDialog(null)}
        onConfirm={(text) => act(() => feedbackService.resolve(project.id, complaintCode, text), "Đã giải quyết khiếu nại và báo khách hàng.")} />
      <TextDialog open={dialog === "escalate"} title="Chuyển khiếu nại lên Admin (M-20)" label="Ghi chú chuyển cấp" required maxLength={500} busy={busy}
        confirmText="Chuyển Admin" color="error" onClose={() => setDialog(null)}
        onConfirm={(text) => act(() => feedbackService.escalate(project.id, complaintCode, text), "Đã chuyển khiếu nại lên Admin.")} />
    </>
  );
}

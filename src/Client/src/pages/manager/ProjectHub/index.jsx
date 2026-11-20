import { Link as RouterLink } from "react-router-dom";
import { useDispatch } from "react-redux";
import { useState } from "react";
import { Alert, Box, Button, Stack } from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import BlockIcon from "@mui/icons-material/Block";
import ModuleCards from "./components/ModuleCards";
import CustomerAccountCard from "./components/CustomerAccountCard";
import StageNotes from "./components/StageNotes";
import MoneyLimits from "./components/MoneyLimits";
import EventStages from "../../../components/EventStages";
import ProjectOperators from "../../../components/ProjectOperators";
import TextDialog from "../../../components/TextDialog";
import DataState from "../../../components/DataState";
import { useProject } from "../../../hooks/useProject";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { eventFlowService } from "../../../services/eventFlowService";
import { loadProject } from "../../../store/projectSlice";
import { PATHS } from "../../../routes/paths";
import { dateVi } from "../../../utils/format";

// Overview tab of an event: the five stages, the money limits of stage 2, stage notes, operators and the customer account.
export default function ProjectHub() {
  const dispatch = useDispatch();
  const { project } = useProject();
  const workspace = useAsync(() => eventFlowService.workspace(project.id), [project.id, project.status]);
  const { busy, run } = useAction();
  const [cancelOpen, setCancelOpen] = useState(false);
  const ws = workspace.data;

  const cancel = async (reason) => {
    const done = await run(() => eventFlowService.cancelEvent(project.id, reason), "Đã hủy sự kiện. Tiền đặt cọc không hoàn lại.");
    if (done) { setCancelOpen(false); dispatch(loadProject(project.id)); }
  };

  return (
    <Stack spacing={2}>
      {project.beoStatus === "Stale" && (
        <Alert severity="error" action={<Button color="inherit" component={RouterLink} to={PATHS.manager.beo(project.id)}>Mở phiếu điều phối</Button>}>
          Phiếu điều phối đã cũ vì khách vừa xác nhận hợp đồng phát sinh. Hãy duyệt lại để gửi các bộ phận.
        </Alert>
      )}
      {project.status === "Adjusting" && !project.canAdjust && (
        <Alert severity="warning">Đã qua hạn điều chỉnh ({dateVi(project.lockDate)}). Hệ thống sẽ khóa dịch vụ trong lần chạy tiếp theo.</Alert>
      )}
      {project.status === "Locked" && project.pendingAddons > 0 && (
        <Alert severity="warning" action={<Button color="inherit" component={RouterLink} to={PATHS.manager.addons(project.id)}>Xem</Button>}>
          Có {project.pendingAddons} hợp đồng phát sinh đang chờ khách xác nhận. Hãy nhắc khách xác nhận trước khi thực hiện.
        </Alert>
      )}
      <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
        {ws?.canCancel && <Button color="error" startIcon={<BlockIcon />} onClick={() => setCancelOpen(true)}>Khách hủy sự kiện</Button>}
        <Button startIcon={<PrintIcon />} onClick={() => window.print()}>In tổng hợp</Button>
      </Stack>
      <EventStages stages={project.stages} />
      <ModuleCards project={project} />
      <DataState loading={workspace.loading} error={workspace.error} onRetry={workspace.reload}>
        {ws && (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" } }}>
            <Stack spacing={2}>
              <MoneyLimits workspace={ws} />
              <StageNotes code={project.id} notes={ws.notes} readOnly={project.status === "Cancelled"} onAdded={() => workspace.reload({ silent: true })} />
            </Stack>
            <Stack spacing={2}>
              <CustomerAccountCard project={project} />
              <ProjectOperators projectCode={project.id} />
            </Stack>
          </Box>
        )}
      </DataState>
      <TextDialog open={cancelOpen} title="Khách hủy sự kiện" color="error" confirmText="Xác nhận hủy" busy={busy}
        description="Theo hợp đồng, khách hủy thì không hoàn tiền đặt cọc. Sảnh, ca và ngày sẽ được mở lại cho khách khác. Thao tác không hoàn tác được."
        label="Lý do hủy" required onClose={() => setCancelOpen(false)} onConfirm={cancel} />
    </Stack>
  );
}

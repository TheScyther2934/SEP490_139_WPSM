import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Stack, Typography } from "@mui/material";
import DataState from "../../../components/DataState";
import TaskCard from "./components/TaskCard";
import ProofUploadDialog from "./components/ProofUploadDialog";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { dayOfService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";
import { dateVi } from "../../../utils/format";

// M-16 (UC-30) with the M-17 upload dialog (UC-31). Refreshes every 30 s so JOB-05 overdue flags appear.
export default function DayOfBoard() {
  const { project } = useProject();
  const { data: board, loading, error, reload, setData } = useAsync(() => dayOfService.get(project.id), [project.id]);
  usePolling(() => reload({ silent: true }));
  const { busy, run } = useAction();
  const [task, setTask] = useState(null);

  const complete = async (files) => {
    const updated = await run(() => dayOfService.complete(project.id, task.taskId, files), "Đã hoàn thành công việc.");
    if (updated) { setData(updated); setTask(null); }
  };
  const load = async () => {
    const updated = await run(() => dayOfService.load(project.id), "Đã tải lịch trình ngày cưới.");
    if (updated) setData(updated);
  };

  return (
    <>
      <Typography variant="h6" sx={{ mb: 2 }}>Bảng điều phối ngày cưới (M-16)</Typography>
      <DataState loading={loading} error={error} onRetry={reload}>
        {board && (
          <Stack spacing={2} id="dayOfBoard">
            <Typography color="text.secondary">Ngày {dateVi(board.eventDate)} · ca {board.slotTime}</Typography>
            {board.beoStatus === "Stale" && (
              <Alert severity="error" action={<Button color="inherit" component={RouterLink} to={PATHS.manager.beo(project.id)}>Mở BEO</Button>}>
                BEO đang lỗi thời. Hãy duyệt lại trước khi thực hiện (UC-29).
              </Alert>
            )}
            {board.tasks.length === 0 ? (
              <Alert severity="info" action={<Button color="inherit" onClick={load} disabled={busy}>Tải lịch trình</Button>}>
                Lịch trình được hệ thống tạo lúc 00:00 ngày cưới (JOB-04). Manager có thể tải trước từ một ngày trước tiệc.
              </Alert>
            ) : board.tasks.map((t) => <TaskCard key={t.taskId} task={t} onComplete={setTask} />)}
          </Stack>
        )}
      </DataState>
      <ProofUploadDialog task={task} busy={busy} onClose={() => setTask(null)} onComplete={complete} />
    </>
  );
}

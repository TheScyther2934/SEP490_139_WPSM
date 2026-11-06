import { useSearchParams } from "react-router-dom";
import { Alert, MenuItem, Stack, TextField, Typography } from "@mui/material";
import DataState from "../../../components/DataState";
import RoadmapTable from "./components/RoadmapTable";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { managerService } from "../../../services/operationsService";

// M-06 (UC-21): all milestones of the project; the Manager marks own tasks done. Owner filter on the query string.
export default function Roadmap() {
  const { project } = useProject();
  const [params, setParams] = useSearchParams();
  const owner = params.get("owner") || "";
  const { data, loading, error, reload } = useAsync(() => managerService.getRoadmap(project.id), [project.id]);
  const { busy, run } = useAction();
  const tasks = (data ?? []).filter((t) => !owner || t.assigneeRole === owner);
  const complete = async (task) => {
    if (await run(() => managerService.completeTask(project.id, task.taskId), "Đã đánh dấu hoàn thành.")) reload({ silent: true });
  };
  return (
    <>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, justifyContent: "space-between", alignItems: { sm: "center" } }}>
        <Typography variant="h6">Lộ trình và nhiệm vụ (M-06)</Typography>
        <TextField select size="small" label="Phụ trách" value={owner} sx={{ minWidth: 200 }}
          onChange={(e) => setParams(e.target.value ? { owner: e.target.value } : {}, { replace: true })}>
          <MenuItem value="">Tất cả</MenuItem>
          <MenuItem value="Manager">Nhiệm vụ của quản lý</MenuItem>
          <MenuItem value="Customer">Nhiệm vụ của khách hàng</MenuItem>
        </TextField>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload}>
        {data?.length === 0 ? <Alert severity="info">Lộ trình được tạo sau khi baseline được đóng băng.</Alert>
          : <RoadmapTable tasks={tasks} busy={busy} onComplete={complete} />}
      </DataState>
    </>
  );
}

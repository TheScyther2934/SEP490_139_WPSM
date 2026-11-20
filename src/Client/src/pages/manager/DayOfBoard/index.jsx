import { useEffect, useState } from "react";
import { Link as RouterLink, useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Stack, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import PlaylistAddIcon from "@mui/icons-material/PlaylistAdd";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useProject } from "../../../hooks/useProject";
import { useCoordinationTasks } from "../../../components/coordination/useCoordinationTasks";
import { TASK_FILTERS, taskStats } from "../../../components/coordination/departments";
import { PATHS } from "../../../routes/paths";
import { dateVi } from "../../../utils/format";

function Summary({ tasks }) {
  const s = taskStats(tasks);
  const items = [
    { label: "Công việc đã xong", value: `${s.done}/${s.total}`, color: s.done === s.total ? "success.main" : "text.primary" },
    { label: "Đang quá hạn", value: s.overdue, color: s.overdue ? "error.main" : "text.primary" },
    { label: "Đã xong nhưng thiếu minh chứng", value: s.missing, color: s.missing ? "warning.main" : "text.primary" },
    { label: "Minh chứng", value: s.proofs, color: "text.primary" },
  ];
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, mb: 2 }}>
      {items.map((i) => (
        <Card key={i.label}><CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
          <Typography variant="caption" color="text.secondary">{i.label}</Typography>
          <Typography variant="h5" sx={{ color: i.color, fontWeight: 700 }}>{i.value}</Typography>
        </CardContent></Card>
      ))}
    </Box>
  );
}

// Event-day board: the coordination tasks due on the event day, in time order, with their department.
// Preparation tasks (before the event day) live on the department sheets; both share the same tasks and proof.
export default function DayOfBoard() {
  const { project } = useProject();
  const code = project.id;
  const [searchParams] = useSearchParams();
  const targetTaskId = Number(searchParams.get("taskId"));
  const coord = useCoordinationTasks(code);
  const [filter, setFilter] = useState("all");

  const data = coord.data;
  const allTasks = coord.tasks;
  const tasks = allTasks.filter((t) => t.isEventDay);
  const prep = taskStats(allTasks.filter((t) => !t.isEventDay));
  const shown = tasks.filter(TASK_FILTERS[filter].test);
  const missing = tasks.filter((t) => t.missingProof).length;

  useEffect(() => {
    if (!targetTaskId || !data) return undefined;
    const timer = setTimeout(() => document.getElementById(`task-${targetTaskId}`)?.scrollIntoView({ behavior: "smooth", block: "center" }), 300);
    return () => clearTimeout(timer);
  }, [targetTaskId, data]);

  return (
    <>
      <PageHeader title="Công việc ngày cưới"
        subtitle={data ? `${data.coupleName} · ${dateVi(data.eventDate)} · ca ${data.slotTime}` : `Sự kiện ${code}`}
        actions={<>
          <Button component={RouterLink} to={PATHS.manager.departmentSheets(code)}>Điều phối theo bộ phận</Button>
          <Button startIcon={<RefreshIcon />} onClick={() => coord.board.reload()} disabled={coord.board.loading}>Làm mới</Button>
        </>} />

      <DataState loading={coord.board.loading} error={coord.board.error} onRetry={coord.board.reload}>
        {data && (
          <>
            {data.canAddPhotos && missing > 0 && (
              <Alert severity="warning" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => setFilter("missing")}>Xem</Button>}>
                Có {missing} công việc đã đánh dấu xong nhưng chưa có ảnh hoặc tài liệu chứng minh. Hãy bổ sung trước khi tất toán với khách.
              </Alert>
            )}
            {prep.total > 0 && prep.done < prep.total && (
              <Alert severity={prep.overdue ? "error" : "info"} sx={{ mb: 2 }}
                action={<Button color="inherit" component={RouterLink} to={PATHS.manager.departmentSheets(code)}>Xem</Button>}>
                Nhiệm vụ chuẩn bị trước ngày cưới: {prep.done}/{prep.total} đã xong{prep.overdue ? `, ${prep.overdue} quá hạn` : ""}.
              </Alert>
            )}
            {data.projectStatus === "Adjusting" && (
              <Alert severity="info" sx={{ mb: 2 }}>Công việc được tạo và mở cập nhật ngay sau khi khóa dịch vụ.</Alert>
            )}
            {tasks.length === 0 ? (
              <Card><CardContent>
                <Typography sx={{ mb: 2 }}>Chưa có công việc ngày cưới. Tạo nhiệm vụ điều phối từ các dịch vụ đã khóa để có lịch trình ngày cưới và các việc chuẩn bị.</Typography>
                <Button variant="contained" startIcon={<PlaylistAddIcon />} disabled={coord.busy || data.projectStatus !== "Locked"} onClick={coord.generate}>
                  Tạo nhiệm vụ điều phối
                </Button>
              </CardContent></Card>
            ) : (
              <>
                <Summary tasks={tasks} />
                <ToggleButtonGroup size="small" exclusive value={filter} onChange={(_, v) => v && setFilter(v)} sx={{ mb: 2, flexWrap: "wrap" }}>
                  {Object.entries(TASK_FILTERS).map(([key, f]) => (
                    <ToggleButton key={key} value={key}>{f.label} ({tasks.filter(f.test).length})</ToggleButton>
                  ))}
                </ToggleButtonGroup>
                <Stack spacing={1.5}>
                  {shown.map((t) => coord.renderTask(t, { showDepartment: true }))}
                  {shown.length === 0 && <Typography color="text.secondary">Không có công việc nào trong mục này.</Typography>}
                </Stack>
              </>
            )}
          </>
        )}
      </DataState>
      {coord.dialogs}
    </>
  );
}

import { useCallback, useEffect, useState } from "react";
import { Link as RouterLink, useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Stack, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import PlaylistAddIcon from "@mui/icons-material/PlaylistAdd";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import TaskCard from "./components/TaskCard";
import ProofUploadDialog from "./components/ProofUploadDialog";
import PhotoViewer from "./components/PhotoViewer";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { dayOfService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";
import { dateVi } from "../../../utils/format";

const FILTERS = {
  all: { label: "Tất cả", test: () => true },
  todo: { label: "Chưa xong", test: (t) => t.status !== "Done" },
  missing: { label: "Thiếu ảnh", test: (t) => t.missingProof },
  done: { label: "Đã xong", test: (t) => t.status === "Done" },
};

function Summary({ tasks }) {
  const done = tasks.filter((t) => t.status === "Done").length;
  const overdue = tasks.filter((t) => t.status === "Overdue").length;
  const missing = tasks.filter((t) => t.missingProof).length;
  const photos = tasks.reduce((n, t) => n + t.photos.length, 0);
  const items = [
    { label: "Công việc đã xong", value: `${done}/${tasks.length}`, color: done === tasks.length ? "success.main" : "text.primary" },
    { label: "Đang quá hạn", value: overdue, color: overdue ? "error.main" : "text.primary" },
    { label: "Đã xong nhưng thiếu ảnh", value: missing, color: missing ? "warning.main" : "text.primary" },
    { label: "Ảnh minh chứng", value: photos, color: "text.primary" },
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

// Event-day board: the timeline of tasks, completing them with proof photos, adding photos to tasks finished without them.
export default function DayOfBoard() {
  const { project } = useProject();
  const code = project.id;
  const [searchParams] = useSearchParams();
  const targetTaskId = Number(searchParams.get("taskId"));
  const board = useAsync(() => dayOfService.get(code), [code]);
  const { busy, run } = useAction();
  const [dialog, setDialog] = useState(null);   // { task, mode: "complete" | "add" }
  const [viewer, setViewer] = useState(null);   // { title, photos, index }
  const [filter, setFilter] = useState("all");

  const data = board.data;
  const tasks = data?.tasks ?? [];
  const shown = tasks.filter(FILTERS[filter].test);
  const missing = tasks.filter((t) => t.missingProof).length;

  useEffect(() => {
    if (!targetTaskId || !data) return;
    const timer = setTimeout(() => document.getElementById(`task-${targetTaskId}`)?.scrollIntoView({ behavior: "smooth", block: "center" }), 300);
    return () => clearTimeout(timer);
  }, [targetTaskId, data]);

  const submit = async (files) => {
    const { task, mode } = dialog;
    const result = mode === "add"
      ? await run(() => dayOfService.addPhotos(code, task.taskId, files), `Đã tải ${files.length} ảnh minh chứng cho "${task.taskName}".`)
      : await run(() => dayOfService.complete(code, task.taskId, files), `Đã hoàn thành "${task.taskName}".`);
    if (result) { setDialog(null); board.reload({ silent: true }); }
  };
  const loadTimeline = async () => {
    if (await run(() => dayOfService.load(code), "Đã tải lịch trình ngày cưới.")) board.reload({ silent: true });
  };
  const moveViewer = useCallback((index) => setViewer((v) => (v ? { ...v, index } : v)), []);

  return (
    <>
      <PageHeader title="Công việc ngày cưới"
        subtitle={data ? `${data.coupleName} · ${dateVi(data.eventDate)} · ca ${data.slotTime}` : `Sự kiện ${code}`}
        actions={<>
          <Button component={RouterLink} to={PATHS.manager.project(code)}>Về sự kiện</Button>
          <Button startIcon={<RefreshIcon />} onClick={() => board.reload()} disabled={board.loading}>Làm mới</Button>
        </>} />

      <DataState loading={board.loading} error={board.error} onRetry={board.reload}>
        {data && (
          <>
            {data.canAddPhotos && missing > 0 && (
              <Alert severity="warning" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => setFilter("missing")}>Xem</Button>}>
                Có {missing} công việc đã đánh dấu xong nhưng chưa có ảnh chứng minh kết quả. Hãy bổ sung ảnh trước khi tất toán với khách.
              </Alert>
            )}
            {!data.canAddPhotos && data.projectStatus !== "Settled" && data.projectStatus !== "Closed" && (
              <Alert severity="info" sx={{ mb: 2 }}>Chỉ hoàn thành công việc và tải ảnh minh chứng từ ngày cưới ({dateVi(data.eventDate)}).</Alert>
            )}
            {tasks.length === 0 ? (
              <Card><CardContent>
                <Typography sx={{ mb: 2 }}>Chưa có công việc ngày cưới. Lịch trình được tải tự động vào sáng ngày cưới, hoặc tải trước từ một ngày trước tiệc.</Typography>
                <Button variant="contained" startIcon={<PlaylistAddIcon />} disabled={busy || data.projectStatus !== "Locked"} onClick={loadTimeline}>Tải lịch trình ngày cưới</Button>
              </CardContent></Card>
            ) : (
              <>
                <Summary tasks={tasks} />
                <ToggleButtonGroup size="small" exclusive value={filter} onChange={(_, v) => v && setFilter(v)} sx={{ mb: 2, flexWrap: "wrap" }}>
                  {Object.entries(FILTERS).map(([key, f]) => (
                    <ToggleButton key={key} value={key}>{f.label} ({tasks.filter(f.test).length})</ToggleButton>
                  ))}
                </ToggleButtonGroup>
                <Stack spacing={2}>
                  {shown.map((task) => (
                    <TaskCard key={task.taskId} task={task} canAddPhotos={data.canAddPhotos} maxPhotos={data.maxPhotosPerTask}
                      onComplete={(t) => setDialog({ task: t, mode: "complete" })}
                      onAddPhotos={(t) => setDialog({ task: t, mode: "add" })}
                      onOpenPhoto={(t, index) => setViewer({ title: t.taskName, photos: t.photos, index })} />
                  ))}
                  {shown.length === 0 && <Typography color="text.secondary">Không có công việc nào trong mục này.</Typography>}
                </Stack>
              </>
            )}
          </>
        )}
      </DataState>

      <ProofUploadDialog task={dialog?.task} mode={dialog?.mode} maxPhotos={data?.maxPhotosPerTask ?? 6} busy={busy}
        onClose={() => setDialog(null)} onSubmit={submit} />
      <PhotoViewer viewer={viewer} onClose={() => setViewer(null)} onMove={moveViewer} />
    </>
  );
}

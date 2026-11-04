import { useState, useEffect, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Grid,
  LinearProgress,
  MenuItem,
  Snackbar,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import CalendarMonthIcon from "@mui/icons-material/CalendarMonth";
import AccessTimeIcon from "@mui/icons-material/AccessTime";
import RoomIcon from "@mui/icons-material/Room";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import RoadmapTable from "./components/RoadmapTable";
import { useProject } from "../../../hooks/useProject";
import { projectService } from "../../../services/projectService";
import { dateVi, countdownLabel, errorMessage } from "../../../utils/format";

const MILESTONES = ["T-90", "T-30", "T-7", "Event Day"];

export default function Roadmap() {
  const { project, roadmap: initialRoadmap } = useProject();
  const [params, setParams] = useSearchParams();
  const owner = params.get("owner") || "";

  const [tasks, setTasks] = useState(initialRoadmap ?? []);
  const [loading, setLoading] = useState(false);
  const [completeDialogOpen, setCompleteDialogOpen] = useState(false);
  const [targetTask, setTargetTask] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState({ open: false, message: "", severity: "success" });

  // Load roadmap from API whenever project or owner changes
  useEffect(() => {
    if (!project?.id) return;
    let active = true;
    setLoading(true);
    projectService
      .getRoadmap(project.id, owner)
      .then((data) => {
        if (!active) return;
        if (data?.tasks) {
          setTasks(data.tasks);
        }
      })
      .catch((err) => {
        if (!active) return;
        // If API fails or fallback, filter local tasks
        const localTasks = (initialRoadmap ?? []).filter(
          (t) => !owner || t.assigneeRole === owner
        );
        setTasks(localTasks);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [project?.id, owner, initialRoadmap]);

  // Overall statistics (computed from all project tasks)
  const allTasks = useMemo(() => initialRoadmap ?? [], [initialRoadmap]);
  const totalCount = allTasks.length;
  const doneCount = allTasks.filter((t) => t.status === "Done").length;
  const overdueCount = allTasks.filter((t) => t.status === "Overdue").length;
  const managerTasksCount = allTasks.filter((t) => t.assigneeRole === "Manager").length;
  const customerTasksCount = allTasks.filter((t) => t.assigneeRole === "Customer").length;
  const progressPct = totalCount > 0 ? Math.round((doneCount / totalCount) * 100) : 0;

  // Milestone breakdown
  const milestoneStats = useMemo(() => {
    return MILESTONES.map((m) => {
      const groupTasks = allTasks.filter((t) => t.milestone === m);
      const groupDone = groupTasks.filter((t) => t.status === "Done").length;
      return {
        milestone: m,
        total: groupTasks.length,
        done: groupDone
      };
    });
  }, [allTasks]);

  // Filtered displayed tasks
  const displayedTasks = useMemo(() => {
    return tasks.filter((t) => !owner || t.assigneeRole === owner);
  }, [tasks, owner]);

  // Handle Complete Task Dialog
  const handleOpenCompleteDialog = (task) => {
    setTargetTask(task);
    setCompleteDialogOpen(true);
  };

  const handleCloseCompleteDialog = () => {
    if (submitting) return;
    setCompleteDialogOpen(false);
    setTargetTask(null);
  };

  const handleConfirmComplete = async () => {
    if (!targetTask || !project?.id) return;
    setSubmitting(true);
    try {
      const res = await projectService.completeRoadmapTask(project.id, targetTask.taskId);
      if (res?.success) {
        // Update local state without reloading the whole page
        const updated = res.updatedTask;
        setTasks((prev) =>
          prev.map((t) => (t.taskId === targetTask.taskId ? { ...t, ...updated, status: "Done", completedAt: new Date().toISOString() } : t))
        );
        setToast({
          open: true,
          message: `Đã hoàn thành: "${targetTask.title}"`,
          severity: "success"
        });
      } else {
        setToast({
          open: true,
          message: res?.message || "Không thể hoàn thành nhiệm vụ.",
          severity: "error"
        });
      }
      setCompleteDialogOpen(false);
      setTargetTask(null);
    } catch (err) {
      setToast({
        open: true,
        message: errorMessage(err) || "Có lỗi xảy ra khi xác nhận hoàn thành nhiệm vụ.",
        severity: "error"
      });
    } finally {
      setSubmitting(false);
    }
  };

  if (!project) {
    return (
      <Box sx={{ p: 3, textAlign: "center" }}>
        <Typography color="text.secondary">Đang tải dữ liệu dự án...</Typography>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      {/* ── PROJECT HEADER & COUNTDOWN CARD ────────────────────────────── */}
      <Card elevation={2} sx={{ p: 3, borderRadius: 2 }}>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} md={7}>
            <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
              <Typography variant="h5" fontWeight={700} color="primary.main">
                {project.id}
              </Typography>
              <Typography variant="h6" color="text.primary">
                — {project.coupleName || "Dự án Tiệc Cưới"}
              </Typography>
            </Stack>

            <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ color: "text.secondary", mt: 1 }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <RoomIcon fontSize="small" color="action" />
                <Typography variant="body2">{project.hall || "Chưa phân sảnh"}</Typography>
              </Box>
              <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <AccessTimeIcon fontSize="small" color="action" />
                <Typography variant="body2">{project.slot || "Chưa phân ca"}</Typography>
              </Box>
              <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <CalendarMonthIcon fontSize="small" color="action" />
                <Typography variant="body2">{dateVi(project.eventDate)}</Typography>
              </Box>
            </Stack>
          </Grid>

          <Grid item xs={12} md={5}>
            <Box
              sx={{
                p: 2,
                borderRadius: 2,
                bgcolor: "primary.lighter",
                border: "1px solid",
                borderColor: "primary.light",
                textAlign: { xs: "left", md: "right" }
              }}
            >
              <Typography variant="caption" fontWeight={700} color="primary.dark" textTransform="uppercase">
                Đếm ngược ngày cưới
              </Typography>
              <Typography variant="h4" fontWeight={800} color="primary.dark" sx={{ my: 0.5 }}>
                {countdownLabel(project.eventDate)}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Tiến độ: {doneCount}/{totalCount} nhiệm vụ ({progressPct}%)
              </Typography>
              <LinearProgress
                variant="determinate"
                value={progressPct}
                sx={{ height: 8, borderRadius: 4, mt: 1, bgcolor: "grey.200" }}
              />
            </Box>
          </Grid>
        </Grid>
      </Card>

      {/* ── METRIC CARDS ─────────────────────────────────────────────────── */}
      <Grid container spacing={2}>
        <Grid item xs={6} sm={3}>
          <Card sx={{ p: 2, textAlign: "center", bgcolor: "background.paper" }}>
            <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase">
              Tổng số nhiệm vụ
            </Typography>
            <Typography variant="h5" fontWeight={700} sx={{ mt: 0.5 }}>
              {totalCount}
            </Typography>
          </Card>
        </Grid>

        <Grid item xs={6} sm={3}>
          <Card sx={{ p: 2, textAlign: "center", bgcolor: "background.paper" }}>
            <Typography variant="caption" color="primary.main" fontWeight={600} textTransform="uppercase">
              Quản lý phụ trách
            </Typography>
            <Typography variant="h5" fontWeight={700} color="primary.main" sx={{ mt: 0.5 }}>
              {managerTasksCount}
            </Typography>
          </Card>
        </Grid>

        <Grid item xs={6} sm={3}>
          <Card sx={{ p: 2, textAlign: "center", bgcolor: "background.paper" }}>
            <Typography variant="caption" color="secondary.main" fontWeight={600} textTransform="uppercase">
              Khách hàng phụ trách
            </Typography>
            <Typography variant="h5" fontWeight={700} color="secondary.main" sx={{ mt: 0.5 }}>
              {customerTasksCount}
            </Typography>
          </Card>
        </Grid>

        <Grid item xs={6} sm={3}>
          <Card sx={{ p: 2, textAlign: "center", bgcolor: overdueCount > 0 ? "error.lighter" : "background.paper" }}>
            <Typography variant="caption" color={overdueCount > 0 ? "error.dark" : "text.secondary"} fontWeight={600} textTransform="uppercase">
              Quá hạn
            </Typography>
            <Typography variant="h5" fontWeight={700} color={overdueCount > 0 ? "error.main" : "text.primary"} sx={{ mt: 0.5 }}>
              {overdueCount}
            </Typography>
          </Card>
        </Grid>
      </Grid>

      {/* ── 4 MILESTONES SUMMARY STRIP ─────────────────────────────────────── */}
      <Card sx={{ p: 2 }}>
        <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1.5, textTransform: "uppercase", letterSpacing: 0.5 }}>
          Cột mốc vận hành (Lifecycle Milestones)
        </Typography>
        <Grid container spacing={1.5}>
          {milestoneStats.map((ms) => {
            const isFinished = ms.total > 0 && ms.done === ms.total;
            return (
              <Grid item xs={6} sm={3} key={ms.milestone}>
                <Box
                  sx={{
                    p: 1.5,
                    borderRadius: 1.5,
                    border: "1px solid",
                    borderColor: isFinished ? "success.light" : "divider",
                    bgcolor: isFinished ? "success.lighter" : "background.neutral",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center"
                  }}
                >
                  <Box>
                    <Typography variant="body2" fontWeight={700}>
                      {ms.milestone}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {ms.done}/{ms.total} hoàn thành
                    </Typography>
                  </Box>
                  {isFinished && <CheckCircleIcon color="success" fontSize="small" />}
                </Box>
              </Grid>
            );
          })}
        </Grid>
      </Card>

      {/* ── CONTROLS & OWNER FILTER ────────────────────────────────────────── */}
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}
      >
        <Box>
          <Typography variant="h6" fontWeight={700}>
            Danh sách nhiệm vụ chi tiết (M-06)
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Hiển thị {displayedTasks.length} nhiệm vụ được sắp xếp theo hạn chót
          </Typography>
        </Box>

        <TextField
          select
          size="small"
          label="Lọc theo người phụ trách"
          value={owner}
          sx={{ minWidth: 240 }}
          onChange={(e) =>
            setParams(e.target.value ? { owner: e.target.value } : {}, { replace: true })
          }
        >
          <MenuItem value="">Tất cả nhiệm vụ (All)</MenuItem>
          <MenuItem value="Manager">Nhiệm vụ của Quản lý (Manager)</MenuItem>
          <MenuItem value="Customer">Nhiệm vụ của Khách hàng (Customer)</MenuItem>
        </TextField>
      </Stack>

      {/* ── TASK TABLE OR EMPTY STATE ──────────────────────────────────────── */}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
          <CircularProgress />
        </Box>
      ) : allTasks.length === 0 ? (
        <Alert severity="info" sx={{ py: 3 }}>
          Lộ trình sẽ được tạo tự động sau khi baseline dự án được đóng băng.
        </Alert>
      ) : (
        <RoadmapTable tasks={displayedTasks} onCompleteTask={handleOpenCompleteDialog} />
      )}

      {/* ── CONFIRMATION DIALOG FOR MARK AS DONE ──────────────────────────── */}
      <Dialog open={completeDialogOpen} onClose={handleCloseCompleteDialog} maxWidth="xs" fullWidth>
        <DialogTitle sx={{ fontWeight: 700 }}>Xác nhận hoàn thành</DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ mb: 2 }}>
            Bạn có chắc chắn muốn xác nhận đã hoàn thành nhiệm vụ này không?
          </DialogContentText>
          {targetTask && (
            <Box sx={{ p: 2, bgcolor: "background.neutral", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="subtitle2" fontWeight={600} color="primary.main">
                {targetTask.title}
              </Typography>
              <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.5 }}>
                Cột mốc: <strong>{targetTask.milestone}</strong> · Hạn chót: <strong>{dateVi(targetTask.dueDate)}</strong>
              </Typography>
            </Box>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button onClick={handleCloseCompleteDialog} disabled={submitting} color="inherit">
            Hủy
          </Button>
          <Button
            onClick={handleConfirmComplete}
            variant="contained"
            color="primary"
            disabled={submitting}
            autoFocus
          >
            {submitting ? "Đang xử lý..." : "Xác nhận xong"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── TOAST NOTIFICATION ────────────────────────────────────────────── */}
      <Snackbar
        open={toast.open}
        autoHideDuration={4000}
        onClose={() => setToast((prev) => ({ ...prev, open: false }))}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        <Alert
          severity={toast.severity}
          onClose={() => setToast((prev) => ({ ...prev, open: false }))}
          sx={{ width: "100%", boxShadow: 3 }}
        >
          {toast.message}
        </Alert>
      </Snackbar>
    </Stack>
  );
}

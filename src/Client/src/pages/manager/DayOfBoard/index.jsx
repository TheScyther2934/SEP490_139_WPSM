import { useEffect, useRef, useState } from "react";
import { useParams, useSearchParams, Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import AccessTimeIcon from "@mui/icons-material/AccessTime";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import CloseIcon from "@mui/icons-material/Close";
import RefreshIcon from "@mui/icons-material/Refresh";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync } from "../../../hooks/useAsync";
import { managerService } from "../../../services/managerService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// M-16: Day-of Board & M-17: Task Completion with Proof
export default function DayOfBoard() {
  const { projectCode } = useParams();
  const [searchParams] = useSearchParams();
  const targetTaskId = Number(searchParams.get("taskId"));

  const { data, loading, error, reload } = useAsync(
    () => managerService.getDayOfTasks(projectCode),
    [projectCode]
  );

  const [activeTask, setActiveTask] = useState(null);
  const [selectedFiles, setSelectedFiles] = useState([]);
  const [submitError, setSubmitError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [successMsg, setSuccessMsg] = useState("");

  const taskRefs = useRef({});

  // Auto-scroll to target task if passed via ?taskId= query param
  useEffect(() => {
    if (targetTaskId && data && taskRefs.current[targetTaskId]) {
      setTimeout(() => {
        taskRefs.current[targetTaskId].scrollIntoView({
          behavior: "smooth",
          block: "center",
        });
      }, 300);
    }
  }, [targetTaskId, data]);

  const handleOpenCompleteDialog = (task) => {
    setActiveTask(task);
    setSelectedFiles([]);
    setSubmitError("");
  };

  const handleCloseDialog = () => {
    setActiveTask(null);
    setSelectedFiles([]);
    setSubmitError("");
  };

  const handleFileChange = (e) => {
    const files = Array.from(e.target.files || []);
    if (!files.length) return;

    if (selectedFiles.length + files.length > 3) {
      setSubmitError("Nhiệm vụ này chỉ cho phép tải lên tối đa 3 ảnh minh chứng.");
      return;
    }

    const invalid = files.find(
      (f) =>
        f.size > 10 * 1024 * 1024 ||
        !["image/jpeg", "image/png", "image/jpg"].includes(f.type)
    );

    if (invalid) {
      setSubmitError(
        "Ảnh không hợp lệ. Chỉ chấp nhận định dạng JPG/PNG dung lượng tối đa 10 MB mỗi ảnh (MSG17)."
      );
      return;
    }

    setSubmitError("");
    setSelectedFiles((prev) => [...prev, ...files]);
  };

  const handleRemoveFile = (index) => {
    setSelectedFiles((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSubmitComplete = async () => {
    if (!activeTask) return;

    if (activeTask.photoRequired && selectedFiles.length < 1) {
      setSubmitError("Nhiệm vụ này yêu cầu từ 1 đến 3 ảnh minh chứng (MSG10).");
      return;
    }

    setIsSubmitting(true);
    setSubmitError("");

    try {
      const formData = new FormData();
      selectedFiles.forEach((file) => {
        formData.append("photos", file);
      });

      await managerService.completeDayOfTask(projectCode, activeTask.taskId, formData);
      setSuccessMsg(`Nhiệm vụ "${activeTask.taskName}" đã được hoàn thành thành công.`);
      handleCloseDialog();
      reload();
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Lỗi khi hoàn thành nhiệm vụ.";
      setSubmitError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const tasks = data ?? [];

  return (
    <>
      <PageHeader
        code="M-16"
        title="Bảng điều phối ngày sự kiện"
        subtitle={`Theo dõi các nhiệm vụ vận hành trực tiếp trong ngày tiệc của dự án ${projectCode}.`}
        actions={
          <Stack direction="row" spacing={1}>
            <Button
              variant="outlined"
              startIcon={<ArrowBackIcon />}
              component={RouterLink}
              to={PATHS.manager.project(projectCode)}
            >
              Về dự án
            </Button>
            <Button
              variant="outlined"
              startIcon={<RefreshIcon />}
              onClick={() => reload()}
              disabled={loading}
            >
              Làm mới
            </Button>
          </Stack>
        }
      />

      {successMsg && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccessMsg("")}>
          {successMsg}
        </Alert>
      )}

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!tasks.length}
        emptyMessage="Chưa có nhiệm vụ ngày sự kiện nào được tạo cho dự án này."
      >
        <Stack spacing={2}>
          {tasks.map((task) => {
            const isTarget = task.taskId === targetTaskId;
            const isOverdue = task.status === "Overdue";
            const isDone = task.status === "Done";

            return (
              <Card
                key={task.taskId}
                ref={(el) => (taskRefs.current[task.taskId] = el)}
                sx={{
                  border: isTarget
                    ? "2px solid #d32f2f"
                    : isOverdue
                    ? "1px solid #ef5350"
                    : undefined,
                  boxShadow: isTarget ? "0 0 12px rgba(211, 47, 47, 0.3)" : undefined,
                  transition: "all 0.3s ease",
                  backgroundColor: isTarget ? "#fff8f8" : "inherit",
                }}
              >
                <CardContent>
                  <Stack
                    direction={{ xs: "column", sm: "row" }}
                    justifyContent="space-between"
                    alignItems={{ xs: "flex-start", sm: "center" }}
                    spacing={1}
                    sx={{ mb: 1.5 }}
                  >
                    <Box>
                      <Stack direction="row" alignItems="center" spacing={1}>
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                          {task.taskName}
                        </Typography>
                        {isTarget && (
                          <Chip label="Đang chọn" color="error" size="small" variant="outlined" />
                        )}
                      </Stack>
                      <Typography variant="body2" color="text.secondary">
                        Hạn chót: <strong>{dateTimeVi(task.deadlineTime)}</strong>
                      </Typography>
                    </Box>

                    <Stack direction="row" spacing={1} alignItems="center">
                      {isOverdue && (
                        <Chip
                          icon={<WarningAmberIcon />}
                          label={`Quá hạn ${task.overdueMinutes} phút`}
                          color="error"
                          sx={{ fontWeight: "bold" }}
                        />
                      )}
                      {isDone ? (
                        <>
                          <Chip
                            icon={<CheckCircleIcon />}
                            label="Đã hoàn thành"
                            color="success"
                            sx={{ fontWeight: "bold" }}
                          />
                          {task.isLate && (
                            <Chip
                              label="Hoàn thành muộn"
                              color="warning"
                              size="small"
                              variant="outlined"
                            />
                          )}
                        </>
                      ) : (
                        !isOverdue && (
                          <Chip
                            icon={<AccessTimeIcon />}
                            label="Chờ thực hiện"
                            color="default"
                          />
                        )
                      )}
                    </Stack>
                  </Stack>

                  <Divider sx={{ my: 1.5 }} />

                  <Stack
                    direction={{ xs: "column", sm: "row" }}
                    justifyContent="space-between"
                    alignItems={{ xs: "flex-start", sm: "center" }}
                    spacing={1}
                  >
                    <Box>
                      <Chip
                        icon={<PhotoCameraIcon fontSize="small" />}
                        label={
                          task.photoRequired
                            ? "Yêu cầu 1–3 ảnh minh chứng (Proof)"
                            : "Không yêu cầu ảnh"
                        }
                        size="small"
                        color={task.photoRequired ? "info" : "default"}
                        variant="outlined"
                      />
                      {isDone && task.completedAt && (
                        <Typography variant="caption" display="block" color="text.secondary" sx={{ mt: 0.5 }}>
                          Hoàn thành lúc: {dateTimeVi(task.completedAt)}
                        </Typography>
                      )}
                    </Box>

                    {!isDone && (
                      <Button
                        variant="contained"
                        color={isOverdue ? "error" : "primary"}
                        startIcon={<CheckCircleIcon />}
                        onClick={() => handleOpenCompleteDialog(task)}
                      >
                        Hoàn thành nhiệm vụ
                      </Button>
                    )}
                  </Stack>

                  {/* Render uploaded proof photos if available */}
                  {task.photos && task.photos.length > 0 && (
                    <Box sx={{ mt: 2 }}>
                      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: "bold", display: "block", mb: 0.5 }}>
                        Ảnh minh chứng đã tải lên ({task.photos.length}):
                      </Typography>
                      <Stack direction="row" spacing={1} flexWrap="wrap">
                        {task.photos.map((photo) => (
                          <Box
                            key={photo.photoId}
                            component="img"
                            src={photo.imageUrl}
                            alt="Minh chứng"
                            sx={{
                              width: 80,
                              height: 80,
                              borderRadius: 1,
                              objectFit: "cover",
                              border: "1px solid",
                              borderColor: "divider",
                            }}
                          />
                        ))}
                      </Stack>
                    </Box>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </Stack>
      </DataState>

      {/* ── M-17: Modal Hoàn thành nhiệm vụ & Upload ảnh minh chứng ──── */}
      <Dialog open={Boolean(activeTask)} onClose={handleCloseDialog} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ m: 0, p: 2, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Typography variant="h6">Hoàn thành nhiệm vụ (M-17)</Typography>
          <IconButton onClick={handleCloseDialog} size="small">
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers>
          {activeTask && (
            <Stack spacing={2}>
              <Box>
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                  {activeTask.taskName}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Hạn chót: {dateTimeVi(activeTask.deadlineTime)}
                </Typography>
                {activeTask.status === "Overdue" && (
                  <Typography variant="body2" color="error.main" sx={{ fontWeight: "bold", mt: 0.5 }}>
                    ⚠️ Nhiệm vụ này đang quá hạn {activeTask.overdueMinutes} phút. Khi hoàn thành, hệ thống sẽ ghi nhận trạng thái hoàn thành muộn.
                  </Typography>
                )}
              </Box>

              {activeTask.photoRequired && (
                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                    Ảnh minh chứng (Yêu cầu 1–3 ảnh, JPG/PNG, tối đa 10 MB mỗi ảnh):
                  </Typography>

                  <Button
                    variant="outlined"
                    component="label"
                    startIcon={<PhotoCameraIcon />}
                    disabled={selectedFiles.length >= 3}
                  >
                    Chọn ảnh minh chứng
                    <input
                      type="file"
                      hidden
                      multiple
                      accept="image/png, image/jpeg, image/jpg"
                      onChange={handleFileChange}
                    />
                  </Button>

                  {selectedFiles.length > 0 && (
                    <Stack direction="row" spacing={1} sx={{ mt: 1.5, flexWrap: "wrap" }}>
                      {selectedFiles.map((file, idx) => (
                        <Chip
                          key={idx}
                          label={`${file.name} (${Math.round(file.size / 1024)} KB)`}
                          onDelete={() => handleRemoveFile(idx)}
                          variant="outlined"
                        />
                      ))}
                    </Stack>
                  )}
                </Box>
              )}

              {submitError && (
                <Alert severity="error">{submitError}</Alert>
              )}
            </Stack>
          )}
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={handleCloseDialog} color="inherit">
            Hủy
          </Button>
          <Button
            onClick={handleSubmitComplete}
            variant="contained"
            color="primary"
            disabled={isSubmitting}
          >
            {isSubmitting ? "Đang xử lý..." : "Xác nhận hoàn thành"}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

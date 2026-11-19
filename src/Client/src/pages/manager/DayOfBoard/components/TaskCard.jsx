import { Box, Button, ButtonBase, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import AddPhotoAlternateIcon from "@mui/icons-material/AddPhotoAlternate";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import StatusChip from "../../../../components/StatusChip";
import { timeVi } from "../../../../utils/format";

// One timeline card: deadline, status (Overdue is set by the job), who finished it, its proof photos.
// A done task that needs proof but has no photo is flagged and can take photos afterwards.
export default function TaskCard({ task, canAddPhotos, maxPhotos, onComplete, onAddPhotos, onOpenPhoto }) {
  const done = task.status === "Done";
  const room = maxPhotos - task.photos.length;
  const border = task.missingProof ? "warning.main" : task.status === "Overdue" ? "error.main" : done ? "success.main" : "grey.400";
  return (
    <Card id={`task-${task.taskId}`} sx={{ borderLeft: 4, borderColor: border }}>
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
          <Box>
            <Typography fontWeight={600}>{timeVi(task.deadline)} · {task.taskName}</Typography>
            <Stack direction="row" sx={{ mt: 0.5, flexWrap: "wrap", gap: 1 }}>
              <StatusChip kind="task" status={task.status} />
              {task.photoRequired && <Chip size="small" icon={<PhotoCameraIcon />} label="Cần ảnh minh chứng" variant="outlined" />}
              {task.isLate && <Chip size="small" color="error" variant="outlined" label={`Hoàn thành muộn lúc ${timeVi(task.completedAt)}`} />}
              {task.completedAt && !task.isLate && <Chip size="small" variant="outlined" label={`Xong lúc ${timeVi(task.completedAt)}`} />}
              {task.completedBy && <Chip size="small" variant="outlined" label={`Người làm: ${task.completedBy}`} />}
              {task.missingProof && <Chip size="small" color="warning" icon={<ReportProblemIcon />} label="Thiếu ảnh minh chứng" />}
            </Stack>
          </Box>
          <Stack direction="row" sx={{ gap: 1, flexShrink: 0 }}>
            {done && canAddPhotos && room > 0 && (
              <Button variant={task.missingProof ? "contained" : "outlined"} color={task.missingProof ? "warning" : "primary"}
                startIcon={<AddPhotoAlternateIcon />} onClick={() => onAddPhotos(task)}>
                {task.missingProof ? "Tải ảnh minh chứng" : "Thêm ảnh"}
              </Button>
            )}
            {!done && <Button variant="contained" color={task.status === "Overdue" ? "error" : "primary"} onClick={() => onComplete(task)}>Hoàn thành</Button>}
          </Stack>
        </Stack>

        {task.photos.length > 0 ? (
          <Stack direction="row" sx={{ mt: 2, flexWrap: "wrap", gap: 1 }}>
            {task.photos.map((p, i) => (
              <ButtonBase key={p.photoId} onClick={() => onOpenPhoto(task, i)} aria-label={`Xem ảnh ${i + 1}`}
                sx={{ borderRadius: 1, overflow: "hidden", border: 1, borderColor: "divider" }}>
                <Box component="img" loading="lazy" src={p.imageUrl} alt={`${task.taskName} – ảnh ${i + 1}`}
                  sx={{ width: { xs: 96, sm: 128 }, height: { xs: 72, sm: 96 }, objectFit: "cover", display: "block" }} />
              </ButtonBase>
            ))}
          </Stack>
        ) : done && (
          <Typography variant="body2" color={task.missingProof ? "warning.main" : "text.secondary"} sx={{ mt: 1.5 }}>
            {task.missingProof
              ? canAddPhotos ? "Công việc đã đánh dấu xong nhưng chưa có ảnh chứng minh kết quả. Hãy tải ảnh lên." : "Công việc đã xong nhưng không có ảnh chứng minh kết quả."
              : "Không có ảnh minh chứng (công việc không bắt buộc ảnh)."}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

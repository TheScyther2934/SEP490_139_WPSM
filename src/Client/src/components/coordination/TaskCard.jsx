import { useState } from "react";
import { Box, Button, ButtonBase, Card, CardContent, Chip, IconButton, Menu, MenuItem, Stack, Typography } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import AddPhotoAlternateIcon from "@mui/icons-material/AddPhotoAlternate";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import StatusChip from "../StatusChip";
import { departmentLabel } from "./departments";
import { dateTimeVi, timeVi } from "../../utils/format";

// One coordination task: deadline, status (Overdue is set by the job), who finished it, its proof (photos open the viewer,
// PDF documents open in a new tab). A done task that needs proof but has none is flagged and can take proof afterwards.
// `showDate`: preparation tasks fall on other days than the event, so the deadline shows the date too.
export default function TaskCard({ task, canAct, maxPhotos, showDate, showDepartment, onComplete, onAddPhotos, onOpenPhoto, onEdit, onDelete, onReopen }) {
  const [menu, setMenu] = useState(null);
  const done = task.status === "Done";
  const room = maxPhotos - task.photos.length;
  const images = task.photos.filter((p) => p.isImage);
  const documents = task.photos.filter((p) => !p.isImage);
  const border = task.missingProof ? "warning.main" : task.status === "Overdue" ? "error.main" : done ? "success.main" : "grey.400";
  const when = showDate ? dateTimeVi(task.deadline) : timeVi(task.deadline);
  const canDelete = task.isCustom && !done && task.photos.length === 0;

  return (
    <Card id={`task-${task.taskId}`} variant="outlined" sx={{ borderLeft: 4, borderLeftColor: border }}>
      <CardContent sx={{ "&:last-child": { pb: 2 } }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
          <Box sx={{ minWidth: 0 }}>
            <Typography fontWeight={600}>{task.taskName}</Typography>
            <Typography variant="body2" color={task.status === "Overdue" ? "error.main" : "text.secondary"}>
              Hạn {when}{showDepartment ? ` · ${departmentLabel(task.department)}` : ""}{task.isCustom ? " · thêm thủ công" : ""}
            </Typography>
            <Stack direction="row" sx={{ mt: 0.75, flexWrap: "wrap", gap: 1 }}>
              <StatusChip kind="task" status={task.status} />
              {task.photoRequired && !done && <Chip size="small" icon={<PhotoCameraIcon />} label="Cần minh chứng" variant="outlined" />}
              {task.isLate && <Chip size="small" color="error" variant="outlined" label={`Xong muộn · ${dateTimeVi(task.completedAt)}`} />}
              {task.completedAt && !task.isLate && <Chip size="small" variant="outlined" label={`Xong · ${dateTimeVi(task.completedAt)}`} />}
              {task.completedBy && <Chip size="small" variant="outlined" label={`Người làm: ${task.completedBy}`} />}
              {task.missingProof && <Chip size="small" color="warning" icon={<ReportProblemIcon />} label="Thiếu minh chứng" />}
            </Stack>
          </Box>
          <Stack direction="row" sx={{ gap: 1, flexShrink: 0, alignItems: "center" }}>
            {canAct && done && room > 0 && (
              <Button variant={task.missingProof ? "contained" : "outlined"} color={task.missingProof ? "warning" : "primary"}
                startIcon={<AddPhotoAlternateIcon />} onClick={() => onAddPhotos(task)}>
                {task.missingProof ? "Tải minh chứng" : "Thêm minh chứng"}
              </Button>
            )}
            {canAct && !done && (
              <Button variant="contained" color={task.status === "Overdue" ? "error" : "primary"} onClick={() => onComplete(task)}>Hoàn thành</Button>
            )}
            {canAct && onEdit && (
              <>
                <IconButton aria-label="Tùy chọn nhiệm vụ" onClick={(e) => setMenu(e.currentTarget)}><MoreVertIcon /></IconButton>
                <Menu anchorEl={menu} open={!!menu} onClose={() => setMenu(null)}>
                  <MenuItem onClick={() => { setMenu(null); onEdit(task); }}>Sửa nhiệm vụ</MenuItem>
                  {done && onReopen && <MenuItem data-action="reopen-task" onClick={() => { setMenu(null); onReopen(task); }}>Hủy hoàn thành (mở lại)</MenuItem>}
                  {canDelete && <MenuItem sx={{ color: "error.main" }} onClick={() => { setMenu(null); onDelete(task); }}>Xóa nhiệm vụ</MenuItem>}
                </Menu>
              </>
            )}
          </Stack>
        </Stack>

        {task.note && <Typography variant="body2" sx={{ mt: 1, whiteSpace: "pre-line" }}>{task.note}</Typography>}

        {task.photos.length > 0 ? (
          <Stack direction="row" sx={{ mt: 1.5, flexWrap: "wrap", gap: 1, alignItems: "center" }}>
            {images.map((p, i) => (
              <ButtonBase key={p.photoId} onClick={() => onOpenPhoto(task, i)} aria-label={`Xem ảnh ${i + 1}`}
                sx={{ borderRadius: 1, overflow: "hidden", border: 1, borderColor: "divider" }}>
                <Box component="img" loading="lazy" src={p.imageUrl} alt={`${task.taskName} – ảnh ${i + 1}`}
                  sx={{ width: { xs: 88, sm: 112 }, height: { xs: 66, sm: 84 }, objectFit: "cover", display: "block" }} />
              </ButtonBase>
            ))}
            {documents.map((p) => (
              <Chip key={p.photoId} icon={<PictureAsPdfIcon />} label={p.fileName || "Tài liệu PDF"} component="a" href={p.imageUrl}
                target="_blank" rel="noopener" clickable variant="outlined" sx={{ maxWidth: 240 }} />
            ))}
          </Stack>
        ) : done && (
          <Typography variant="body2" color={task.missingProof ? "warning.main" : "text.secondary"} sx={{ mt: 1.5 }}>
            {task.missingProof
              ? canAct ? "Đã đánh dấu xong nhưng chưa có ảnh hoặc tài liệu chứng minh. Hãy tải minh chứng lên." : "Đã xong nhưng không có minh chứng."
              : "Không có minh chứng (nhiệm vụ không bắt buộc)."}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

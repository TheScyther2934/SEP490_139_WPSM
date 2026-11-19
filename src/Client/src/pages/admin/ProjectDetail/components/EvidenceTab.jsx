import { Alert, Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import PhotoCard from "../../../../components/PhotoCard";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi, timeVi } from "../../../../utils/format";

// Field evidence: each day-of task with its deadline, who completed it and the proof photos (late uploads flagged).
export default function EvidenceTab({ project }) {
  const tasks = project.evidence;
  const photos = tasks.reduce((n, t) => n + t.photos.length, 0);
  const late = tasks.reduce((n, t) => n + t.photos.filter((p) => p.late).length, 0);
  if (tasks.length === 0) return <Alert severity="info">Sự kiện chưa có nhiệm vụ ngày cưới nên chưa có ảnh minh chứng.</Alert>;
  return (
    <Box sx={{ display: "grid", gap: 2 }}>
      <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
        <Chip label={`${tasks.length} nhiệm vụ`} />
        <Chip label={`${photos} ảnh minh chứng`} color="primary" variant="outlined" />
        {late > 0 && <Chip label={`${late} ảnh tải lên trễ hạn`} color="error" variant="outlined" />}
      </Stack>
      {tasks.map((t) => (
        <Card key={t.taskId} id={`evidence-${t.taskId}`}>
          <CardContent>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1} justifyContent="space-between" alignItems={{ sm: "center" }} sx={{ mb: 1.5 }}>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography fontWeight={600}>{t.taskName}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Hạn {dateTimeVi(t.deadline)}{t.completedAt ? ` · hoàn thành ${dateTimeVi(t.completedAt)}` : ""}
                  {t.completedBy ? ` · người thực hiện: ${t.completedBy}` : ""}
                  {t.photoRequired ? " · bắt buộc ảnh" : ""}
                </Typography>
              </Box>
              <StatusChip kind="task" status={t.status} />
            </Stack>
            {t.photos.length === 0 ? (
              <Typography variant="body2" color={t.photoRequired && t.status === "Done" ? "error" : "text.secondary"}>
                {t.photoRequired && t.status === "Done" ? "Đã hoàn thành nhưng thiếu ảnh minh chứng." : "Chưa có ảnh minh chứng."}
              </Typography>
            ) : (
              <Box sx={{ display: "grid", gap: 1.5, gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))" }}>
                {t.photos.map((p) => (
                  <PhotoCard key={p.photoId} photo={p} height={130} to={PATHS.admin.photo(p.photoId)}
                    title={`${timeVi(p.uploadedAt)}${p.late ? " · TRỄ HẠN" : ""}`} subtitle={`Tải lên bởi ${p.uploadedBy}`} />
                ))}
              </Box>
            )}
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}

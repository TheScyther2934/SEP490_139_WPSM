import { Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import { dateTimeVi, slotLabel } from "../../../../utils/format";

export default function PhotoMeta({ photo }) {
  const rows = [
    ["Dự án", photo.projectCode],
    ["Sảnh – ca", `${photo.hallName} – ${slotLabel(photo.slotName)}`],
    ["Nhiệm vụ", photo.taskName],
    ["Hạn hoàn thành", dateTimeVi(photo.deadline)],
    ["Thời điểm tải lên", dateTimeVi(photo.uploadedAt)],
    ["Người tải lên", photo.uploaderName],
  ];
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2 }}>
          <Typography variant="h6">Thông tin kiểm tra</Typography>
          {photo.isLate ? <Chip label="Trễ hạn" color="error" size="small" /> : <Chip label="Đúng hạn" color="success" size="small" />}
        </Stack>
        <Stack spacing={1.5}>
          {rows.map(([label, value]) => (
            <div key={label}>
              <Typography variant="caption" color="text.secondary">{label}</Typography>
              <Typography data-field={label}>{value}</Typography>
            </div>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}

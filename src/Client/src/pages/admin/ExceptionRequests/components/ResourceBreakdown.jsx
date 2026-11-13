import { Box, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { dateVi, timeVi } from "../../../../utils/format";

// UC-47 step 2 for a resource conflict: the resource lane and the events it clashes with (UC-26).
export default function ResourceBreakdown({ resource }) {
  const Field = ({ label, children, tone }) => (
    <Box>
      <Typography variant="caption" color="text.secondary" component="p">{label}</Typography>
      <Typography variant="body2" fontWeight={600} color={tone}>{children}</Typography>
    </Box>
  );

  return (
    <>
      <Typography variant="subtitle2" gutterBottom>Hồ sơ xung đột tài nguyên (M-22)</Typography>
      <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, mb: 2 }}>
        <Field label="Tài nguyên">{resource.resourceName}</Field>
        <Field label="Loại">{resource.resourceTypeLabel}</Field>
        <Field label="Số lượng yêu cầu">{resource.quantity} / {resource.totalQuantity}</Field>
        <Field label="Thời gian">{timeVi(resource.startTime)} – {timeVi(resource.endTime)} · {dateVi(resource.startTime)}</Field>
        <Field label="Sảnh">{resource.hallName}</Field>
        <Field label="Đang giữ bởi dự án khác">{resource.otherQuantity}</Field>
        <Field label="Thiếu hụt" tone={resource.shortage ? "error.main" : "text.primary"}>{resource.shortage}</Field>
        <Field label="Phân bổ">#{resource.allocationId}</Field>
      </Box>
      {resource.clashing.length === 0 ? (
        <Typography variant="body2" color="text.secondary">Không còn sự kiện chồng lấn — công suất đã đủ.</Typography>
      ) : (
        <Table size="small" id="a18Clashing">
          <TableHead>
            <TableRow>
              <TableCell>Dự án đang giữ</TableCell>
              <TableCell>Khung giờ</TableCell>
              <TableCell align="center">Số lượng</TableCell>
              <TableCell align="center">Trạng thái</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {resource.clashing.map((event) => (
              <TableRow key={`${event.projectCode}-${event.startTime}`}>
                <TableCell>
                  <Typography variant="body2" fontWeight={600}>{event.projectCode}</Typography>
                  <Typography variant="caption" color="text.secondary">{event.coupleName}</Typography>
                </TableCell>
                <TableCell>{timeVi(event.startTime)} – {timeVi(event.endTime)}</TableCell>
                <TableCell align="center">{event.quantity}</TableCell>
                <TableCell align="center"><StatusChip kind="allocation" status={event.status} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
        <Typography variant="caption" color="text.secondary">
          Nếu duyệt, phân bổ chuyển sang “Ngoại lệ đã duyệt” và giữ chỗ vượt công suất; nếu từ chối, phân bổ được thu hồi.
        </Typography>
      </Stack>
    </>
  );
}

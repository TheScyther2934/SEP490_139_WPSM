import { Box, Card, CardContent, Chip, Divider, Stack, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { dateTimeVi, dateVi, timeVi } from "../../../../utils/format";

// M-22 header card: which resource is booked, for which project/hall, and in what state.
export default function AllocationSummary({ allocation }) {
  const Field = ({ label, children }) => (
    <Box>
      <Typography variant="caption" color="text.secondary" component="p">{label}</Typography>
      <Typography variant="body2" fontWeight={600}>{children}</Typography>
    </Box>
  );

  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1, mb: 2 }}>
          <Typography variant="h6">{allocation.resourceName}</Typography>
          <Chip size="small" variant="outlined" label={allocation.resourceTypeLabel} />
          <StatusChip kind="allocation" status={allocation.status} />
          {allocation.isExceptionApproved && <Chip size="small" color="info" label="Ngoại lệ đã duyệt" />}
        </Stack>
        <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" } }}>
          <Field label="Dự án">{allocation.projectCode}</Field>
          <Field label="Khách hàng">{allocation.coupleName}</Field>
          <Field label="Sảnh">{allocation.hallName}</Field>
          <Field label="Ngày diễn ra">{dateVi(allocation.eventDate)}</Field>
          <Field label="Khung giờ">{timeVi(allocation.startTime)} – {timeVi(allocation.endTime)}</Field>
          <Field label="Số lượng phân bổ">{allocation.quantity} / {allocation.totalQuantity} tổng công suất</Field>
          <Field label="Đang giữ bởi dự án khác">{allocation.otherQuantity}</Field>
          <Field label="Thiếu hụt">{allocation.shortage}</Field>
        </Box>
        {allocation.isExceptionApproved && (
          <>
            <Divider sx={{ my: 2 }} />
            <Typography variant="caption" color="text.secondary">
              Admin chấp thuận {allocation.exceptionApprovedAt ? `lúc ${dateTimeVi(allocation.exceptionApprovedAt)}` : ""}
              {allocation.exceptionNote ? ` · Ghi chú: ${allocation.exceptionNote}` : ""}
            </Typography>
          </>
        )}
      </CardContent>
    </Card>
  );
}

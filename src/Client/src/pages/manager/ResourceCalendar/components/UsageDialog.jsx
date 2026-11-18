import { Link as RouterLink } from "react-router-dom";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateVi, timeVi } from "../../../../utils/format";

// Click-through of one resource on one day: what is booked, by whom, and how much is left.
export default function UsageDialog({ open, lane, date, onClose, onAllocate }) {
  if (!lane) return null;
  const usage = lane.usages.find((entry) => entry.date === date);
  const committed = usage ? usage.reservedQuantity + usage.approvedQuantity : 0;
  const pending = usage?.pendingQuantity ?? 0;
  const remaining = Math.max(0, lane.totalQuantity - committed);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>{lane.name} · {dateVi(date)}</DialogTitle>
      <DialogContent>
        <Stack direction="row" spacing={3} sx={{ mb: 2, flexWrap: "wrap", gap: 1 }}>
          <Typography variant="body2" color="text.secondary">Tổng công suất: <strong>{lane.totalQuantity}</strong></Typography>
          <Typography variant="body2" color="text.secondary">Đã giữ: <strong>{committed}</strong></Typography>
          <Typography variant="body2" color="text.secondary">Chờ Admin: <strong>{pending}</strong></Typography>
          <Typography variant="body2" color={remaining === 0 ? "error.main" : "text.secondary"}>Còn lại: <strong>{remaining}</strong></Typography>
        </Stack>
        {!usage || usage.allocations.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 2 }}>Chưa có phân bổ nào cho tài nguyên này trong ngày.</Typography>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Dự án</TableCell>
                <TableCell>Khung giờ</TableCell>
                <TableCell align="center">Số lượng</TableCell>
                <TableCell align="center">Trạng thái</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {usage.allocations.map((allocation) => (
                <TableRow key={allocation.allocationId} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={600} color="primary">{allocation.projectCode}</Typography>
                    <Typography variant="caption" color="text.secondary">{allocation.coupleName} · {allocation.hallName}</Typography>
                  </TableCell>
                  <TableCell>{timeVi(allocation.startTime)} – {timeVi(allocation.endTime)}</TableCell>
                  <TableCell align="center">{allocation.quantity}</TableCell>
                  <TableCell align="center"><StatusChip kind="allocation" status={allocation.status} /></TableCell>
                  <TableCell align="right">
                    <Button size="small" component={RouterLink} to={PATHS.manager.conflict(allocation.allocationId)}>Chi tiết</Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Đóng</Button>
        <Button variant="contained" startIcon={<AddIcon />} onClick={onAllocate}>Phân bổ cho ngày này</Button>
      </DialogActions>
    </Dialog>
  );
}

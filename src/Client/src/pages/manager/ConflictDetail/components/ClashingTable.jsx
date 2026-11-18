import { Link as RouterLink } from "react-router-dom";
import { Button, Card, CardContent, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateVi, timeVi } from "../../../../utils/format";

// UC-26 breakdown: every allocation that overlaps this one on the same resource.
export default function ClashingTable({ clashing }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Sự kiện chồng lấn</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Các dự án đang giữ cùng tài nguyên trong khoảng thời gian giao nhau.
        </Typography>
        {clashing.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 2 }}>Không có phân bổ nào chồng lấn — tài nguyên đang đủ cho khung giờ này.</Typography>
        ) : (
          <Table size="small" id="clashing-table">
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
              {clashing.map((entry) => (
                <TableRow key={entry.allocationId} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={600} color="primary">{entry.projectCode}</Typography>
                    <Typography variant="caption" color="text.secondary">{entry.coupleName} · {entry.hallName}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{timeVi(entry.startTime)} – {timeVi(entry.endTime)}</Typography>
                    <Typography variant="caption" color="text.secondary">{dateVi(entry.startTime)}</Typography>
                  </TableCell>
                  <TableCell align="center">{entry.quantity}</TableCell>
                  <TableCell align="center">
                    <Stack spacing={0.5} sx={{ alignItems: "center" }}>
                      <StatusChip kind="allocation" status={entry.status} />
                      {entry.hasPendingException && <Typography variant="caption" color="warning.main">Chờ Admin</Typography>}
                    </Stack>
                  </TableCell>
                  <TableCell align="right">
                    <Button size="small" component={RouterLink} to={PATHS.manager.conflict(entry.allocationId)}>Xem</Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

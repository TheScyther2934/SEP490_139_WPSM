import { Link as RouterLink } from "react-router-dom";
import { Button, Card, CardContent, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi, dateVi, slotLabel } from "../../../../utils/format";

// Hall/slot cells show project code and status; resource lanes show allocations, red when an exception is involved.
export function BookingTable({ bookings }) {
  return (
    <Card sx={{ mb: 2 }}>
      <CardContent><Typography variant="subtitle1" fontWeight={600}>Sảnh và ca đã đặt</Typography></CardContent>
      <TableContainer>
        <Table size="small" id="bookingTable">
          <TableHead><TableRow><TableCell>Ngày</TableCell><TableCell>Sảnh</TableCell><TableCell>Ca</TableCell><TableCell>Dự án</TableCell><TableCell>Trạng thái</TableCell></TableRow></TableHead>
          <TableBody>
            {bookings.length === 0 ? <TableRow><TableCell colSpan={5} align="center">Các sảnh đều trống trong khoảng này.</TableCell></TableRow> : bookings.map((b) => (
              <TableRow key={b.projectCode}>
                <TableCell>{dateVi(b.eventDate)}</TableCell><TableCell>{b.hall}</TableCell><TableCell>{slotLabel(b.slot)}</TableCell>
                <TableCell>{b.projectCode}{b.mine ? "" : " (Manager khác)"}</TableCell><TableCell><StatusChip kind="project" status={b.status} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}

export function AllocationTable({ resources, allocations, onEdit, onRelease }) {
  return (
    <Card>
      <CardContent><Typography variant="subtitle1" fontWeight={600}>Phân bổ tài nguyên dùng chung</Typography></CardContent>
      <TableContainer>
        <Table size="small" id="allocationTable">
          <TableHead>
            <TableRow><TableCell>Tài nguyên</TableCell><TableCell>Dự án</TableCell><TableCell>Sảnh</TableCell><TableCell>Thời gian</TableCell>
              <TableCell align="right">SL</TableCell><TableCell>Trạng thái</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {allocations.length === 0 ? <TableRow><TableCell colSpan={7} align="center">Chưa có phân bổ trong khoảng này.</TableCell></TableRow> : allocations.map((a) => {
              const total = resources.find((r) => r.resourceId === a.resourceId)?.totalQuantity;
              return (
                <TableRow key={a.allocationId} data-allocation={a.allocationId} sx={a.exceptionStatus ? { bgcolor: "rgba(211,47,47,0.06)" } : undefined}>
                  <TableCell>{a.resourceType} · {a.resourceName}</TableCell>
                  <TableCell>{a.projectCode}</TableCell>
                  <TableCell>{a.hall}</TableCell>
                  <TableCell>{dateTimeVi(a.startTime)} – {dateTimeVi(a.endTime)}</TableCell>
                  <TableCell align="right">{a.quantity}{total ? `/${total}` : ""}</TableCell>
                  <TableCell>
                    <StatusChip kind="allocation" status={a.status} />
                    {a.exceptionStatus && <Chip size="small" color="error" variant="outlined" sx={{ ml: 0.5 }} label={`Ngoại lệ: ${a.exceptionStatus}`} />}
                  </TableCell>
                  <TableCell align="right">
                    {a.mine && a.exceptionStatus && <Button size="small" component={RouterLink} to={PATHS.manager.conflict(a.allocationId)}>Xung đột</Button>}
                    {a.mine && !a.exceptionStatus && <Button size="small" onClick={() => onEdit(a)}>Đổi</Button>}
                    {a.mine && <Button size="small" color="error" onClick={() => onRelease(a)}>Giải phóng</Button>}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}

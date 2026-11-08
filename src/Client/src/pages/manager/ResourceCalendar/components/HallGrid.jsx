import { Box, Card, CardContent, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import EventBusyIcon from "@mui/icons-material/EventBusy";
import StatusChip from "../../../../components/StatusChip";
import { dateVi, slotLabel, weekdayVi } from "../../../../utils/format";

const dayLabel = (day) => `${weekdayVi(day)} ${day.slice(8, 10)}/${day.slice(5, 7)}`;

// Lưới 1: halls × slots × days. A cell is a Wedding Project (GBR-09 keeps one project per hall+slot+date);
// empty cells are free. Clicking a project opens the allocation dialog for it.
export default function HallGrid({ halls, bookings, days, view, onSelectProject }) {
  const bookingAt = (hallId, slotId, day) =>
    bookings.find((b) => b.hallId === hallId && b.slotId === slotId && b.eventDate === day);

  return (
    <Card>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "baseline", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography variant="h6">Lưới 1: Điều phối sảnh &amp; ca tiệc cưới</Typography>
            <Typography variant="body2" color="text.secondary">
              {halls.length} sảnh hoạt động · {halls.reduce((sum, hall) => sum + hall.slots.length, 0)} ca tiệc · bấm vào ô có dự án để phân bổ tài nguyên
            </Typography>
          </Box>
        </Stack>
        <TableContainer sx={{ maxHeight: 620 }}>
          <Table size="small" stickyHeader id="hall-grid">
            <TableHead>
              <TableRow>
                <TableCell sx={{ minWidth: 190 }}>Hạng mục &amp; ca vận hành (GMT+7)</TableCell>
                {days.map((day) => <TableCell key={day} align="center" sx={{ minWidth: view === "day" ? 240 : 108 }}>{dayLabel(day)}</TableCell>)}
              </TableRow>
            </TableHead>
            <TableBody>
              {halls.map((hall) => hall.slots.map((slot) => (
                <TableRow key={`${hall.hallId}-${slot.slotId}`} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={600}>
                      {hall.name} <Typography component="span" variant="caption" color="text.secondary">({hall.minCapacity}–{hall.maxCapacity} khách)</Typography>
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      Ca {slotLabel(slot.slotName)} · {String(slot.startTime).slice(0, 5)} – {String(slot.endTime).slice(0, 5)}
                    </Typography>
                  </TableCell>
                  {days.map((day) => {
                    const booking = bookingAt(hall.hallId, slot.slotId, day);
                    return (
                      <TableCell key={day} align="center" sx={{ p: 0.75 }}>
                        {booking ? (
                          <Box role="button" tabIndex={0} onClick={() => onSelectProject(booking)}
                            onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") onSelectProject(booking); }}
                            sx={{ cursor: "pointer", border: 1, borderColor: "divider", borderRadius: 2, p: 1, bgcolor: "background.paper", "&:hover": { borderColor: "primary.main" } }}>
                            <Typography variant="caption" fontWeight={700} color="primary">{booking.code}</Typography>
                            <Typography variant="body2" noWrap title={booking.coupleName} sx={{ maxWidth: 190, mx: "auto" }}>{booking.coupleName}</Typography>
                            {view === "day" && <Typography variant="caption" color="text.secondary" component="p">Khách hàng · {dateVi(booking.eventDate)}</Typography>}
                            <Box sx={{ mt: 0.5 }}><StatusChip kind="project" status={booking.status} /></Box>
                          </Box>
                        ) : (
                          <Stack alignItems="center" spacing={0.25} sx={{ color: "text.secondary", py: 1 }}>
                            <EventBusyIcon fontSize="small" />
                            <Typography variant="caption">Trống</Typography>
                          </Stack>
                        )}
                      </TableCell>
                    );
                  })}
                </TableRow>
              )))}
            </TableBody>
          </Table>
        </TableContainer>
      </CardContent>
    </Card>
  );
}

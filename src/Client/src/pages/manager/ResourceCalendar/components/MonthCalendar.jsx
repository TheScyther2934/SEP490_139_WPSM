import { Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";

const WEEKDAYS = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật"];

// M-21 month view: one cell per day with the event and conflict counts; click a day to drill into it.
export default function MonthCalendar({ range, days, bookings, lanes, anchor, onSelectDay }) {
  const offset = (new Date(`${range.from}T00:00:00`).getDay() + 6) % 7;
  const cells = [...Array(offset).fill(null), ...days];

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Lưới tháng: mật độ tiệc &amp; xung đột tài nguyên</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Tháng {anchor.slice(5, 7)}/{anchor.slice(0, 4)} · bấm vào một ngày để xem lưới sảnh và tài nguyên
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(7, minmax(0, 1fr))", gap: 1 }}>
          {WEEKDAYS.map((label) => (
            <Typography key={label} variant="caption" color="text.secondary" align="center" fontWeight={600}>{label}</Typography>
          ))}
          {cells.map((day, index) => {
            if (!day) return <Box key={`empty-${index}`} />;
            const events = bookings.filter((booking) => booking.eventDate === day).length;
            const conflicts = lanes.reduce((total, lane) => total + (lane.usages.find((usage) => usage.date === day)?.hasConflict ? 1 : 0), 0);
            const isAnchor = day === anchor;
            return (
              <Box key={day} role="button" tabIndex={0} onClick={() => onSelectDay(day)}
                onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") onSelectDay(day); }}
                sx={{
                  minHeight: 92, border: 1, borderColor: isAnchor ? "primary.main" : "divider", borderRadius: 2, p: 1, cursor: "pointer",
                  bgcolor: events ? "rgba(109, 22, 52, 0.04)" : "transparent", "&:hover": { borderColor: "primary.main" },
                }}>
                <Typography variant="caption" component="div" fontWeight={isAnchor ? 700 : 600} color={isAnchor ? "primary.main" : "text.primary"}>
                  {Number(day.slice(8, 10))}
                </Typography>
                <Typography variant="caption" component="div" color={events ? "text.primary" : "text.secondary"}>
                  {events ? `${events} tiệc` : "Trống"}
                </Typography>
                {conflicts > 0 && (
                  <Stack direction="row" spacing={0.5} sx={{ mt: 0.5 }}>
                    <Chip size="small" color="error" label={`${conflicts} xung đột`} sx={{ height: 20 }} />
                  </Stack>
                )}
              </Box>
            );
          })}
        </Box>
      </CardContent>
    </Card>
  );
}

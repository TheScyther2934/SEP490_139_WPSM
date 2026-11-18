import { Box, Button, Card, CardContent, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { dateVi } from "../../../../utils/format";

const VIEWS = [
  { value: "day", label: "Ngày" },
  { value: "week", label: "Tuần" },
  { value: "month", label: "Tháng" },
];

// M-21 toolbar: view, period navigation, hall/type filters and the conflict-only switch.
export default function CalendarToolbar({ view, range, halls, types, filters, conflictCount, onChange, onStep, onToday }) {
  return (
    <Card sx={{ mb: 2 }}>
      <CardContent sx={{ pb: "16px !important" }}>
        <Stack direction={{ xs: "column", lg: "row" }} spacing={2} sx={{ alignItems: { lg: "center" } }}>
          <ToggleButtonGroup size="small" exclusive value={view} onChange={(_, next) => next && onChange({ view: next, date: "" })} aria-label="Chế độ xem lịch">
            {VIEWS.map((option) => <ToggleButton key={option.value} value={option.value}>{option.label}</ToggleButton>)}
          </ToggleButtonGroup>

          <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
            <IconButton size="small" aria-label="Kỳ trước" onClick={() => onStep(-1)}><ChevronLeftIcon /></IconButton>
            <Button size="small" onClick={onToday}>Hôm nay</Button>
            <IconButton size="small" aria-label="Kỳ sau" onClick={() => onStep(1)}><ChevronRightIcon /></IconButton>
            <Typography fontWeight={600} sx={{ ml: 1, whiteSpace: "nowrap" }}>
              {view === "day" ? dateVi(range.from) : `${dateVi(range.from)} – ${dateVi(range.to)}`}
            </Typography>
          </Stack>

          <Box sx={{ flex: 1 }} />

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "center" } }}>
            <TextField select size="small" label="Sảnh" value={filters.hallId} sx={{ minWidth: 180 }}
              onChange={(event) => onChange({ hall: event.target.value })}>
              <MenuItem value="">Tất cả sảnh tiệc ({halls.length})</MenuItem>
              {halls.map((hall) => <MenuItem key={hall.hallId} value={String(hall.hallId)}>{hall.name}</MenuItem>)}
            </TextField>
            <TextField select size="small" label="Tài nguyên" value={filters.type} sx={{ minWidth: 210 }}
              onChange={(event) => onChange({ type: event.target.value })}>
              <MenuItem value="">Tất cả loại tài nguyên &amp; nhân sự</MenuItem>
              {types.map((type) => <MenuItem key={type.type} value={type.type}>{type.label} ({type.totalQuantity})</MenuItem>)}
            </TextField>
            <FormControlLabel control={<Switch size="small" checked={filters.onlyConflicts} onChange={(event) => onChange({ conflicts: event.target.checked ? "1" : "" })} />}
              label={conflictCount ? `Chỉ hiện xung đột (${conflictCount})` : "Chỉ hiện xung đột"} />
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}

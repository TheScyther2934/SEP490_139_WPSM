import { MenuItem, Stack, TextField } from "@mui/material";

export const RESOURCE_TYPES = ["Audio", "Follow Light", "Decor Frame", "Technician", "Kitchen"];

// Date range, hall and resource type kept on the query string (F5 or a shared link keeps the view).
export default function CalendarFilters({ filters, halls, onChange }) {
  return (
    <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField type="date" label="Từ ngày" size="small" value={filters.from} onChange={(e) => onChange({ from: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
      <TextField type="date" label="Đến ngày" size="small" value={filters.to} onChange={(e) => onChange({ to: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
      <TextField select label="Sảnh" size="small" value={filters.hallId} sx={{ minWidth: 180 }} onChange={(e) => onChange({ hallId: e.target.value })}>
        <MenuItem value="">Tất cả sảnh</MenuItem>
        {halls.map((h) => <MenuItem key={h.hallId} value={String(h.hallId)}>{h.name}</MenuItem>)}
      </TextField>
      <TextField select label="Loại tài nguyên" size="small" value={filters.type} sx={{ minWidth: 180 }} onChange={(e) => onChange({ type: e.target.value })}>
        <MenuItem value="">Tất cả</MenuItem>
        {RESOURCE_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
      </TextField>
    </Stack>
  );
}

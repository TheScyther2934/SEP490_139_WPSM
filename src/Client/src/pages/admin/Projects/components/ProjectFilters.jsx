import { useEffect, useRef, useState } from "react";
import { Button, Card, CardContent, Chip, InputAdornment, MenuItem, Stack, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";
import { statusLabel } from "../../../../components/StatusChip";
import { isoDate } from "../../../../utils/format";

const SORTS = [["", "Ngày cưới mới nhất"], ["eventDate", "Ngày cưới sớm nhất"], ["createdAt", "Mới tạo gần đây"], ["code", "Mã sự kiện"]];

const shift = (days) => { const d = new Date(); d.setDate(d.getDate() + days); return isoDate(d); };
// Quick ranges on the wedding date: the most common questions an Admin asks.
const QUICK = [
  { label: "30 ngày tới", from: () => shift(0), to: () => shift(30) },
  { label: "Tháng này", from: () => { const d = new Date(); return isoDate(new Date(d.getFullYear(), d.getMonth(), 1)); },
    to: () => { const d = new Date(); return isoDate(new Date(d.getFullYear(), d.getMonth() + 1, 0)); } },
  { label: "Đã diễn ra", from: () => "", to: () => shift(-1) },
];

// The search box keeps its own text and reaches the URL 350 ms after typing stops (one request per pause).
export default function ProjectFilters({ filters, options, invalidRange, onChange, onReset }) {
  const [query, setQuery] = useState(filters.search);
  const timer = useRef(null);
  useEffect(() => setQuery(filters.search), [filters.search]);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  const search = (text) => {
    setQuery(text);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => onChange("search", text.trim()), 350);
  };
  const select = (key, label, allLabel, items) => (
    <TextField select size="small" label={label} value={filters[key]} onChange={(e) => onChange(key, e.target.value)} sx={{ minWidth: 170 }}
      slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}>
      {allLabel && <MenuItem value="">{allLabel}</MenuItem>}
      {items.map(([value, text]) => <MenuItem key={value} value={String(value)}>{text}</MenuItem>)}
    </TextField>
  );
  const date = (key, label) => (
    <TextField type="date" size="small" label={label} value={filters[key]} onChange={(e) => onChange(key, e.target.value)}
      error={invalidRange} helperText={invalidRange && key === "to" ? "Phải sau hoặc bằng ngày bắt đầu" : ""}
      slotProps={{ inputLabel: { shrink: true } }} sx={{ minWidth: 160 }} />
  );
  const hasFilters = Object.values(filters).some(Boolean);

  return (
    <Card sx={{ mb: 2 }}>
      <CardContent>
        <Stack spacing={2}>
          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <TextField id="adminProjectSearch" size="small" label="Tìm mã sự kiện, cô dâu chú rể, email, SĐT, Manager" value={query}
              onChange={(e) => search(e.target.value)} sx={{ flex: 1 }}
              slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
            {select("sort", "Sắp xếp", null, SORTS)}
          </Stack>
          <Stack direction={{ xs: "column", md: "row" }} spacing={2} useFlexGap flexWrap="wrap">
            {select("status", "Trạng thái", "Tất cả trạng thái", (options?.statuses ?? []).map((s) => [s, statusLabel("project", s)]))}
            {select("hallId", "Sảnh", "Tất cả sảnh", (options?.halls ?? []).map((h) => [h.id, h.name]))}
            {select("managerId", "Manager điều hành", "Tất cả Manager", (options?.managers ?? []).map((m) => [m.id, m.name]))}
            {date("from", "Ngày cưới từ")}
            {date("to", "Đến ngày")}
          </Stack>
          <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" alignItems="center">
            {QUICK.map((q) => (
              <Chip key={q.label} label={q.label} variant="outlined" onClick={() => onChange({ from: q.from(), to: q.to() })} />
            ))}
            <Button size="small" startIcon={<FilterAltOffIcon />} disabled={!hasFilters} onClick={() => { setQuery(""); onReset(); }} sx={{ ml: { md: "auto" } }}>
              Xóa bộ lọc
            </Button>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}

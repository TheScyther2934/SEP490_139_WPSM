import { useState } from "react";
import { Button, Card, CardContent, InputAdornment, MenuItem, Stack, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { statusLabel } from "../../../../components/StatusChip";

const STATUSES = ["Active Baseline", "Baseline Frozen", "Event Completed", "Settled", "Closed"];

// The search box keeps its own text: the URL updates inside a router transition, and binding the input
// to it directly drops keystrokes when typing fast.
export default function ProjectFilters({ filters, halls, months, onChange, onReset }) {
  const [query, setQuery] = useState(filters.q);
  const search = (text) => { setQuery(text); onChange("q", text); };
  const reset = () => { setQuery(""); onReset(); };
  const select = (key, label, allLabel, options) => (
    <TextField select size="small" label={label} value={filters[key]} onChange={(e) => onChange(key, e.target.value)} sx={{ minWidth: 170 }}>
      <MenuItem value="">{allLabel}</MenuItem>
      {options.map(([value, text]) => <MenuItem key={value} value={value}>{text}</MenuItem>)}
    </TextField>
  );
  return (
    <Card sx={{ mb: 2 }}>
      <CardContent>
        <Stack direction={{ xs: "column", lg: "row" }} spacing={2}>
          <TextField id="search-input" size="small" label="Tìm mã dự án, tên khách, email" value={query} onChange={(e) => search(e.target.value)} sx={{ flex: 1 }}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
          {select("status", "Trạng thái", "Tất cả trạng thái", STATUSES.map((s) => [s, statusLabel("project", s)]))}
          {select("hall", "Sảnh", "Tất cả sảnh", halls.map((h) => [h, h]))}
          {select("month", "Tháng", "Tất cả các tháng", months.map((m) => [m, `${m.slice(5)}/${m.slice(0, 4)}`]))}
          <Button onClick={reset}>Đặt lại</Button>
        </Stack>
      </CardContent>
    </Card>
  );
}

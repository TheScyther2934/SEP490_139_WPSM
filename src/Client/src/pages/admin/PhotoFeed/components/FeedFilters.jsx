import { Card, CardContent, MenuItem, Stack, TextField } from "@mui/material";

const STATUSES = [["", "Tất cả trạng thái"], ["Pending", "Chờ thực hiện"], ["Done", "Hoàn thành"], ["Overdue", "Quá hạn"]];

// Filters sit in one row above the grid; values live on the query string (date, projectCode, hall, status).
export default function FeedFilters({ filters, projects, halls, onChange }) {
  const select = (key, label, options) => (
    <TextField select size="small" label={label} value={filters[key]} onChange={(e) => onChange(key, e.target.value)} sx={{ minWidth: 180 }}>
      {options.map(([value, text]) => <MenuItem key={value} value={value}>{text}</MenuItem>)}
    </TextField>
  );
  return (
    <Card sx={{ mb: 3 }}>
      <CardContent>
        <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
          <TextField id="feedDate" type="date" size="small" label="Ngày" value={filters.date}
            onChange={(e) => onChange("date", e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          {select("projectCode", "Dự án", [["", "Tất cả dự án"], ...projects.map((p) => [p, p])])}
          {select("hall", "Sảnh", [["", "Tất cả sảnh"], ...halls.map((h) => [h, h])])}
          {select("status", "Trạng thái nhiệm vụ", STATUSES)}
        </Stack>
      </CardContent>
    </Card>
  );
}

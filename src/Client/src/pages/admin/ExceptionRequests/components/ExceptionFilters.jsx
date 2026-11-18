import { useState } from "react";
import { Button, Card, InputAdornment, MenuItem, Stack, Tab, Tabs, TextField, Typography } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { SOURCE_CR, SOURCE_RESOURCE, sourceLabel } from "../exceptionMeta";

const TABS = [
  { value: "Pending", label: "Chờ duyệt" },
  { value: "Approved", label: "Đã duyệt" },
  { value: "Rejected", label: "Đã từ chối" },
];

// A-18 filter bar: queue status, request source and free-text search (code, project, manager, resource).
export default function ExceptionFilters({ filters, summary, total, onChange, onReset }) {
  const [query, setQuery] = useState(filters.q);
  const search = (text) => { setQuery(text); onChange({ q: text }); };
  const count = (value) => ({ Pending: summary.pending, Approved: summary.approved, Rejected: summary.rejected }[value] ?? 0);

  return (
    <Card sx={{ mb: 2 }}>
      <Tabs value={filters.status} onChange={(_, value) => onChange({ status: value })} variant="scrollable" scrollButtons="auto"
        aria-label="Lọc yêu cầu ngoại lệ theo trạng thái" sx={{ px: 1, borderBottom: 1, borderColor: "divider" }}>
        {TABS.map((tab) => <Tab key={tab.value} value={tab.value} label={`${tab.label} (${count(tab.value)})`} />)}
      </Tabs>
      <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ p: 2, alignItems: { md: "center" } }}>
        <TextField id="a18Search" size="small" label="Tìm mã yêu cầu, mã dự án, quản lý, tài nguyên" value={query} sx={{ flex: 1 }}
          onChange={(event) => search(event.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
        <TextField select size="small" label="Phân loại" value={filters.type} sx={{ minWidth: 220 }}
          onChange={(event) => onChange({ type: event.target.value })}>
          <MenuItem value="">Tất cả loại ngoại lệ</MenuItem>
          <MenuItem value={SOURCE_CR}>{sourceLabel(SOURCE_CR)}</MenuItem>
          <MenuItem value={SOURCE_RESOURCE}>{sourceLabel(SOURCE_RESOURCE)}</MenuItem>
        </TextField>
        <Button onClick={() => { setQuery(""); onReset(); }}>Xóa bộ lọc</Button>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ px: 2, pb: 1.5, display: "block" }}>
        Hiển thị {total} yêu cầu thuộc nhóm này
      </Typography>
    </Card>
  );
}

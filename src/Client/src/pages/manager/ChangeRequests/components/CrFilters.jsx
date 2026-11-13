import { useState } from "react";
import { Button, Card, InputAdornment, MenuItem, Stack, Tab, Tabs, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { CR_STATUS_LABELS, CR_TYPES } from "../../../../components/changeRequest/crMeta";

const TABS = [
  { value: "", label: "Tất cả" },
  { value: "Draft", label: CR_STATUS_LABELS.Draft },
  { value: "Negotiating", label: CR_STATUS_LABELS.Negotiating },
  { value: "Approved", label: CR_STATUS_LABELS.Approved },
  { value: "Rejected", label: CR_STATUS_LABELS.Rejected },
];

// M-07 filters: pipeline status, ticket origin (Customer vs Manager) and change type, all on the URL.
export default function CrFilters({ filters, counts, onChange, onReset }) {
  const [query, setQuery] = useState(filters.q);
  const search = (text) => { setQuery(text); onChange({ q: text, page: "" }); };
  const reset = () => { setQuery(""); onReset(); };
  const count = (value) => (value ? counts[value] ?? 0 : counts.total);

  return (
    <Card sx={{ mb: 2 }}>
      <Tabs value={filters.status} onChange={(_, value) => onChange({ status: value, page: "" })} variant="scrollable" scrollButtons="auto"
        aria-label="Lọc yêu cầu thay đổi theo trạng thái" sx={{ px: 1, borderBottom: 1, borderColor: "divider" }}>
        {TABS.map((tab) => (
          <Tab key={tab.value || "all"} value={tab.value} label={`${tab.label} (${count(tab.value)})`} />
        ))}
      </Tabs>
      <Stack direction={{ xs: "column", lg: "row" }} spacing={2} sx={{ p: 2 }}>
        <TextField id="cr-search" size="small" label="Tìm mã CR, nội dung hoặc hạng mục" value={query} sx={{ flex: 1 }}
          onChange={(e) => search(e.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
        <TextField select size="small" label="Người tạo" value={filters.creator} sx={{ minWidth: 170 }}
          onChange={(e) => onChange({ creator: e.target.value })}>
          <MenuItem value="">Tất cả nguồn tạo</MenuItem>
          <MenuItem value="Customer">Khách hàng tạo</MenuItem>
          <MenuItem value="Manager">Quản lý tạo</MenuItem>
        </TextField>
        <TextField select size="small" label="Loại thay đổi" value={filters.type} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ type: e.target.value })}>
          <MenuItem value="">Tất cả phân loại</MenuItem>
          {CR_TYPES.map((type) => <MenuItem key={type.value} value={type.value}>{type.label}</MenuItem>)}
        </TextField>
        <Button onClick={reset}>Xóa bộ lọc</Button>
      </Stack>
    </Card>
  );
}

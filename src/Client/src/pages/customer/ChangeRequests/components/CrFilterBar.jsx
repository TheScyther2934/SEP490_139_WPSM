import { useState } from "react";
import { Button, Card, InputAdornment, MenuItem, Stack, Tab, Tabs, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { CR_STATUS_LABELS } from "../../../../components/changeRequest/crMeta";

const TABS = [
  { value: "", label: "Tất cả" },
  { value: "Draft", label: CR_STATUS_LABELS.Draft },
  { value: "Negotiating", label: CR_STATUS_LABELS.Negotiating },
  { value: "Approved", label: CR_STATUS_LABELS.Approved },
  { value: "Rejected", label: CR_STATUS_LABELS.Rejected },
];

const SORTS = [
  { value: "newest", label: "Mới nhất trước" },
  { value: "oldest", label: "Cũ nhất trước" },
  { value: "impact", label: "Chênh lệch cao nhất" },
];

// C-05 status filter, search and sort. Filter state lives in the URL so a refresh keeps the view.
// The search box keeps its own text: the URL updates inside a router transition (see ProjectFilters).
export default function CrFilterBar({ filters, counts, onChange, onReset }) {
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
      <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ p: 2 }}>
        <TextField id="cr-search" size="small" label="Tìm theo mã CR, nội dung, hạng mục" value={query} sx={{ flex: 1 }}
          onChange={(e) => search(e.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
        <TextField select size="small" label="Sắp xếp" value={filters.sort} sx={{ minWidth: 200 }}
          onChange={(e) => onChange({ sort: e.target.value })}>
          {SORTS.map((option) => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
        </TextField>
        <Button onClick={reset}>Xóa bộ lọc</Button>
      </Stack>
    </Card>
  );
}

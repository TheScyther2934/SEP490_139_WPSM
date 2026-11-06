import { useEffect, useState } from "react";
import { Box, Button, InputAdornment, MenuItem, Stack, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import AddIcon from "@mui/icons-material/Add";
import { CATEGORIES } from "../../../../utils/catalog";

// Toolbar of A-02: search, category, status and the Add button (mockup). The search box keeps its own state and
// pushes to the URL after a pause, so fast typing does not lose characters (L-18).
export default function CatalogFilters({ filters, onChange, onAdd }) {
  const [text, setText] = useState(filters.query);
  useEffect(() => {
    const value = text.trim() ? text : "";
    if (value === filters.query) return undefined;
    const timer = setTimeout(() => onChange("q", value), 300);
    return () => clearTimeout(timer);
  }, [text, filters.query, onChange]);

  return (
    <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ p: 2, borderBottom: 1, borderColor: "divider", alignItems: { md: "center" } }}>
      <TextField size="small" placeholder="Tìm theo tên dịch vụ" value={text} onChange={(e) => setText(e.target.value)} sx={{ width: { md: 300 } }}
        slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }, htmlInput: { "aria-label": "Tìm dịch vụ" } }} />
      <TextField id="catalogCategoryFilter" select size="small" label="Danh mục" value={filters.category} onChange={(e) => onChange("category", e.target.value)} sx={{ minWidth: 200 }}>
        <MenuItem value="">Tất cả danh mục</MenuItem>
        {CATEGORIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
      </TextField>
      <TextField id="catalogStatusFilter" select size="small" label="Trạng thái" value={filters.status} onChange={(e) => onChange("status", e.target.value)} sx={{ minWidth: 160 }}>
        <MenuItem value="">Tất cả</MenuItem>
        <MenuItem value="active">Đang dùng</MenuItem>
        <MenuItem value="inactive">Ngừng dùng</MenuItem>
      </TextField>
      <Box sx={{ flex: 1 }} />
      <Button variant="contained" startIcon={<AddIcon />} onClick={onAdd} sx={{ px: 3, py: 1 }}>Thêm dịch vụ</Button>
    </Stack>
  );
}

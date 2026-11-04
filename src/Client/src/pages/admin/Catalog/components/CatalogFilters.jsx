import { Card, CardContent, InputAdornment, MenuItem, Stack, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { CATEGORIES } from "../../../../utils/catalog";

export default function CatalogFilters({ filters, onChange }) {
  return (
    <Card sx={{ mb: 2 }}>
      <CardContent>
        <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
          <TextField size="small" label="Tìm dịch vụ" value={filters.query} onChange={(e) => onChange("query", e.target.value)} sx={{ flex: 1 }}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
          <TextField select size="small" label="Danh mục" value={filters.category} onChange={(e) => onChange("category", e.target.value)} sx={{ minWidth: 200 }}>
            <MenuItem value="">Tất cả danh mục</MenuItem>
            {CATEGORIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </TextField>
          <TextField select size="small" label="Trạng thái" value={filters.status} onChange={(e) => onChange("status", e.target.value)} sx={{ minWidth: 160 }}>
            <MenuItem value="">Tất cả</MenuItem>
            <MenuItem value="active">Đang dùng</MenuItem>
            <MenuItem value="inactive">Ngừng dùng</MenuItem>
          </TextField>
        </Stack>
      </CardContent>
    </Card>
  );
}

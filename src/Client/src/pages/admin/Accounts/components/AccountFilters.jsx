import { useEffect, useState } from "react";
import { Box, Button, InputAdornment, MenuItem, Stack, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import PersonAddIcon from "@mui/icons-material/PersonAdd";
import { roleLabel } from "../../../../utils/format";

export const ROLE_OPTIONS = ["Admin", "Manager", "Customer"];

// Toolbar of A-19. The search box keeps its own state and pushes to the URL after a pause (L-18).
export default function AccountFilters({ filters, onChange, onAdd }) {
  const [text, setText] = useState(filters.query);
  useEffect(() => {
    const value = text.trim() ? text : "";
    if (value === filters.query) return undefined;
    const timer = setTimeout(() => onChange("q", value), 300);
    return () => clearTimeout(timer);
  }, [text, filters.query, onChange]);

  return (
    <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ p: 2, borderBottom: 1, borderColor: "divider", alignItems: { md: "center" } }}>
      <TextField size="small" placeholder="Tìm theo tên, email, số điện thoại" value={text} onChange={(e) => setText(e.target.value)} sx={{ width: { md: 320 } }}
        slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }, htmlInput: { "aria-label": "Tìm tài khoản" } }} />
      <TextField id="accountRoleFilter" select size="small" label="Vai trò" value={filters.role} onChange={(e) => onChange("role", e.target.value)} sx={{ minWidth: 190 }}>
        <MenuItem value="">Tất cả vai trò</MenuItem>
        {ROLE_OPTIONS.map((r) => <MenuItem key={r} value={r}>{roleLabel(r)}</MenuItem>)}
      </TextField>
      <TextField id="accountStatusFilter" select size="small" label="Trạng thái" value={filters.status} onChange={(e) => onChange("status", e.target.value)} sx={{ minWidth: 160 }}>
        <MenuItem value="">Tất cả</MenuItem>
        <MenuItem value="active">Đang hoạt động</MenuItem>
        <MenuItem value="inactive">Đã khóa</MenuItem>
      </TextField>
      <Box sx={{ flex: 1 }} />
      <Button id="addAccount" variant="contained" startIcon={<PersonAddIcon />} onClick={onAdd} sx={{ px: 3, py: 1 }}>Thêm tài khoản</Button>
    </Stack>
  );
}

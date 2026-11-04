import { useState } from "react";
import {
  Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography,
} from "@mui/material";
import { frameFor } from "../rules";
import { CATEGORIES } from "../../../../utils/catalog";
import { money } from "../../../../utils/format";

// Only active services not already in the specification (one line per service).
export default function CatalogPickerDialog({ open, catalog, priceFrames, selectedIds, onAdd, onClose }) {
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("");
  const q = query.trim().toLowerCase();
  const choices = catalog.filter((c) => c.active && !selectedIds.has(Number(c.id))
    && (!category || c.category === category) && (!q || c.name.toLowerCase().includes(q)));

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>Chọn dịch vụ từ Catalog</DialogTitle>
      <DialogContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ my: 1 }}>
          <TextField id="catalogSearch" size="small" label="Tìm dịch vụ" value={query} onChange={(e) => setQuery(e.target.value)} sx={{ flex: 1 }} />
          <TextField id="catalogCategoryFilter" select size="small" label="Danh mục" value={category} onChange={(e) => setCategory(e.target.value)} sx={{ minWidth: 200 }}>
            <MenuItem value="">Tất cả danh mục</MenuItem>
            {CATEGORIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </TextField>
        </Stack>
        {choices.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 3, textAlign: "center" }}>Không còn dịch vụ đang hoạt động để thêm.</Typography>
        ) : (
          <Stack spacing={1}>
            {choices.map((item) => {
              const frame = frameFor(priceFrames, item.id);
              return (
                <Card key={item.id}>
                  <CardContent sx={{ display: "flex", gap: 2, alignItems: "center", justifyContent: "space-between", py: "12px !important" }}>
                    <div>
                      <Typography fontWeight={600}>{item.name}</Typography>
                      <Typography variant="body2" color="text.secondary">{item.category} · {item.unit} · Giá mặc định {money(item.basePrice)}</Typography>
                      <Typography variant="caption" color={frame ? "success.main" : "error"}>
                        Khung giá: {frame ? `${money(frame.minPrice)} – ${money(frame.maxPrice)}` : "chưa có"}
                      </Typography>
                    </div>
                    <Button variant="outlined" onClick={() => onAdd(item)} data-add-spec-item={item.id}>Thêm</Button>
                  </CardContent>
                </Card>
              );
            })}
          </Stack>
        )}
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Đóng</Button></DialogActions>
    </Dialog>
  );
}

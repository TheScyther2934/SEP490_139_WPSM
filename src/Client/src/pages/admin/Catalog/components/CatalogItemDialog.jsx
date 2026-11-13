import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, FormControlLabel, FormLabel, IconButton, InputAdornment,
  MenuItem, Radio, RadioGroup, TextField, Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { CATEGORIES } from "../../../../utils/catalog";
import { moneyInput } from "../../../../utils/format";

const EMPTY = { name: "", category: "", unit: "", basePrice: "", active: true };

// A-03 (add or edit). SRS 10.2: name ≤100 and unique per category (checked by the API), unit ≤20, price > 0.
export function validateCatalogItem(item) {
  const errors = {};
  if (!item.name.trim()) errors.name = "Trường Tên dịch vụ là bắt buộc.";
  else if (item.name.trim().length > 100) errors.name = "Tên dịch vụ tối đa 100 ký tự.";
  if (!item.category) errors.category = "Trường Danh mục là bắt buộc.";
  if (!item.unit.trim()) errors.unit = "Trường Đơn vị tính là bắt buộc.";
  else if (item.unit.trim().length > 20) errors.unit = "Đơn vị tính tối đa 20 ký tự.";
  if (moneyInput(item.basePrice) <= 0) errors.basePrice = "Giá mặc định phải lớn hơn 0.";
  return errors;
}

export default function CatalogItemDialog({ open, initial, onClose, onSave }) {
  const [item, setItem] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (!open) return;
    setItem(initial ? { name: initial.name, category: initial.category, unit: initial.unit, basePrice: String(initial.basePrice), active: initial.active } : EMPTY);
    setErrors({});
    setServerError("");
  }, [open, initial]);

  const set = (key) => (e) => setItem((v) => ({ ...v, [key]: key === "active" ? e.target.value === "active" : e.target.value }));
  const save = async () => {
    const next = validateCatalogItem(item);
    setErrors(next);
    if (Object.keys(next).length) return;
    setSaving(true);
    const message = await onSave({ name: item.name.trim(), category: item.category, unit: item.unit.trim(), basePrice: moneyInput(item.basePrice), active: item.active });
    setSaving(false);
    setServerError(message ?? "");
  };
  const price = moneyInput(item.basePrice);

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle sx={{ pr: 6 }}>
        {initial ? `Sửa dịch vụ #${initial.id}` : "Thêm dịch vụ"}
        <IconButton aria-label="Đóng" onClick={onClose} disabled={saving} sx={{ position: "absolute", right: 8, top: 8 }}><CloseIcon /></IconButton>
      </DialogTitle>
      <DialogContent>
        {serverError && <Alert severity="error" sx={{ mb: 2 }}>{serverError}</Alert>}
        <Box sx={{ display: "grid", gap: 2.5, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, pt: 1 }}>
          <TextField id="catalogName" label="Tên dịch vụ" required value={item.name} onChange={set("name")} error={!!errors.name}
            helperText={errors.name ?? `${item.name.trim().length}/100`} sx={{ gridColumn: "1 / -1" }} />
          <TextField id="catalogCategory" select label="Danh mục" required value={item.category} onChange={set("category")} error={!!errors.category} helperText={errors.category ?? " "}>
            {CATEGORIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </TextField>
          <TextField id="catalogUnit" label="Đơn vị tính (tối đa 20 ký tự)" required placeholder="bàn, bộ, giờ…" value={item.unit} onChange={set("unit")}
            error={!!errors.unit} helperText={errors.unit ?? " "} />
          <TextField id="catalogPrice" label="Giá mặc định" required value={price ? price.toLocaleString("vi-VN") : item.basePrice} onChange={set("basePrice")}
            error={!!errors.basePrice} helperText={errors.basePrice ?? " "}
            slotProps={{ input: { endAdornment: <InputAdornment position="end">VND</InputAdornment> }, htmlInput: { inputMode: "numeric" } }} />
          <FormControl>
            <FormLabel id="catalogStatusLabel" sx={{ fontSize: 13 }}>Trạng thái</FormLabel>
            <RadioGroup row aria-labelledby="catalogStatusLabel" value={item.active ? "active" : "inactive"} onChange={set("active")}>
              <FormControlLabel value="active" control={<Radio size="small" />} label="Đang dùng" />
              <FormControlLabel value="inactive" control={<Radio size="small" />} label="Ngừng dùng" />
            </RadioGroup>
          </FormControl>
        </Box>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          Tên trùng trong cùng danh mục sẽ bị từ chối. Dịch vụ ngừng dùng vẫn được giữ trong các đặc tả đã lập.
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button variant="outlined" color="inherit" onClick={onClose} disabled={saving} sx={{ minWidth: 100 }}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={saving} sx={{ minWidth: 100 }}>{saving ? "Đang lưu…" : "Lưu"}</Button>
      </DialogActions>
    </Dialog>
  );
}

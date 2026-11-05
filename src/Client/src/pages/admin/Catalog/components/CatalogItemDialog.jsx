import { useEffect, useState } from "react";
import {
  Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, Switch, TextField,
} from "@mui/material";
import { CATEGORIES } from "../../../../utils/catalog";
import { moneyInput } from "../../../../utils/format";

const EMPTY = { name: "", category: "", unit: "", basePrice: "", active: true };

// A-03 (add). SRS 10.2: name ≤100 and unique per category (checked by the API), unit ≤20, price > 0.
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

export default function CatalogItemDialog({ open, onClose, onSave }) {
  const [item, setItem] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (open) { setItem(EMPTY); setErrors({}); setServerError(""); } }, [open]);

  const set = (key) => (e) => setItem((v) => ({ ...v, [key]: key === "active" ? e.target.checked : e.target.value }));
  const save = async () => {
    const next = validateCatalogItem(item);
    setErrors(next);
    if (Object.keys(next).length) return;
    setSaving(true);
    const message = await onSave({ name: item.name.trim(), category: item.category, unit: item.unit.trim(), basePrice: moneyInput(item.basePrice), active: item.active });
    setSaving(false);
    setServerError(message ?? "");
  };

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Thêm dịch vụ</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {serverError && <Alert severity="error">{serverError}</Alert>}
          <TextField id="catalogName" label="Tên dịch vụ" value={item.name} onChange={set("name")} error={!!errors.name} helperText={errors.name} />
          <TextField id="catalogCategory" select label="Danh mục" value={item.category} onChange={set("category")} error={!!errors.category} helperText={errors.category}>
            {CATEGORIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </TextField>
          <TextField id="catalogUnit" label="Đơn vị tính" placeholder="bàn, bộ, giờ…" value={item.unit} onChange={set("unit")} error={!!errors.unit} helperText={errors.unit} />
          <TextField id="catalogPrice" label="Giá mặc định (VND)" inputMode="numeric" value={item.basePrice} onChange={set("basePrice")} error={!!errors.basePrice} helperText={errors.basePrice} />
          <FormControlLabel control={<Switch checked={item.active} onChange={set("active")} />} label="Đang hoạt động" />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={saving}>{saving ? "Đang lưu…" : "Lưu"}</Button>
      </DialogActions>
    </Dialog>
  );
}

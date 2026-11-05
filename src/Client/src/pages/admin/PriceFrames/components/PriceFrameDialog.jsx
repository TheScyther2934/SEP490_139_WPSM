import { useEffect, useState } from "react";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from "@mui/material";
import { isoDate, moneyInput } from "../../../../utils/format";

// A-05. SRS 10.3: min ≥ 0, min ≤ max (MSG24), effective from today or later.
export function validatePriceFrame({ minPrice, maxPrice, effectiveFrom }) {
  const errors = {};
  const min = moneyInput(minPrice);
  const max = moneyInput(maxPrice);
  if (minPrice === "") errors.minPrice = "Trường Giá sàn là bắt buộc.";
  if (maxPrice === "") errors.maxPrice = "Trường Giá trần là bắt buộc.";
  if (!errors.minPrice && !errors.maxPrice && min > max) errors.maxPrice = "Giá sàn không được lớn hơn giá trần.";
  if (!effectiveFrom) errors.effectiveFrom = "Trường Hiệu lực từ là bắt buộc.";
  else if (effectiveFrom < isoDate(new Date())) errors.effectiveFrom = "Ngày hiệu lực phải từ hôm nay trở đi.";
  return errors;
}

export default function PriceFrameDialog({ frame, onClose, onSave }) {
  const [values, setValues] = useState({ minPrice: "", maxPrice: "", effectiveFrom: "" });
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (frame) {
      setValues({ minPrice: String(frame.minPrice), maxPrice: String(frame.maxPrice), effectiveFrom: isoDate(new Date()) });
      setErrors({});
      setServerError("");
    }
  }, [frame]);

  const set = (key) => (e) => setValues((v) => ({ ...v, [key]: e.target.value }));
  const save = async () => {
    const next = validatePriceFrame(values);
    setErrors(next);
    if (Object.keys(next).length) return;
    setSaving(true);
    const message = await onSave(frame, { minPrice: moneyInput(values.minPrice), maxPrice: moneyInput(values.maxPrice), effectiveFrom: values.effectiveFrom });
    setSaving(false);
    setServerError(message ?? "");
  };

  return (
    <Dialog open={!!frame} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Đặt khung giá · {frame?.serviceName}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {serverError && <Alert severity="error">{serverError}</Alert>}
          <TextField id="minPrice" label="Giá sàn (VND)" inputMode="numeric" value={values.minPrice} onChange={set("minPrice")} error={!!errors.minPrice} helperText={errors.minPrice} />
          <TextField id="maxPrice" label="Giá trần (VND)" inputMode="numeric" value={values.maxPrice} onChange={set("maxPrice")} error={!!errors.maxPrice} helperText={errors.maxPrice} />
          <TextField id="effectiveDate" label="Hiệu lực từ" type="date" value={values.effectiveFrom} onChange={set("effectiveFrom")}
            error={!!errors.effectiveFrom} helperText={errors.effectiveFrom ?? "Không ảnh hưởng hợp đồng đã chốt trước ngày này."} slotProps={{ inputLabel: { shrink: true } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={saving}>{saving ? "Đang lưu…" : "Lưu khung giá"}</Button>
      </DialogActions>
    </Dialog>
  );
}

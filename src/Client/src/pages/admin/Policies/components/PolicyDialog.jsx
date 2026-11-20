import { useEffect, useState } from "react";
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from "@mui/material";
import { isoDate } from "../../../../utils/format";

const FIELDS = [
  { key: "depositRatio", label: "Tiền đặt cọc (%)", min: 1, max: 100 },
  { key: "maxDecreaseRatio", label: "Giảm tối đa trong giai đoạn điều chỉnh (%)", min: 0, max: 99 },
  { key: "bookingMinDays", label: "Ký hợp đồng: ngày cưới cách tối thiểu (ngày)", min: 1, max: 365, integer: true },
  { key: "bookingMaxDays", label: "Ký hợp đồng: ngày cưới cách tối đa (ngày)", min: 1, max: 365, integer: true },
  { key: "lockDays", label: "Tự khóa dịch vụ trước ngày cưới (ngày)", min: 1, max: 365, integer: true },
  { key: "supplierDeadlineDays", label: "Hạn đặt nhà cung cấp trước ngày cưới (ngày)", min: 1, max: 365, integer: true },
  { key: "settlementDueDays", label: "Hạn tất toán sau ngày cưới (ngày)", min: 0, max: 90, integer: true },
];
const HOTLINE = /^[0-9 .+]{6,20}$/;

export function validatePolicy(v) {
  const errors = {};
  for (const f of FIELDS) {
    const n = Number(v[f.key]);
    if (v[f.key] === "" || v[f.key] == null) errors[f.key] = "Bắt buộc.";
    else if (Number.isNaN(n) || n < f.min || n > f.max) errors[f.key] = `Từ ${f.min} đến ${f.max}.`;
    else if (f.integer && !Number.isInteger(n)) errors[f.key] = "Phải là số nguyên.";
  }
  if (!errors.bookingMaxDays && Number(v.bookingMaxDays) < Number(v.bookingMinDays)) errors.bookingMaxDays = "Phải lớn hơn hoặc bằng số ngày tối thiểu.";
  if (!errors.lockDays && Number(v.lockDays) < Number(v.supplierDeadlineDays)) errors.lockDays = "Mốc khóa phải sớm hơn hoặc bằng hạn đặt nhà cung cấp.";
  if (!errors.lockDays && Number(v.lockDays) >= Number(v.bookingMinDays)) errors.lockDays = "Mốc khóa phải nhỏ hơn số ngày ký tối thiểu.";
  if (!errors.maxDecreaseRatio && 100 - Number(v.maxDecreaseRatio) < Number(v.depositRatio)) errors.maxDecreaseRatio = "Giá trị sau khi giảm phải luôn lớn hơn tiền đặt cọc.";
  if (!HOTLINE.test(String(v.hotline ?? "").trim())) errors.hotline = "Chỉ gồm chữ số (6–20 ký tự).";
  if (!v.effectiveFrom) errors.effectiveFrom = "Bắt buộc.";
  else if (v.effectiveFrom < isoDate(new Date())) errors.effectiveFrom = "Ngày hiệu lực phải từ hôm nay trở đi.";
  return errors;
}

export default function PolicyDialog({ open, policy, onClose, onSave }) {
  const [values, setValues] = useState({});
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (open && policy) {
      setValues({ ...Object.fromEntries(FIELDS.map((f) => [f.key, String(policy[f.key])])), hotline: policy.hotline, effectiveFrom: isoDate(new Date()) });
      setErrors({});
      setServerError("");
    }
  }, [open, policy]);

  const save = async () => {
    const next = validatePolicy(values);
    setErrors(next);
    if (Object.keys(next).length) return;
    setSaving(true);
    const message = await onSave({ ...Object.fromEntries(FIELDS.map((f) => [f.key, Number(values[f.key])])), hotline: values.hotline.trim(), effectiveFrom: values.effectiveFrom });
    setSaving(false);
    setServerError(message ?? "");
  };

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="md">
      <DialogTitle>Chính sách sự kiện</DialogTitle>
      <DialogContent>
        {serverError && <Alert severity="error" sx={{ mb: 2 }}>{serverError}</Alert>}
        <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { md: "1fr 1fr" }, pt: 1 }}>
          {FIELDS.map((f) => (
            <TextField key={f.key} id={f.key} label={f.label} type="number" value={values[f.key] ?? ""}
              onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))} error={!!errors[f.key]} helperText={errors[f.key]} />
          ))}
          <TextField id="hotline" label="Hotline" value={values.hotline ?? ""} onChange={(e) => setValues((v) => ({ ...v, hotline: e.target.value }))}
            error={!!errors.hotline} helperText={errors.hotline} />
        </Box>
        <Stack sx={{ mt: 2 }}>
          <TextField id="effectiveFrom" label="Hiệu lực từ" type="date" value={values.effectiveFrom ?? ""}
            onChange={(e) => setValues((v) => ({ ...v, effectiveFrom: e.target.value }))} error={!!errors.effectiveFrom}
            helperText={errors.effectiveFrom ?? "Hotline đổi ngay khi lưu."} slotProps={{ inputLabel: { shrink: true } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={saving}>{saving ? "Đang lưu…" : "Lưu"}</Button>
      </DialogActions>
    </Dialog>
  );
}

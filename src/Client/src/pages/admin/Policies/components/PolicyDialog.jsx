import { useEffect, useState } from "react";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from "@mui/material";
import { isoDate } from "../../../../utils/format";

// A-07 (kind "deposit": NRD and T-30 ratios); A-08 ranges live in PenaltyRulesDialog. SRS 10.4:
// ratios 0–100, days integer ≥ 0, effective from today or later.
const FIELDS = {
  deposit: [
    { key: "nrdRatio", label: "Tỷ lệ NRD (%)", max: 100 },
    { key: "t30Ratio", label: "Tỷ lệ cộng dồn đến T-30 (%)", max: 100 },
  ],
  penalty: [
    { key: "crPenaltyDays", label: "Số ngày trước tiệc", integer: true },
    { key: "crPenaltyPercent", label: "Mức phạt (%)", max: 100 },
  ],
};

export function validatePolicy(kind, values) {
  const errors = {};
  for (const f of FIELDS[kind]) {
    const raw = values[f.key];
    const n = Number(raw);
    if (raw === "" || raw == null) errors[f.key] = `Trường ${f.label} là bắt buộc.`;
    else if (Number.isNaN(n) || n < 0 || (f.max != null && n > f.max)) errors[f.key] = f.max != null ? "Giá trị từ 0 đến 100." : "Giá trị phải từ 0 trở lên.";
    else if (f.integer && !Number.isInteger(n)) errors[f.key] = "Giá trị phải là số nguyên.";
  }
  if (kind === "deposit" && !errors.nrdRatio && !errors.t30Ratio && Number(values.t30Ratio) < Number(values.nrdRatio))
    errors.t30Ratio = "Tỷ lệ T-30 là tỷ lệ cộng dồn nên không nhỏ hơn tỷ lệ NRD.";
  if (!values.effectiveFrom) errors.effectiveFrom = "Trường Hiệu lực từ là bắt buộc.";
  else if (values.effectiveFrom < isoDate(new Date())) errors.effectiveFrom = "Ngày hiệu lực phải từ hôm nay trở đi.";
  return errors;
}

export default function PolicyDialog({ kind, policy, onClose, onSave }) {
  const [values, setValues] = useState({});
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (kind) {
      setValues({ ...Object.fromEntries(FIELDS[kind].map((f) => [f.key, String(policy[f.key])])), effectiveFrom: isoDate(new Date()) });
      setErrors({});
      setServerError("");
    }
  }, [kind, policy]);

  const save = async () => {
    const next = validatePolicy(kind, values);
    setErrors(next);
    if (Object.keys(next).length) return;
    setSaving(true);
    const changed = Object.fromEntries(FIELDS[kind].map((f) => [f.key, Number(values[f.key])]));
    const message = await onSave({ ...policy, ...changed, effectiveFrom: values.effectiveFrom });
    setSaving(false);
    setServerError(message ?? "");
  };

  return (
    <Dialog open={!!kind} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{kind === "deposit" ? "Tỷ lệ đặt cọc và thanh toán (A-07)" : "Quy tắc phạt CR theo T-day (A-08)"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {serverError && <Alert severity="error">{serverError}</Alert>}
          {kind && FIELDS[kind].map((f) => (
            <TextField key={f.key} id={f.key} label={f.label} type="number" value={values[f.key] ?? ""}
              onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))} error={!!errors[f.key]} helperText={errors[f.key]} />
          ))}
          <TextField id="effectiveFrom" label="Hiệu lực từ" type="date" value={values.effectiveFrom ?? ""}
            onChange={(e) => setValues((v) => ({ ...v, effectiveFrom: e.target.value }))} error={!!errors.effectiveFrom}
            helperText={errors.effectiveFrom ?? "Không ảnh hưởng hợp đồng và phụ lục đã tạo trước ngày này."} slotProps={{ inputLabel: { shrink: true } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={saving}>{saving ? "Đang lưu…" : "Lưu"}</Button>
      </DialogActions>
    </Dialog>
  );
}

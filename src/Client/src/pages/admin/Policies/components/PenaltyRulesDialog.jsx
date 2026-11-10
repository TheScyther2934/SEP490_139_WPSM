import { useEffect, useState } from "react";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Stack, TextField } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { isoDate } from "../../../../utils/format";

// A-08 (UC-40): rows of T-day range and rate. Integers ≥ 0, min ≤ max, rate 0–100, no overlap (MSG23), effective today or later.
export function validateRules(rules, effectiveFrom) {
  const errors = rules.map((r) => {
    const min = Number(r.tDayMin), max = Number(r.tDayMax), rate = Number(r.ratePercent);
    if ([r.tDayMin, r.tDayMax, r.ratePercent].some((v) => v === "" || v == null)) return "Nhập đủ T-day và tỷ lệ.";
    if (!Number.isInteger(min) || !Number.isInteger(max) || min < 0 || min > max) return "T-day là số nguyên ≥ 0 và tối thiểu ≤ tối đa.";
    if (Number.isNaN(rate) || rate < 0 || rate > 100) return "Tỷ lệ phạt từ 0 đến 100%.";
    return "";
  });
  const order = rules.map((r, i) => ({ min: Number(r.tDayMin), max: Number(r.tDayMax), i })).sort((a, b) => a.min - b.min);
  for (let k = 1; k < order.length; k++)
    if (!errors[order[k].i] && !errors[order[k - 1].i] && order[k].min <= order[k - 1].max) errors[order[k].i] = "Các khoảng T-day không được chồng lấn (MSG23).";
  const dateError = !effectiveFrom ? "Trường Hiệu lực từ là bắt buộc." : effectiveFrom < isoDate(new Date()) ? "Ngày hiệu lực phải từ hôm nay trở đi." : "";
  return { rows: errors, date: dateError, ok: rules.length > 0 && errors.every((e) => !e) && !dateError };
}

export default function PenaltyRulesDialog({ open, rules, onClose, onSave }) {
  const [rows, setRows] = useState([]);
  const [effectiveFrom, setEffectiveFrom] = useState(isoDate(new Date()));
  const [show, setShow] = useState(false);
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (!open) return;
    setRows(rules.length ? rules.map((r) => ({ tDayMin: String(r.tDayMin), tDayMax: String(r.tDayMax), ratePercent: String(r.ratePercent) }))
      : [{ tDayMin: "0", tDayMax: "14", ratePercent: "5" }]);
    setEffectiveFrom(isoDate(new Date()));
    setShow(false);
    setServerError("");
  }, [open, rules]);
  const check = validateRules(rows, effectiveFrom);
  const set = (i, key, value) => setRows((list) => list.map((r, k) => (k === i ? { ...r, [key]: value } : r)));
  const save = async () => {
    setShow(true);
    if (!check.ok) return;
    setSaving(true);
    const message = await onSave(rows.map((r) => ({ tDayMin: Number(r.tDayMin), tDayMax: Number(r.tDayMax), ratePercent: Number(r.ratePercent) })), effectiveFrom);
    setSaving(false);
    setServerError(message ?? "");
  };
  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Quy tắc phạt CR theo T-day (A-08)</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }} id="penaltyRules">
          {serverError && <Alert severity="error">{serverError}</Alert>}
          {rows.map((r, i) => (
            <Stack key={i} direction="row" spacing={1} sx={{ alignItems: "flex-start" }} data-rule={i}>
              <TextField label="T-day từ" type="number" value={r.tDayMin} onChange={(e) => set(i, "tDayMin", e.target.value)} sx={{ width: 110 }}
                error={show && !!check.rows[i]} slotProps={{ htmlInput: { min: 0, "data-field": "tDayMin" } }} />
              <TextField label="đến" type="number" value={r.tDayMax} onChange={(e) => set(i, "tDayMax", e.target.value)} sx={{ width: 110 }}
                error={show && !!check.rows[i]} slotProps={{ htmlInput: { min: 0, "data-field": "tDayMax" } }} />
              <TextField label="Phạt (%)" type="number" value={r.ratePercent} onChange={(e) => set(i, "ratePercent", e.target.value)} sx={{ flex: 1 }}
                error={show && !!check.rows[i]} helperText={show ? check.rows[i] || " " : " "} slotProps={{ htmlInput: { min: 0, max: 100, "data-field": "ratePercent" } }} />
              <IconButton aria-label="Xóa khoảng" onClick={() => setRows((list) => list.filter((_, k) => k !== i))} disabled={rows.length === 1}><DeleteIcon /></IconButton>
            </Stack>
          ))}
          <div><Button startIcon={<AddIcon />} onClick={() => setRows((list) => [...list, { tDayMin: "", tDayMax: "", ratePercent: "" }])}>Thêm khoảng</Button></div>
          <TextField label="Hiệu lực từ" type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)}
            error={show && !!check.date} helperText={(show && check.date) || "Không ảnh hưởng phụ lục đã tạo trước ngày này (GBR-14)."}
            slotProps={{ inputLabel: { shrink: true } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={saving}>{saving ? "Đang lưu…" : "Lưu"}</Button>
      </DialogActions>
    </Dialog>
  );
}

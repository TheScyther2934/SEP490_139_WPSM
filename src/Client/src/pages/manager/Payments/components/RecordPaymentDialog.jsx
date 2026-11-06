import { useEffect, useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { isoDate, money, moneyInput } from "../../../../utils/format";

// M-14 (UC-33) field rules: amount > 0 and ≤ outstanding (MSG11), date not in the future, receipt required for Bank.
export function validatePayment(form, outstanding) {
  const errors = {};
  if (!form.type) errors.type = "Trường Loại thanh toán là bắt buộc.";
  if (!(form.amount > 0)) errors.amount = "Số tiền phải lớn hơn 0.";
  else if (form.amount > outstanding) errors.amount = `Số tiền vượt quá số dư còn lại ${money(outstanding)} (MSG11).`;
  if (!form.method) errors.method = "Trường Phương thức là bắt buộc.";
  if (!form.paidDate) errors.paidDate = "Trường Ngày thanh toán là bắt buộc.";
  else if (form.paidDate > isoDate(new Date())) errors.paidDate = "Ngày thanh toán không được ở tương lai.";
  if (form.method === "Bank" && !form.receipt) errors.receipt = "Chứng từ chuyển khoản là bắt buộc khi thanh toán qua ngân hàng.";
  else if (form.receipt && (form.receipt.size > 10 * 1024 * 1024 || !/\.(pdf|jpe?g|png)$/i.test(form.receipt.name)))
    errors.receipt = "Chỉ chấp nhận tệp JPG, PNG hoặc PDF, tối đa 10 MB (MSG17).";
  return errors;
}

export default function RecordPaymentDialog({ open, payments, busy, onClose, onSave }) {
  const empty = { type: "", amount: 0, method: "", paidDate: isoDate(new Date()), receipt: null };
  const [form, setForm] = useState(empty);
  const [showErrors, setShowErrors] = useState(false);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { if (open) { setForm(empty); setShowErrors(false); } }, [open]);
  const t30Paid = payments.payments.some((p) => p.type === "T30" && p.status === "Paid");
  const errors = validatePayment(form, payments.outstanding);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const pickType = (type) => {
    const due = payments.payments.find((p) => p.type === type);
    set({ type, amount: type === "FINAL" ? payments.outstanding : due?.amount ?? 0 });
  };
  const save = () => {
    setShowErrors(true);
    if (Object.keys(errors).length === 0) onSave(form);
  };
  const err = (key) => (showErrors ? errors[key] : undefined);

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Ghi nhận thanh toán</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }} id="recordPaymentForm">
          <TextField id="paymentType" select label="Loại thanh toán" required value={form.type} onChange={(e) => pickType(e.target.value)}
            error={!!err("type")} helperText={err("type") ?? "NRD đã được ghi nhận khi khởi tạo hợp đồng."} slotProps={{ htmlInput: { "data-field": "paymentType" } }}>
            <MenuItem value="T30" disabled={t30Paid}>Thanh toán đợt T-30</MenuItem>
            <MenuItem value="FINAL" disabled={!payments.canRecordFinal}>Quyết toán (sau ngày tiệc)</MenuItem>
          </TextField>
          <TextField label="Số tiền (VND)" required value={form.amount ? form.amount.toLocaleString("vi-VN") : ""}
            onChange={(e) => set({ amount: moneyInput(e.target.value) })} error={!!err("amount")}
            helperText={err("amount") ?? `Số dư còn lại: ${money(payments.outstanding)}`} slotProps={{ htmlInput: { inputMode: "numeric", "data-field": "amount" } }} />
          <TextField id="paymentMethod" select label="Phương thức" required value={form.method} onChange={(e) => set({ method: e.target.value })}
            error={!!err("method")} helperText={err("method") ?? " "} slotProps={{ htmlInput: { "data-field": "method" } }}>
            <MenuItem value="Bank">Chuyển khoản</MenuItem>
            <MenuItem value="Cash">Tiền mặt</MenuItem>
          </TextField>
          <TextField type="date" label="Ngày thanh toán" required value={form.paidDate} onChange={(e) => set({ paidDate: e.target.value })}
            error={!!err("paidDate")} helperText={err("paidDate") ?? " "} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: isoDate(new Date()), "data-field": "paidDate" } }} />
          <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
            <Button component="label" variant="outlined" startIcon={<UploadFileIcon />}>
              Chứng từ
              <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png" data-field="receipt" onChange={(e) => set({ receipt: e.target.files?.[0] ?? null })} />
            </Button>
            <Typography variant="body2" color={err("receipt") ? "error" : "text.secondary"}>
              {err("receipt") ?? form.receipt?.name ?? (form.method === "Cash" ? "Không bắt buộc với tiền mặt" : "Bắt buộc với chuyển khoản")}
            </Typography>
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={busy}>Ghi nhận</Button>
      </DialogActions>
    </Dialog>
  );
}

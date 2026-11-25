import { useEffect, useState } from "react";
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, MenuItem, TextField, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { roleLabel } from "../../../../utils/format";
import { PHONE_MESSAGE, isValidPhone } from "../../../../utils/phone";

const EMPTY = { fullName: "", email: "", phone: "", role: "Manager" };
const STAFF_ROLES = ["Manager", "Admin"];

// Same limits as the API: name ≤100, unique valid email ≤100, phone optional (PhonePolicy: starts with 0, 10–11 digits).
export function validateAccount(form) {
  const errors = {};
  if (!form.fullName.trim()) errors.fullName = "Trường Họ tên là bắt buộc.";
  else if (form.fullName.trim().length > 100) errors.fullName = "Họ tên tối đa 100 ký tự.";
  const email = form.email.trim();
  if (!email) errors.email = "Trường Email là bắt buộc.";
  else if (email.length > 100 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = "Email không hợp lệ.";
  if (form.phone.trim() && !isValidPhone(form.phone)) errors.phone = PHONE_MESSAGE;
  return errors;
}

// Add (Manager/Admin only) or edit. A Customer keeps its role; an admin cannot change their own role.
export default function AccountDialog({ open, initial, self, onClose, onSave }) {
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (!open) return;
    setForm(initial ? { fullName: initial.fullName, email: initial.email, phone: initial.phone ?? "", role: initial.role } : EMPTY);
    setErrors({});
    setServerError("");
  }, [open, initial]);

  const set = (key) => (e) => setForm((v) => ({ ...v, [key]: e.target.value }));
  const save = async () => {
    const next = validateAccount(form);
    setErrors(next);
    if (Object.keys(next).length) return;
    setSaving(true);
    const message = await onSave({ fullName: form.fullName.trim(), email: form.email.trim(), phone: form.phone.trim(), role: form.role });
    setSaving(false);
    setServerError(message ?? "");
  };
  const customer = initial?.role === "Customer";
  const roleLocked = customer || self;

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle sx={{ pr: 6 }}>
        {initial ? `Sửa tài khoản #${initial.id}` : "Thêm tài khoản"}
        <IconButton aria-label="Đóng" onClick={onClose} disabled={saving} sx={{ position: "absolute", right: 8, top: 8 }}><CloseIcon /></IconButton>
      </DialogTitle>
      <DialogContent>
        {serverError && <Alert severity="error" sx={{ mb: 2 }}>{serverError}</Alert>}
        <Box sx={{ display: "grid", gap: 2.5, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, pt: 1 }}>
          <TextField id="accountFullName" label="Họ tên" required value={form.fullName} onChange={set("fullName")} error={!!errors.fullName}
            helperText={errors.fullName ?? `${form.fullName.trim().length}/100`} sx={{ gridColumn: "1 / -1" }} />
          <TextField id="accountEmail" label="Email đăng nhập" required type="email" value={form.email} onChange={set("email")} error={!!errors.email}
            helperText={errors.email ?? " "} sx={{ gridColumn: "1 / -1" }} />
          <TextField id="accountPhone" label="Số điện thoại" value={form.phone} onChange={set("phone")} error={!!errors.phone}
            helperText={errors.phone ?? "Không bắt buộc"} slotProps={{ htmlInput: { inputMode: "tel" } }} />
          <TextField id="accountRole" select label="Vai trò" required value={form.role} onChange={set("role")} disabled={roleLocked}
            helperText={customer ? "Tài khoản khách hàng giữ nguyên vai trò." : self ? "Không thể tự đổi vai trò." : " "}>
            {(customer ? ["Customer"] : STAFF_ROLES).map((r) => <MenuItem key={r} value={r}>{roleLabel(r)}</MenuItem>)}
          </TextField>
        </Box>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          {initial
            ? "Đổi email sẽ đổi email đăng nhập của người dùng."
            : "Hệ thống sinh mật khẩu tạm và gửi qua email; người dùng phải đổi mật khẩu ở lần đăng nhập đầu. Tài khoản khách hàng được tạo khi Manager khởi tạo sự kiện."}
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button variant="outlined" color="inherit" onClick={onClose} disabled={saving} sx={{ minWidth: 100 }}>Hủy</Button>
        <Button id="saveAccount" variant="contained" onClick={save} disabled={saving} sx={{ minWidth: 100 }}>{saving ? "Đang lưu…" : "Lưu"}</Button>
      </DialogActions>
    </Dialog>
  );
}

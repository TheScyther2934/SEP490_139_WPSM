import { useState } from "react";
import { useDispatch } from "react-redux";
import { Alert, Button, Stack } from "@mui/material";
import PasswordField from "./PasswordField";
import { changePassword } from "../store/authSlice";

// G-03 rules (SRS 1.2): new password 8–64 chars with a letter and a digit, different from the current one, confirmed.
export function validatePasswords({ currentPassword, newPassword, confirmPassword }) {
  const errors = {};
  if (!currentPassword) errors.currentPassword = "Trường Mật khẩu hiện tại là bắt buộc.";
  if (!newPassword) errors.newPassword = "Trường Mật khẩu mới là bắt buộc.";
  else if (newPassword.length < 8 || newPassword.length > 64) errors.newPassword = "Mật khẩu mới dài từ 8 đến 64 ký tự.";
  else if (!/[A-Za-z]/.test(newPassword) || !/\d/.test(newPassword)) errors.newPassword = "Mật khẩu mới cần ít nhất một chữ cái và một chữ số.";
  else if (newPassword === currentPassword) errors.newPassword = "Mật khẩu mới không được trùng với mật khẩu hiện tại.";
  if (!errors.newPassword && newPassword !== confirmPassword) errors.confirmPassword = "Mật khẩu xác nhận không khớp với mật khẩu mới.";
  return errors;
}

export default function ChangePasswordForm({ onDone, onCancel, submitLabel = "Lưu mật khẩu" }) {
  const dispatch = useDispatch();
  const [values, setValues] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" });
  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  const field = (name) => ({
    id: name, value: values[name], error: !!errors[name], helperText: errors[name], fullWidth: true,
    onChange: (e) => setValues((v) => ({ ...v, [name]: e.target.value })),
  });

  const submit = async (event) => {
    event.preventDefault();
    const next = validatePasswords(values);
    setErrors(next);
    setServerError("");
    if (Object.keys(next).length) return;
    setSaving(true);
    const result = await dispatch(changePassword(values));
    setSaving(false);
    if (changePassword.rejected.match(result)) { setServerError(result.payload); return; }
    onDone();
  };

  return (
    <Stack component="form" spacing={2} onSubmit={submit} noValidate>
      {serverError && <Alert severity="error">{serverError}</Alert>}
      <PasswordField label="Mật khẩu hiện tại" autoComplete="current-password" {...field("currentPassword")} />
      <PasswordField label="Mật khẩu mới" autoComplete="new-password" {...field("newPassword")} />
      <PasswordField label="Xác nhận mật khẩu mới" autoComplete="new-password" {...field("confirmPassword")} />
      <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
        {onCancel && <Button onClick={onCancel} disabled={saving}>Hủy</Button>}
        <Button type="submit" variant="contained" disabled={saving}>{saving ? "Đang lưu…" : submitLabel}</Button>
      </Stack>
    </Stack>
  );
}

import { useState } from "react";
import { Alert, Button, Stack, TextField } from "@mui/material";
import PasswordField from "../../../../components/PasswordField";

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// G-01 fields: email (required, valid, ≤100) and password (required, 8–64). Server errors stay generic (GBR-11).
export default function LoginForm({ onSubmit, serverError, submitting }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState({});

  const submit = (event) => {
    event.preventDefault();
    const next = {};
    if (!email.trim()) next.email = "Trường Email là bắt buộc.";
    else if (!EMAIL.test(email.trim()) || email.length > 100) next.email = "Email không đúng định dạng.";
    if (!password) next.password = "Trường Mật khẩu là bắt buộc.";
    else if (password.length < 8 || password.length > 64) next.password = "Mật khẩu dài từ 8 đến 64 ký tự.";
    setErrors(next);
    if (!Object.keys(next).length) onSubmit({ email: email.trim(), password });
  };

  return (
    <Stack component="form" spacing={2} onSubmit={submit} noValidate>
      {serverError && <Alert severity="error" role="alert">{serverError}</Alert>}
      <TextField id="email" label="Email" type="email" autoComplete="username" autoFocus value={email}
        onChange={(e) => setEmail(e.target.value)} error={!!errors.email} helperText={errors.email} fullWidth />
      <PasswordField id="password" label="Mật khẩu" autoComplete="current-password" value={password}
        onChange={(e) => setPassword(e.target.value)} error={!!errors.password} helperText={errors.password} fullWidth />
      <Button type="submit" variant="contained" size="large" disabled={submitting}>
        {submitting ? "Đang đăng nhập…" : "Đăng nhập"}
      </Button>
    </Stack>
  );
}

import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import { Button, Card, CardContent, Typography } from "@mui/material";
import LoginForm from "./components/LoginForm";
import SupportDialog from "./components/SupportDialog";
import { login, selectAuth } from "../../../store/authSlice";
import { PATHS, homeFor } from "../../../routes/paths";

// G-01. After sign-in: forced password change first, then the page the user came from, else the role home.
export default function Login() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const { status, user } = useSelector(selectAuth);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [supportOpen, setSupportOpen] = useState(false);

  if (status === "authenticated" && !submitting) return <Navigate to={homeFor(user.role)} replace />;

  const submit = async (credentials) => {
    setSubmitting(true);
    setError("");
    const result = await dispatch(login(credentials));
    if (login.rejected.match(result)) {
      setError(result.payload);
      setSubmitting(false);
      return;
    }
    const signedIn = result.payload.user;
    const from = location.state?.from?.pathname;
    const target = signedIn.mustChangePassword ? PATHS.changePassword
      : from && from.startsWith(`/${signedIn.role.toLowerCase()}`) ? from : homeFor(signedIn.role);
    navigate(target, { replace: true });
  };

  return (
    <Card>
      <CardContent sx={{ p: 4 }}>
        <Typography variant="h5" component="h1" gutterBottom>Đăng nhập</Typography>
        <Typography color="text.secondary" sx={{ mb: 3 }}>Hệ thống quản lý dự án tiệc cưới WPSM</Typography>
        <LoginForm onSubmit={submit} serverError={error} submitting={submitting} />
        <Button onClick={() => setSupportOpen(true)} sx={{ mt: 2 }} fullWidth>Quên mật khẩu?</Button>
        <SupportDialog open={supportOpen} onClose={() => setSupportOpen(false)} />
      </CardContent>
    </Card>
  );
}

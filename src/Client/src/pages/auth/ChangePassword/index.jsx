import { useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import { Alert, Button, Card, CardContent, Typography } from "@mui/material";
import ChangePasswordForm from "../../../components/ChangePasswordForm";
import { logout, selectUser } from "../../../store/authSlice";
import { notify } from "../../../store/notifySlice";
import { PATHS, homeFor } from "../../../routes/paths";

// G-03 as a page: shown when the account still has its temporary password; every other route redirects here.
export default function ChangePassword() {
  const user = useSelector(selectUser);
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const done = () => {
    dispatch(notify("Đổi mật khẩu thành công."));
    navigate(homeFor(user.role), { replace: true });
  };
  const signOut = async () => {
    await dispatch(logout());
    navigate(PATHS.login, { replace: true });
  };

  return (
    <Card>
      <CardContent sx={{ p: 4 }}>
        <Typography variant="h5" component="h1" gutterBottom>Đổi mật khẩu</Typography>
        {user.mustChangePassword && (
          <Alert severity="info" sx={{ mb: 2 }}>Vui lòng đổi mật khẩu tạm để bắt đầu sử dụng tài khoản.</Alert>
        )}
        <ChangePasswordForm onDone={done} submitLabel="Đổi mật khẩu" />
        <Button onClick={signOut} color="inherit" sx={{ mt: 2 }} fullWidth>Đăng xuất</Button>
      </CardContent>
    </Card>
  );
}

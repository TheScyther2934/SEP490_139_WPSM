import { useState } from "react";
import { useDispatch } from "react-redux";
import { Alert, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import { projectService } from "../../../../services/projectService";
import { notify } from "../../../../store/notifySlice";
import { errorMessage } from "../../../../utils/format";

const EMAIL_STATUS = { Pending: "Đang chờ gửi", Sent: "Đã gửi", Failed: "Gửi thất bại", ExistingAccount: "Tài khoản đã có" };

// Customer contact and the credential e-mail; resending issues a new temporary password.
export default function CustomerAccountCard({ project }) {
  const dispatch = useDispatch();
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [password, setPassword] = useState(null);

  const resend = async () => {
    setSending(true);
    setError("");
    try {
      const result = await projectService.resendCredentials(project.id);
      setPassword(result.temporaryPassword ?? null);
      dispatch(notify("Đã xếp lại email thông tin đăng nhập."));
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSending(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Khách hàng</Typography>
        <Typography>{project.coupleName}</Typography>
        <Typography variant="body2" color="text.secondary">{project.customerEmail} · {project.customerPhone}</Typography>
        <Typography variant="body2" sx={{ mt: 1 }}>Email tài khoản: {EMAIL_STATUS[project.credentialEmailStatus] ?? "Chưa có thông tin"}</Typography>
        {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
        {password && <Alert severity="info" sx={{ mt: 1 }}>Mật khẩu tạm mới: <b>{password}</b> (chỉ hiển thị lần này).</Alert>}
        <Stack direction="row" sx={{ mt: 2 }}>
          <Button variant="outlined" size="small" onClick={resend} disabled={sending}>{sending ? "Đang gửi…" : "Gửi lại thông tin đăng nhập"}</Button>
        </Stack>
      </CardContent>
    </Card>
  );
}

import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from "@mui/material";

// After create/reissue. The API returns the temporary password only in Development without SMTP (as M-03 does);
// otherwise it is only in the email.
export default function CredentialDialog({ credential, onClose }) {
  if (!credential) return null;
  const { account, password, reset } = credential;
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{reset ? "Đã cấp lại mật khẩu" : "Đã tạo tài khoản"}</DialogTitle>
      <DialogContent>
        <Typography sx={{ mb: 2 }}>
          Mật khẩu tạm đã được gửi tới <strong>{account.email}</strong>. Người dùng phải đổi mật khẩu ở lần đăng nhập đầu.
        </Typography>
        {password && (
          <Alert severity="info" id="temporaryPassword">
            Môi trường phát triển chưa cấu hình SMTP. Mật khẩu tạm: <strong data-password>{password}</strong>
          </Alert>
        )}
      </DialogContent>
      <DialogActions><Button variant="contained" onClick={onClose}>Đóng</Button></DialogActions>
    </Dialog>
  );
}

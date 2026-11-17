import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from "@mui/material";

// Confirms lock/unlock and password reissue. Locking keeps every record and signs the user out of all devices.
export default function AccountConfirmDialog({ confirm, busy, onClose, onConfirm }) {
  const account = confirm?.account;
  const lock = confirm?.kind === "status" && account?.active;
  const title = confirm?.kind === "reset" ? "Cấp lại mật khẩu?" : lock ? "Khóa tài khoản?" : "Mở khóa tài khoản?";
  const text = confirm?.kind === "reset"
    ? `Mật khẩu hiện tại của ${account?.email} sẽ mất hiệu lực. Hệ thống gửi mật khẩu tạm mới qua email và đăng xuất người dùng khỏi mọi thiết bị.`
    : lock
      ? `${account?.email} sẽ không đăng nhập được nữa và bị đăng xuất khỏi mọi thiết bị. Dữ liệu dự án vẫn được giữ nguyên.`
      : `${account?.email} sẽ đăng nhập lại được bằng mật khẩu hiện có.`;
  return (
    <Dialog open={!!confirm} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent><DialogContentText>{text}</DialogContentText></DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={onClose} disabled={busy}>Hủy</Button>
        <Button id="confirmAccountAction" variant="contained" color={lock ? "error" : "primary"} onClick={onConfirm} disabled={busy}>Xác nhận</Button>
      </DialogActions>
    </Dialog>
  );
}

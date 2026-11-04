import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from "@mui/material";

// Accounts are issued by the venue, so "forgot password" means contacting them.
export default function SupportDialog({ open, onClose }) {
  return (
    <Dialog open={open} onClose={onClose}>
      <DialogTitle>Cần hỗ trợ đăng nhập?</DialogTitle>
      <DialogContent>
        <Typography>Tài khoản do trung tâm cấp. Nếu quên mật khẩu hoặc tài khoản bị khóa, vui lòng liên hệ hotline 1900 6868 để được cấp lại.</Typography>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Đóng</Button></DialogActions>
    </Dialog>
  );
}

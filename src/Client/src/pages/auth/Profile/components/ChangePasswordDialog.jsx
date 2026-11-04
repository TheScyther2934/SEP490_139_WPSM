import { Dialog, DialogContent, DialogTitle } from "@mui/material";
import ChangePasswordForm from "../../../../components/ChangePasswordForm";

// G-03 dialog opened from G-02; Cancel closes it without changing anything.
export default function ChangePasswordDialog({ open, onClose, onSaved }) {
  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>Đổi mật khẩu</DialogTitle>
      <DialogContent sx={{ pt: "8px !important" }}>
        {open && <ChangePasswordForm onDone={onSaved} onCancel={onClose} />}
      </DialogContent>
    </Dialog>
  );
}

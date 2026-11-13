import { useEffect, useState } from "react";
import { Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Typography } from "@mui/material";

// Confirmation before freezing Baseline v1; the current rows are saved first, then frozen.
export default function FreezeDialog({ open, saving, onClose, onConfirm }) {
  const [consent, setConsent] = useState(false);
  useEffect(() => { if (open) setConsent(false); }, [open]);
  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Xác nhận đóng băng Baseline v1</DialogTitle>
      <DialogContent>
        <Typography>Sau khi đóng băng, không thể sửa trực tiếp đặc tả gốc. Mọi thay đổi về sau phải đi qua Yêu cầu thay đổi.</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>Hệ thống lưu đặc tả đang hiển thị, khóa baseline và tạo lộ trình T-90, T-30, T-7, Event Day.</Typography>
        <FormControlLabel sx={{ mt: 2 }} control={<Checkbox id="freezeConsent" checked={consent} onChange={(e) => setConsent(e.target.checked)} />}
          label="Tôi xác nhận đặc tả và đơn giá đã được kiểm tra để khóa Baseline v1." />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Quay lại</Button>
        <Button id="confirmFreezeBtn" variant="contained" disabled={!consent || saving} onClick={onConfirm}>{saving ? "Đang xử lý…" : "Lưu & đóng băng"}</Button>
      </DialogActions>
    </Dialog>
  );
}

import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from "@mui/material";
import { dateVi, money, moneyInput, slotLabel } from "../../../../utils/format";

export default function ConfirmDialog({ open, form, saving, onClose, onConfirm }) {
  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Xác nhận tạo dự án</DialogTitle>
      <DialogContent>
        <Typography id="createProjectConfirmSummary">
          {[form.coupleName, form.hall, `${dateVi(form.eventDate)} · Ca ${slotLabel(form.slot)}`, money(moneyInput(form.baseValue)), `NRD ${money(moneyInput(form.nrdAmount))}`].join(" · ")}
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 2 }}>
          Hệ thống giữ ca sảnh, tạo dự án ở trạng thái Active Baseline và gửi thông tin đăng nhập cho khách hàng.
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Quay lại</Button>
        <Button id="createProjectConfirmSubmit" variant="contained" onClick={onConfirm} disabled={saving}>{saving ? "Đang tạo…" : "Xác nhận tạo dự án"}</Button>
      </DialogActions>
    </Dialog>
  );
}

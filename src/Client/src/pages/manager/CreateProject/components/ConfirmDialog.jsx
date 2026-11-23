import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from "@mui/material";
import { contractValue } from "../validation";
import { dateVi, money, slotLabel } from "../../../../utils/format";

export default function ConfirmDialog({ open, form, deposit, saving, onClose, onConfirm }) {
  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Xác nhận tạo sự kiện</DialogTitle>
      <DialogContent>
        <Typography id="createProjectConfirmSummary">
          {[form.coupleName, form.hall, `${dateVi(form.eventDate)} · Ca ${slotLabel(form.slot)}`, `${form.lines.length} dịch vụ`,
            money(contractValue(form.lines)), `Đặt cọc ${money(deposit)}`].join(" · ")}
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 2 }}>
          Hệ thống giữ ca sảnh và chuyển sự kiện sang giai đoạn điều chỉnh. Khách mới được cấp tài khoản; khách cũ dùng lại tài khoản đã có.
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Quay lại</Button>
        <Button id="createProjectConfirmSubmit" variant="contained" onClick={onConfirm} disabled={saving}>{saving ? "Đang tạo…" : "Xác nhận tạo sự kiện"}</Button>
      </DialogActions>
    </Dialog>
  );
}

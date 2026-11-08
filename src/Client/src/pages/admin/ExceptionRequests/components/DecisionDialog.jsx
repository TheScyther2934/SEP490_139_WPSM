import { Alert, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from "@mui/material";
import { sourceLabel } from "../exceptionMeta";

// UC-47 confirmation: approving bends a venue rule, rejecting is final, so both ask before writing.
export default function DecisionDialog({ open, approve, request, note, busy, onClose, onConfirm }) {
  if (!request) return null;
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{approve ? "Duyệt ngoại lệ" : "Từ chối ngoại lệ"}</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {approve
            ? `Chấp thuận yêu cầu ${request.code} (${sourceLabel(request.sourceType)}) cho dự án ${request.projectCode}. Hệ thống áp dụng ngoại lệ và ghi hồ sơ kiểm toán.`
            : `Từ chối yêu cầu ${request.code} (${sourceLabel(request.sourceType)}) cho dự án ${request.projectCode}. Quy tắc chuẩn được giữ nguyên; quyết định không thể hoàn tác.`}
        </DialogContentText>
        <Alert severity="info" sx={{ mt: 2 }}>Ghi chú quyết định: “{note}”</Alert>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button color={approve ? "primary" : "error"} onClick={onConfirm} disabled={busy} id="a18ConfirmDecision">
          {approve ? "Duyệt ngoại lệ" : "Từ chối"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

import { useEffect, useState } from "react";
import {
  Alert, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Divider, Stack, TextField, Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import { errorMessage } from "../../../../utils/format";

const REASON_MAX = 500;

// M-08 decision block: the Manager signs off or rejects with the mandatory reason (GBR-04, GBR-13).
// Approving asks for confirmation because it generates the addendum once the Customer also approved.
export default function DecisionPanel({ ticket, onApprove, onReject }) {
  const [reason, setReason] = useState("");
  const [confirm, setConfirm] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => { setReason(""); setError(""); }, [ticket]);

  const decide = async () => {
    setError("");
    setBusy(true);
    try {
      if (confirm === "approve") await onApprove();
      else await onReject(reason.trim());
      setConfirm(null);
    } catch (e) {
      setError(errorMessage(e));
      setConfirm(null);
    } finally {
      setBusy(false);
    }
  };

  const reasonError = !reason.trim() ? "Trường Lý do từ chối là bắt buộc." : "";

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Quyết định của Quản lý</Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}

        {ticket.managerApprovedAt ? (
          <Alert severity="success" sx={{ mb: 2 }}>
            Bạn đã phê duyệt yêu cầu này. CR chuyển sang Approved khi khách hàng cũng phê duyệt (GBR-04).
          </Alert>
        ) : (
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Phê duyệt khi các giá trị đã được hai bên thống nhất. Khi khách hàng cũng đồng ý, hệ thống tạo phụ lục hợp đồng
            và cập nhật tổng giá trị dự án.
          </Typography>
        )}

        <TextField id="rejectReason" label="Lý do từ chối (bắt buộc khi từ chối)" multiline minRows={2} fullWidth value={reason}
          onChange={(e) => setReason(e.target.value)} error={!!error && !!reasonError}
          helperText={`${reason.length}/${REASON_MAX} ký tự`} slotProps={{ htmlInput: { maxLength: REASON_MAX } }} />

        <Divider sx={{ my: 2 }} />
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
          <Button variant="outlined" color="error" startIcon={<CancelIcon />} disabled={busy || !reason.trim()}
            onClick={() => setConfirm("reject")} id="rejectCrBtn">
            Từ chối CR
          </Button>
          <Button variant="contained" startIcon={<CheckCircleIcon />} disabled={busy || !ticket.canApprove}
            onClick={() => setConfirm("approve")} id="approveCrBtn">
            {busy ? "Đang xử lý…" : "Phê duyệt CR"}
          </Button>
        </Stack>
        {!ticket.canApprove && !ticket.managerApprovedAt && (
          <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }}>
            CR không còn ở trạng thái có thể phê duyệt.
          </Typography>
        )}
      </CardContent>

      <Dialog open={!!confirm} onClose={() => (busy ? null : setConfirm(null))} fullWidth maxWidth="xs">
        <DialogTitle>{confirm === "approve" ? "Phê duyệt yêu cầu thay đổi" : "Từ chối yêu cầu thay đổi"}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirm === "approve"
              ? "Hệ thống ghi nhận phê duyệt của Quản lý. Khi khách hàng cũng phê duyệt, CR sẽ Approved và phụ lục hợp đồng được tạo."
              : "Yêu cầu sẽ đóng ở trạng thái Rejected và được lưu cho mục đích đối soát. Hành động này không thể hoàn tác."}
          </DialogContentText>
          {confirm === "reject" && reasonError && <Alert severity="error" sx={{ mt: 2 }}>{reasonError}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirm(null)} disabled={busy}>Hủy</Button>
          <Button onClick={decide} color={confirm === "approve" ? "primary" : "error"} disabled={busy} id="crDecisionConfirm">
            {confirm === "approve" ? "Phê duyệt" : "Từ chối"}
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}

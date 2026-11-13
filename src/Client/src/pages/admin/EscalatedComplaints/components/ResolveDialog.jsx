import { useEffect, useState } from "react";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField, Typography } from "@mui/material";

// A-17: confirm the final decision; the decision is closed for both parties after this (UC-46).
export default function ResolveDialog({ complaint, initialDecision, open, onClose, onConfirm }) {
  const [decision, setDecision] = useState(initialDecision);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (open) { setDecision(initialDecision); setError(""); } }, [open, initialDecision]);

  const confirm = async () => {
    if (!decision.trim()) { setError("Trường Quyết định là bắt buộc."); return; }
    setSaving(true);
    const message = await onConfirm(decision.trim());
    setSaving(false);
    if (message) setError(message);
  };

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Xác nhận giải quyết khiếu nại {complaint?.code}</DialogTitle>
      <DialogContent>
        <Typography color="text.secondary" sx={{ mb: 2 }}>Quyết định sẽ được gửi tới khách hàng và quản lý, khiếu nại chuyển sang trạng thái đã giải quyết.</Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <TextField id="a17Decision" label="Quyết định" multiline minRows={4} fullWidth value={decision} onChange={(e) => setDecision(e.target.value)} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button variant="contained" onClick={confirm} disabled={saving}>{saving ? "Đang lưu…" : "Xác nhận"}</Button>
      </DialogActions>
    </Dialog>
  );
}

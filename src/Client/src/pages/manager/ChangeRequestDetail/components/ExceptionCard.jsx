import { useEffect, useState } from "react";
import { Alert, Button, Card, CardContent, Chip, Stack, TextField, Typography } from "@mui/material";
import SupportAgentIcon from "@mui/icons-material/SupportAgent";
import { errorMessage, money } from "../../../../utils/format";

const EXCEPTION_LABELS = {
  Pending: { label: "Đang chờ Admin", color: "warning" },
  Approved: { label: "Admin đã chấp thuận", color: "success" },
  Rejected: { label: "Admin đã từ chối", color: "error" },
};

// UC-47 / A-18: a CR penalty can be waived by the Admin. The request is visible in the Admin queue only.
export default function ExceptionCard({ ticket, onRequest }) {
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => { setNote(""); setError(""); }, [ticket]);

  const state = EXCEPTION_LABELS[ticket.pendingException] ?? null;
  const submit = async () => {
    setError("");
    setBusy(true);
    try {
      await onRequest(note.trim());
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography variant="h6">Ngoại lệ từ Admin</Typography>
          {state && <Chip size="small" color={state.color} label={state.label} />}
        </Stack>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Phí phạt T-day hiện tại: <strong>{money(ticket.cost.penaltyFee)}</strong>
          {ticket.cost.penaltyRate > 0 ? ` (${ticket.cost.penaltyRate}%)` : ""}.
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Gửi đề nghị miễn/giảm phí phạt kèm lý do nghiệp vụ. CR vẫn tiếp tục thương lượng trong lúc chờ quyết định.
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}
        <TextField id="exceptionNote" label="Lý do đề nghị ngoại lệ" multiline minRows={2} value={note} fullWidth
          disabled={!ticket.canRequestException || busy} onChange={(e) => setNote(e.target.value)}
          helperText={`${note.length}/300 ký tự`} slotProps={{ htmlInput: { maxLength: 300 } }} />
        <Button sx={{ mt: 2 }} variant="outlined" startIcon={<SupportAgentIcon />} onClick={submit}
          disabled={!ticket.canRequestException || busy || !note.trim()} id="requestExceptionBtn">
          {busy ? "Đang gửi…" : "Gửi yêu cầu ngoại lệ"}
        </Button>
        {!ticket.canRequestException && !state && (
          <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }}>
            Chỉ gửi được khi CR đang thương lượng và có phát sinh phí phạt.
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

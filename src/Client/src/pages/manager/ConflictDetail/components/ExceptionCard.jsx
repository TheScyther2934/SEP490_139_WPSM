import { useEffect, useState } from "react";
import { Alert, Button, Card, CardContent, Chip, Stack, TextField, Typography } from "@mui/material";
import SupportAgentIcon from "@mui/icons-material/SupportAgent";
import { errorMessage } from "../../../../utils/format";

// UC-47 escalation from M-22: ask the Admin to accept the over-allocation (A-18).
export default function ExceptionCard({ allocation, onRequest }) {
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => { setNote(""); setError(""); }, [allocation]);

  const submit = async () => {
    setError("");
    setBusy(true);
    try {
      await onRequest(note.trim());
      setNote("");
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  const pending = allocation.pendingRequestCode;

  return (
    <Card>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography variant="h6">Ngoại lệ từ Admin</Typography>
          {pending && <Chip size="small" color="warning" label="Đang chờ quyết định" />}
        </Stack>

        {pending ? (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              Yêu cầu <strong>{pending}</strong> đang chờ Admin quyết định. Phân bổ tạm thời không chiếm công suất.
            </Typography>
            {allocation.pendingRequestNote && (
              <Typography variant="body2" sx={{ fontStyle: "italic" }}>“{allocation.pendingRequestNote}”</Typography>
            )}
            <Alert severity="info" sx={{ mt: 2 }}>
              Khi Admin chấp thuận, phân bổ chuyển sang <strong>Ngoại lệ đã duyệt</strong>; nếu bị từ chối, phân bổ được thu hồi.
            </Alert>
          </>
        ) : allocation.status === "Exception Approved" ? (
          <Alert severity="success">
            Admin đã chấp thuận ngoại lệ cho phân bổ vượt công suất này.
            {allocation.exceptionNote ? ` Ghi chú: ${allocation.exceptionNote}` : ""}
          </Alert>
        ) : allocation.canRequestException ? (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Thiếu {allocation.shortage} so với công suất. Nếu không thể đổi thiết bị hoặc dời giờ, hãy gửi lý do để Admin quyết định.
            </Typography>
            {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}
            <TextField id="exceptionRequestNote" label="Lý do đề nghị ngoại lệ" multiline minRows={2} fullWidth value={note}
              onChange={(event) => setNote(event.target.value)} helperText={`${note.length}/300 ký tự`}
              slotProps={{ htmlInput: { maxLength: 300 } }} />
            <Button sx={{ mt: 2 }} variant="outlined" startIcon={<SupportAgentIcon />} onClick={submit}
              disabled={busy || !note.trim()} id="requestAllocationExceptionBtn">
              {busy ? "Đang gửi…" : "Gửi yêu cầu ngoại lệ"}
            </Button>
          </>
        ) : (
          <Typography variant="body2" color="text.secondary">
            Phân bổ này không vượt công suất nên không cần ngoại lệ.
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

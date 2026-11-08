import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, Chip, Divider, Stack, TextField, Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import StatusChip from "../../../../components/StatusChip";
import CrBreakdown from "./CrBreakdown";
import ResourceBreakdown from "./ResourceBreakdown";
import DecisionDialog from "./DecisionDialog";
import { dateTimeVi, errorMessage } from "../../../../utils/format";
import { sourceColor, sourceDescription, sourceLabel, waitingLabel } from "../exceptionMeta";

const NOTE_MAX = 300;

const Field = ({ label, children }) => (
  <Box>
    <Typography variant="caption" color="text.secondary" component="p">{label}</Typography>
    <Typography variant="body2" fontWeight={600}>{children}</Typography>
  </Box>
);

// A-18 detail: source-specific audit context (CR cost breakdown or resource conflict) plus the
// mandatory-note decision form. UC-47 requires the note for both approve and reject.
export default function ExceptionDetail({ request, onDecide }) {
  const [note, setNote] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(null);

  useEffect(() => { setNote(""); setError(""); setConfirm(null); }, [request?.id]);

  if (!request) return null;
  const pending = request.status === "Pending";

  const submit = async (approve) => {
    setError("");
    if (!note.trim()) {
      setError("Trường Ghi chú quyết định là bắt buộc.");
      setConfirm(null);
      return;
    }
    setBusy(true);
    try {
      await onDecide(approve, note.trim());
      setConfirm(null);
    } catch (e) {
      setError(errorMessage(e));
      setConfirm(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card id="a18Detail">
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
          <Chip size="small" color={sourceColor(request.sourceType)} label={sourceLabel(request.sourceType)} />
          <Typography variant="subtitle1" fontWeight={700}>{request.code}</Typography>
          <StatusChip kind="exception" status={request.status} />
        </Stack>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{sourceDescription(request.sourceType)}</Typography>

        {pending && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            Yêu cầu đang chờ quyết định · {waitingLabel(request.createdAt)}. Cần ghi chú cho cả hai lựa chọn duyệt và từ chối.
          </Alert>
        )}
        {request.status === "Approved" && (
          <Alert severity="success" sx={{ mt: 2 }}>Ban Giám đốc đã chấp thuận ngoại lệ này.</Alert>
        )}
        {request.status === "Rejected" && (
          <Alert severity="error" sx={{ mt: 2 }}>Ban Giám đốc đã từ chối; quy tắc chuẩn của trung tâm được giữ nguyên.</Alert>
        )}

        <Typography variant="h6" sx={{ mt: 2 }}>
          {request.cr ? `Miễn trừ phí phạt cho ${request.cr.crCode}` : `Ngoại lệ giữ chỗ ${request.resource?.resourceName ?? ""}`}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {request.projectCode} · {request.projectCouple} · {request.hallName}
        </Typography>

        <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(3, 1fr)" }, mt: 2 }}>
          <Field label="Quản lý đề xuất">{request.requestedByName}</Field>
          <Field label="Thời điểm gửi">{dateTimeVi(request.createdAt)}</Field>
          <Field label="Thẩm quyền quyết định">Ban Giám đốc (Admin)</Field>
        </Box>

        <Divider sx={{ my: 2 }} />
        {request.cr ? <CrBreakdown cr={request.cr} /> : request.resource ? <ResourceBreakdown resource={request.resource} /> : null}

        <Divider sx={{ my: 2 }} />
        <Typography variant="subtitle2" gutterBottom>Giải trình của Quản lý</Typography>
        <Box sx={{ borderLeft: 3, borderColor: "divider", pl: 2, py: 0.5, bgcolor: "action.hover", borderRadius: 1 }}>
          <Typography variant="body2" sx={{ fontStyle: "italic" }}>“{request.note}”</Typography>
          <Typography variant="caption" color="text.secondary">{request.requestedByName}</Typography>
        </Box>

        <Alert severity="info" sx={{ mt: 2 }}>
          {request.cr
            ? "Nếu duyệt, hệ thống gỡ phí phạt khỏi CR và CR tiếp tục sinh phụ lục hợp đồng; nếu từ chối, phí phạt được giữ nguyên."
            : "Nếu duyệt, phân bổ giữ chỗ vượt công suất; nếu từ chối, phân bổ được thu hồi để nhường chỗ cho dự án khác."}
        </Alert>

        <Divider sx={{ my: 2 }} />
        {pending ? (
          <>
            <Typography variant="subtitle2" gutterBottom>Quyết định của Ban Giám đốc</Typography>
            {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}
            <TextField id="a18Note" label="Ghi chú quyết định (bắt buộc)" multiline minRows={3} fullWidth value={note}
              onChange={(event) => setNote(event.target.value)} helperText={`${note.length}/${NOTE_MAX} ký tự`}
              slotProps={{ htmlInput: { maxLength: NOTE_MAX } }} />
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ mt: 2, justifyContent: "flex-end" }}>
              <Button color="error" variant="outlined" startIcon={<CancelIcon />} disabled={busy || !note.trim()}
                onClick={() => setConfirm("reject")} id="a18RejectBtn">Từ chối ngoại lệ</Button>
              <Button variant="contained" startIcon={<CheckCircleIcon />} disabled={busy || !note.trim()}
                onClick={() => setConfirm("approve")} id="a18ApproveBtn">{busy ? "Đang xử lý…" : "Duyệt ngoại lệ"}</Button>
            </Stack>
            <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1, textAlign: "right" }}>
              Bắt buộc nhập lý do cho cả Phê duyệt và Từ chối để lưu hồ sơ kiểm toán.
            </Typography>
          </>
        ) : (
          <Box sx={{ bgcolor: "action.hover", borderRadius: 1, p: 2 }}>
            <Typography variant="subtitle2" gutterBottom>Hồ sơ quyết định</Typography>
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" } }}>
              <Field label="Người quyết định">{request.decidedByName ?? "—"}</Field>
              <Field label="Thời điểm">{request.decidedAt ? dateTimeVi(request.decidedAt) : "—"}</Field>
              <Field label="Kết quả">{request.status === "Approved" ? "Chấp thuận" : "Từ chối"}</Field>
            </Box>
            <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }}>
              Ghi chú quyết định: “{request.decisionNote ?? "—"}”
            </Typography>
          </Box>
        )}
      </CardContent>

      <DecisionDialog open={!!confirm} approve={confirm === "approve"} request={request} note={note}
        busy={busy} onClose={() => setConfirm(null)} onConfirm={() => submit(confirm === "approve")} />
    </Card>
  );
}

import { useEffect, useState } from "react";
import {
  Alert, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, MenuItem, Stack, TextField, Typography,
} from "@mui/material";
import SaveIcon from "@mui/icons-material/Save";
import UndoIcon from "@mui/icons-material/Undo";
import { errorMessage, dateVi } from "../../../../utils/format";
import { conflictOf } from "../../../../services/resourceCalendarService";
import { timeOfIso, toIsoUtc } from "../../ResourceCalendar/calendarRange";

const toMinutes = (value) => {
  const [hours, minutes] = String(value ?? "").split(":").map(Number);
  return (hours || 0) * 60 + (minutes || 0);
};

// M-22 reschedule / substitute: change the resource, time range or quantity and re-check GBR-09.
export default function ResolveCard({ allocation, resources, onSave, onRelease }) {
  const [resourceId, setResourceId] = useState(String(allocation.resourceId));
  const [start, setStart] = useState(timeOfIso(allocation.startTime));
  const [end, setEnd] = useState(timeOfIso(allocation.endTime));
  const [quantity, setQuantity] = useState(String(allocation.quantity));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [confirmRelease, setConfirmRelease] = useState(false);

  useEffect(() => {
    setResourceId(String(allocation.resourceId));
    setStart(timeOfIso(allocation.startTime));
    setEnd(timeOfIso(allocation.endTime));
    setQuantity(String(allocation.quantity));
    setError("");
  }, [allocation]);

  const editable = allocation.status === "Reserved";
  const resource = resources.find((entry) => entry.resourceId === Number(resourceId)) ?? null;

  const save = async () => {
    setError("");
    const value = Number(quantity);
    if (!Number.isInteger(value) || value <= 0) { setError("Số lượng phải là số nguyên lớn hơn 0."); return; }
    if (toMinutes(end) <= toMinutes(start)) { setError("Giờ kết thúc phải sau giờ bắt đầu."); return; }
    setBusy(true);
    try {
      await onSave({
        resourceId: Number(resourceId),
        projectId: allocation.projectId,
        startTime: toIsoUtc(allocation.eventDate, start),
        endTime: toIsoUtc(allocation.eventDate, end),
        quantity: value,
      });
    } catch (e) {
      const conflict = conflictOf(e);
      setError(conflict
        ? `${errorMessage(e)} (${conflict.resourceName}: đã giữ ${conflict.otherQuantity}/${conflict.totalQuantity}, yêu cầu ${conflict.requestedQuantity})`
        : errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  const release = async () => {
    setBusy(true);
    setError("");
    try {
      await onRelease();
      setConfirmRelease(false);
    } catch (e) {
      setError(errorMessage(e));
      setConfirmRelease(false);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Điều chỉnh phân bổ</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {editable ? "Đổi thiết bị thay thế hoặc dời khung giờ; hệ thống kiểm tra lại xung đột sau khi lưu." : "Phân bổ ở trạng thái này không thể chỉnh sửa."}
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}
        <Stack spacing={2}>
          <TextField select size="small" id="resolveResource" label="Tài nguyên" value={resourceId} disabled={!editable || busy}
            onChange={(event) => setResourceId(event.target.value)}
            helperText={resource ? `Tổng hiện có: ${resource.totalQuantity} · ${resource.typeLabel}` : " "}>
            {resources.map((entry) => <MenuItem key={entry.resourceId} value={String(entry.resourceId)}>{entry.name} ({entry.totalQuantity})</MenuItem>)}
          </TextField>
          <Stack direction="row" spacing={1}>
            <TextField size="small" id="resolveStart" label="Bắt đầu" type="time" value={start} disabled={!editable || busy}
              onChange={(event) => setStart(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} fullWidth />
            <TextField size="small" id="resolveEnd" label="Kết thúc" type="time" value={end} disabled={!editable || busy}
              onChange={(event) => setEnd(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} fullWidth />
          </Stack>
          <TextField size="small" id="resolveQuantity" label="Số lượng" inputMode="numeric" value={quantity} disabled={!editable || busy}
            onChange={(event) => setQuantity(String(event.target.value).replace(/[^0-9]/g, ""))}
            helperText={`Ngày ${dateVi(allocation.eventDate)}`} />
          {editable && (
            <Button variant="contained" startIcon={<SaveIcon />} onClick={save} disabled={busy} id="saveResolveBtn">
              {busy ? "Đang lưu…" : "Lưu thay đổi"}
            </Button>
          )}
          {allocation.status !== "Released" && allocation.status !== "PendingException" && (
            <Button color="error" variant="outlined" startIcon={<UndoIcon />} onClick={() => setConfirmRelease(true)} disabled={busy}>
              Thu hồi phân bổ
            </Button>
          )}
        </Stack>
      </CardContent>

      <Dialog open={confirmRelease} onClose={() => setConfirmRelease(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Thu hồi phân bổ tài nguyên</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Phân bổ {allocation.resourceName} cho {allocation.projectCode} sẽ được giải phóng và không còn chiếm công suất.
            Hành động này không thể hoàn tác.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmRelease(false)} disabled={busy}>Hủy</Button>
          <Button color="error" onClick={release} disabled={busy} id="confirmReleaseBtn">Thu hồi</Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}

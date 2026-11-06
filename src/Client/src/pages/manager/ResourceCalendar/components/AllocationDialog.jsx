import { useEffect, useState } from "react";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, List, ListItem, ListItemText, MenuItem, Stack, TextField } from "@mui/material";
import { dateTimeVi } from "../../../../utils/format";

const toLocalInput = (iso) => {
  if (!iso) return "";
  const d = new Date(iso);
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
};

// Allocate / reschedule / substitute dialog (M-21, UC-25). On conflict the API answers the clashing allocations (MSG08);
// the Manager changes time or resource and saves again, or requests an Admin exception (UC-47).
export default function AllocationDialog({ open, resources, projects, initial, conflict, busy, onClose, onSave, onRequestException }) {
  const [form, setForm] = useState({});
  const [showErrors, setShowErrors] = useState(false);
  const [note, setNote] = useState("");
  useEffect(() => {
    if (!open) return;
    setForm({
      resourceId: initial?.resourceId ?? "", projectCode: initial?.projectCode ?? "",
      startTime: toLocalInput(initial?.startTime), endTime: toLocalInput(initial?.endTime), quantity: initial?.quantity ?? 1,
    });
    setShowErrors(false);
    setNote("");
  }, [open, initial]);
  const resource = resources.find((r) => r.resourceId === Number(form.resourceId));
  const errors = {};
  if (!form.resourceId) errors.resourceId = "Trường Tài nguyên là bắt buộc.";
  if (!form.projectCode) errors.projectCode = "Trường Dự án là bắt buộc.";
  if (!form.startTime) errors.startTime = "Trường Bắt đầu là bắt buộc.";
  if (!form.endTime) errors.endTime = "Trường Kết thúc là bắt buộc.";
  else if (form.startTime && form.endTime <= form.startTime) errors.endTime = "Giờ kết thúc phải sau giờ bắt đầu.";
  if (!(Number(form.quantity) > 0)) errors.quantity = "Số lượng phải là số nguyên dương.";
  else if (resource && Number(form.quantity) > resource.totalQuantity) errors.quantity = `Tối đa ${resource.totalQuantity}.`;
  const err = (k) => (showErrors ? errors[k] : undefined);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const body = () => ({
    resourceId: Number(form.resourceId), projectCode: form.projectCode, quantity: Number(form.quantity),
    startTime: new Date(form.startTime).toISOString(), endTime: new Date(form.endTime).toISOString(),
  });
  const save = () => { setShowErrors(true); if (Object.keys(errors).length === 0) onSave(body()); };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{initial?.allocationId ? "Đổi giờ / đổi tài nguyên" : "Phân bổ tài nguyên"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }} id="allocationForm">
          <TextField select label="Tài nguyên" required value={form.resourceId ?? ""} onChange={(e) => set({ resourceId: e.target.value })}
            error={!!err("resourceId")} helperText={err("resourceId") ?? (resource ? `Tổng số: ${resource.totalQuantity}` : " ")} slotProps={{ htmlInput: { "data-field": "resource" } }}>
            {resources.map((r) => <MenuItem key={r.resourceId} value={r.resourceId}>{r.type} · {r.name}</MenuItem>)}
          </TextField>
          <TextField select label="Dự án" required value={form.projectCode ?? ""} onChange={(e) => set({ projectCode: e.target.value })} disabled={!!initial?.allocationId}
            error={!!err("projectCode")} helperText={err("projectCode") ?? "Sảnh lấy theo dự án."} slotProps={{ htmlInput: { "data-field": "project" } }}>
            {projects.map((p) => <MenuItem key={p.id} value={p.id}>{p.id} · {p.coupleName}</MenuItem>)}
          </TextField>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField type="datetime-local" label="Bắt đầu" required fullWidth value={form.startTime ?? ""} onChange={(e) => set({ startTime: e.target.value })}
              error={!!err("startTime")} helperText={err("startTime") ?? " "} slotProps={{ inputLabel: { shrink: true }, htmlInput: { "data-field": "start" } }} />
            <TextField type="datetime-local" label="Kết thúc" required fullWidth value={form.endTime ?? ""} onChange={(e) => set({ endTime: e.target.value })}
              error={!!err("endTime")} helperText={err("endTime") ?? " "} slotProps={{ inputLabel: { shrink: true }, htmlInput: { "data-field": "end" } }} />
          </Stack>
          <TextField type="number" label="Số lượng" required value={form.quantity ?? ""} onChange={(e) => set({ quantity: e.target.value })}
            error={!!err("quantity")} helperText={err("quantity") ?? " "} slotProps={{ htmlInput: { min: 1, "data-field": "quantity" } }} />
          {conflict?.conflicts?.length > 0 && (
            <Alert severity="error" id="conflictAlert">
              {conflict.message}
              <List dense>
                {conflict.conflicts.map((c, i) => (
                  <ListItem key={i} disableGutters><ListItemText primary={`${c.projectCode}: ${c.quantity} đơn vị`} secondary={`${dateTimeVi(c.startTime)} – ${dateTimeVi(c.endTime)}`} /></ListItem>
                ))}
              </List>
              Đổi giờ hoặc chọn tài nguyên khác rồi lưu lại. Nếu không thể, gửi yêu cầu ngoại lệ tới Admin.
            </Alert>
          )}
          {conflict?.conflicts?.length > 0 && !initial?.allocationId && (
            <TextField label="Ghi chú yêu cầu ngoại lệ" multiline minRows={2} value={note} onChange={(e) => setNote(e.target.value)}
              helperText={`${note.length}/300`} error={note.length > 300} slotProps={{ htmlInput: { "data-field": "exceptionNote" } }} />
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        {conflict?.conflicts?.length > 0 && !initial?.allocationId && (
          <Button color="warning" disabled={busy || !note.trim() || note.length > 300} onClick={() => onRequestException({ ...body(), note: note.trim() })}>
            Yêu cầu ngoại lệ
          </Button>
        )}
        <Button variant="contained" onClick={save} disabled={busy}>Lưu</Button>
      </DialogActions>
    </Dialog>
  );
}

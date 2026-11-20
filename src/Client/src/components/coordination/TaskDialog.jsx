import { useEffect, useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, Switch, TextField } from "@mui/material";
import { DEPARTMENTS } from "./departments";

const pad = (n) => String(n).padStart(2, "0");
const toInput = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
// Default deadline of a new task: 17:00 the day before the event.
const defaultDeadline = (eventDate) => {
  const [y, m, d] = String(eventDate).slice(0, 10).split("-").map(Number);
  return toInput(new Date(y, m - 1, d - 1, 17, 0));
};
const empty = (department, eventDate) => ({ taskName: "", department: department || "Decor", deadline: defaultDeadline(eventDate), photoRequired: true, note: "" });

// Add a task by hand (e.g. "Dựng rạp ngoài sân nhà gái") or edit one: department, deadline, whether proof is required, note.
// `task`: undefined = closed, null = new task, object = edit that task.
export default function TaskDialog({ task, department, eventDate, busy, onClose, onSave }) {
  const open = task !== undefined;
  const [form, setForm] = useState(empty(department, eventDate));
  useEffect(() => {
    if (!open) return;
    setForm(task
      ? { taskName: task.taskName, department: task.department, deadline: toInput(new Date(task.deadline)), photoRequired: task.photoRequired, note: task.note ?? "" }
      : empty(department, eventDate));
  }, [open, task, department, eventDate]);
  const set = (key) => (e) => setForm({ ...form, [key]: e.target.value });
  const valid = form.taskName.trim().length >= 3 && form.deadline;
  const save = () => onSave({ ...form, taskName: form.taskName.trim(), deadline: new Date(form.deadline).toISOString() });

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{task ? "Sửa nhiệm vụ điều phối" : "Thêm nhiệm vụ điều phối"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label="Tên nhiệm vụ" required autoFocus value={form.taskName} onChange={set("taskName")}
            placeholder="Ví dụ: Dựng rạp ngoài sân nhà gái" slotProps={{ htmlInput: { maxLength: 200 } }} />
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField select label="Bộ phận" value={form.department} onChange={set("department")} sx={{ flex: 1 }}>
              {Object.entries(DEPARTMENTS).map(([k, v]) => <MenuItem key={k} value={k}>{v.full}</MenuItem>)}
            </TextField>
            <TextField label="Hạn hoàn thành" type="datetime-local" required value={form.deadline} onChange={set("deadline")} sx={{ flex: 1 }}
              slotProps={{ inputLabel: { shrink: true } }} helperText="Từ sau ngày khóa đến hết ngày cưới" />
          </Stack>
          <FormControlLabel control={<Switch checked={form.photoRequired} onChange={(e) => setForm({ ...form, photoRequired: e.target.checked })} />}
            label="Bắt buộc ảnh hoặc tài liệu khi hoàn thành" />
          <TextField label="Ghi chú" multiline minRows={2} value={form.note} onChange={set("note")} slotProps={{ htmlInput: { maxLength: 500 } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" disabled={busy || !valid} onClick={save}>{task ? "Lưu" : "Thêm nhiệm vụ"}</Button>
      </DialogActions>
    </Dialog>
  );
}

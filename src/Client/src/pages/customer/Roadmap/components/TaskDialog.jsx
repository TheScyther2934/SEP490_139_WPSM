import { useEffect, useState } from "react";
import { Alert, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { dateTimeVi, dateVi } from "../../../../utils/format";

// C-04: task detail. Only the customer's own unfinished tasks can be confirmed (UC-06).
export default function TaskDialog({ task, onClose, onComplete }) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => { setError(""); setSaving(false); }, [task?.taskId]);

  const mine = task?.assigneeRole === "Customer";
  const complete = async () => {
    setSaving(true);
    setError("");
    const message = await onComplete(task);
    setSaving(false);
    if (message) setError(message);
  };

  return (
    <Dialog open={!!task} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      {task && (
        <>
          <DialogTitle>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <Chip size="small" label={task.milestone} />
              <Chip size="small" color={mine ? "primary" : "default"} label={mine ? "Của bạn" : "Ban điều phối"} />
            </Stack>
            <Typography variant="h6" sx={{ mt: 1 }}>{task.title}</Typography>
          </DialogTitle>
          <DialogContent>
            <Stack spacing={1}>
              <Typography>Hạn: <b>{dateVi(task.dueDate)}</b></Typography>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}><Typography>Trạng thái:</Typography><StatusChip kind="task" status={task.status} /></Stack>
              <Typography>Phụ trách: {mine ? "Khách hàng" : "Ban điều phối"}</Typography>
              {task.completedAt && <Typography color="success.main">Hoàn thành lúc {dateTimeVi(task.completedAt)}</Typography>}
              {error && <Alert severity="error">{error}</Alert>}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose} disabled={saving}>Đóng</Button>
            {mine && task.status !== "Done" && (
              <Button id="dlg-complete-btn" variant="contained" onClick={complete} disabled={saving}>{saving ? "Đang xử lý…" : "Xác nhận hoàn thành"}</Button>
            )}
          </DialogActions>
        </>
      )}
    </Dialog>
  );
}

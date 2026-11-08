import { useEffect } from "react";
import { useState } from "react";
import { useDispatch } from "react-redux";
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider, ListSubheader, MenuItem, Stack,
  Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import SaveIcon from "@mui/icons-material/Save";
import { conflictOf, resourceCalendarService } from "../../../../services/resourceCalendarService";
import { notify } from "../../../../store/notifySlice";
import { dateVi, errorMessage, slotLabel, timeVi } from "../../../../utils/format";
import { hhmm, toIsoUtc } from "../calendarRange";

const toMinutes = (value) => {
  const [hours, minutes] = String(value ?? "").split(":").map(Number);
  return (hours || 0) * 60 + (minutes || 0);
};

const slotOf = (calendar, project) =>
  calendar?.halls.find((hall) => hall.hallId === project.hallId)?.slots.find((slot) => slot.slotId === project.slotId) ?? null;

// M-21 "Phân bổ tài nguyên" dialog. A save that exceeds the resource capacity returns 409 with the
// clashing events (GBR-09); the Manager then substitutes equipment, reschedules, or asks the Admin
// for a special exception (UC-47).
export default function AllocationDialog({ open, calendar, initial, onClose, onSaved }) {
  const dispatch = useDispatch();
  const [resourceId, setResourceId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [start, setStart] = useState("17:00");
  const [end, setEnd] = useState("21:30");
  const [quantity, setQuantity] = useState("1");
  const [conflict, setConflict] = useState(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!open || !calendar) return;
    setConflict(null);
    setNote("");
    setBusy(false);
    setError("");
    const selected = calendar.projects.find((project) => project.projectId === Number(initial?.projectId)) ?? null;
    setProjectId(selected ? String(selected.projectId) : "");
    setResourceId(initial?.resourceId ? String(initial.resourceId) : "");
    setQuantity("1");
    const slot = selected ? slotOf(calendar, selected) : null;
    setStart(slot ? hhmm(slot.startTime) : "17:00");
    setEnd(slot ? hhmm(slot.endTime) : "21:30");
  }, [open, calendar, initial]);

  const project = calendar?.projects.find((entry) => entry.projectId === Number(projectId)) ?? null;
  const resource = calendar?.resources.find((entry) => entry.resourceId === Number(resourceId)) ?? null;
  const lane = calendar?.lanes.find((entry) => entry.resourceId === Number(resourceId)) ?? null;
  const usage = project && lane ? lane.usages.find((entry) => entry.date === project.eventDate) : null;
  const committed = usage ? usage.reservedQuantity + usage.approvedQuantity : 0;
  const pending = usage?.pendingQuantity ?? 0;
  const remaining = resource ? Math.max(0, resource.totalQuantity - committed) : 0;

  const pickProject = (value) => {
    setProjectId(value);
    setConflict(null);
    setError("");
    const selected = calendar.projects.find((entry) => entry.projectId === Number(value));
    const slot = selected ? slotOf(calendar, selected) : null;
    if (slot) {
      setStart(hhmm(slot.startTime));
      setEnd(hhmm(slot.endTime));
    }
  };

  const validation = () => {
    if (!project) return "Chọn dự án tiệc cưới cần phân bổ.";
    if (!resource) return "Chọn tài nguyên cần phân bổ.";
    const value = Number(quantity);
    if (!Number.isInteger(value) || value <= 0) return "Số lượng phải là số nguyên lớn hơn 0.";
    if (value > resource.totalQuantity) return `Số lượng vượt quá tổng ${resource.name} hiện có (${resource.totalQuantity}).`;
    if (toMinutes(end) <= toMinutes(start)) return "Giờ kết thúc phải sau giờ bắt đầu.";
    return "";
  };

  const save = async ({ requestException = false } = {}) => {
    setError("");
    const message = validation();
    if (message) {
      setError(message);
      return;
    }
    if (requestException && !note.trim()) {
      setError("Nhập lý do gửi Admin để xin ngoại lệ.");
      return;
    }
    setBusy(true);
    try {
      const detail = await resourceCalendarService.create({
        resourceId: resource.resourceId,
        projectId: project.projectId,
        startTime: toIsoUtc(project.eventDate, start),
        endTime: toIsoUtc(project.eventDate, end),
        quantity: Number(quantity),
        requestException,
        exceptionNote: requestException ? note.trim() : null,
      });
      dispatch(notify(requestException ? "Đã gửi yêu cầu ngoại lệ tới Admin." : "Đã lưu phân bổ tài nguyên."));
      onSaved(detail);
    } catch (e) {
      const payload = conflictOf(e);
      if (payload) {
        setConflict(payload);
        setError("");
      } else {
        setError(errorMessage(e));
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="md">
      <DialogTitle>Phân bổ tài nguyên tiệc cưới</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}

        {conflict && (
          <Alert severity="error" sx={{ mb: 2 }}>
            <Typography fontWeight={700}>Cảnh báo xung đột thiết bị:</Typography>
            <Typography variant="body2" sx={{ mb: 1 }}>
              {conflict.resourceName} chỉ có <strong>{conflict.totalQuantity}</strong>, đã giữ <strong>{conflict.otherQuantity}</strong> trong khung giờ này —
              thiếu <strong>{conflict.shortage}</strong> so với yêu cầu {conflict.requestedQuantity}.
            </Typography>
            <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
              <TableHead>
                <TableRow><TableCell>Dự án đang giữ</TableCell><TableCell>Khung giờ</TableCell><TableCell align="center">Số lượng</TableCell></TableRow>
              </TableHead>
              <TableBody>
                {conflict.clashing.map((entry) => (
                  <TableRow key={entry.allocationId}>
                    <TableCell>{entry.projectCode} · {entry.coupleName}</TableCell>
                    <TableCell>{timeVi(entry.startTime)} – {timeVi(entry.endTime)}</TableCell>
                    <TableCell align="center">{entry.quantity}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <TextField sx={{ mt: 1.5, bgcolor: "background.paper" }} fullWidth size="small" id="allocationExceptionNote"
              label="Lý do gửi Admin (bắt buộc khi xin ngoại lệ)" value={note} onChange={(event) => setNote(event.target.value)}
              helperText={`${note.length}/300 ký tự`} slotProps={{ htmlInput: { maxLength: 300 } }} />
          </Alert>
        )}

        <Stack spacing={2} sx={{ mt: conflict ? 0 : 1 }}>
          <TextField select id="allocationProject" label="Dự án tiệc cưới thụ hưởng" value={projectId} onChange={(event) => pickProject(event.target.value)}
            helperText={project ? `${project.hallName} · Ca ${slotLabel(project.slotName)} · ${dateVi(project.eventDate)}` : "Chọn dự án đã có lịch sảnh."}>
            {calendar?.projects.map((entry) => (
              <MenuItem key={entry.projectId} value={String(entry.projectId)}>
                {entry.code} — {entry.coupleName} ({entry.hallName} · {dateVi(entry.eventDate)})
              </MenuItem>
            ))}
          </TextField>

          <TextField select id="allocationResource" label="Tài nguyên yêu cầu" value={resourceId}
            onChange={(event) => { setResourceId(event.target.value); setConflict(null); setError(""); }}
            helperText={resource ? `Tổng hiện có: ${resource.totalQuantity} · ${resource.typeLabel}` : "Chọn thiết bị hoặc nhân sự cần phân bổ."}>
            {calendar?.types.flatMap((type) => [
              <ListSubheader key={`type-${type.type}`}>{type.label}</ListSubheader>,
              ...calendar.resources.filter((entry) => entry.type === type.type)
                .map((entry) => <MenuItem key={entry.resourceId} value={String(entry.resourceId)}>{entry.name} (hiện có: {entry.totalQuantity})</MenuItem>),
            ])}
          </TextField>

          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" } }}>
            <TextField label="Ngày diễn ra tiệc" value={project ? dateVi(project.eventDate) : "—"} slotProps={{ htmlInput: { readOnly: true } }} />
            <TextField id="allocationStart" label="Giờ bắt đầu sử dụng" type="time" value={start}
              onChange={(event) => { setStart(event.target.value); setConflict(null); }} slotProps={{ inputLabel: { shrink: true } }} />
            <TextField id="allocationEnd" label="Giờ kết thúc bàn giao" type="time" value={end}
              onChange={(event) => { setEnd(event.target.value); setConflict(null); }} slotProps={{ inputLabel: { shrink: true } }}
              helperText="Ràng buộc: kết thúc sau giờ bắt đầu" />
          </Box>

          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 2fr" } }}>
            <TextField id="allocationQuantity" label="Số lượng yêu cầu" inputMode="numeric" value={quantity}
              onChange={(event) => { setQuantity(String(event.target.value).replace(/[^0-9]/g, "")); setConflict(null); }}
              helperText={resource ? `Còn lại khả dụng: ${remaining} (đã dùng ${committed}/${resource.totalQuantity})` : " "} />
            <Box sx={{ alignSelf: "center" }}>
              {pending > 0 && <Typography variant="caption" color="warning.main">{pending} đơn vị đang chờ Admin quyết định.</Typography>}
              {resource && remaining === 0 && <Typography variant="caption" color="error.main">Tài nguyên đã dùng hết trong ngày này.</Typography>}
            </Box>
          </Box>

          <Divider />
          <Typography variant="caption" color="text.secondary">
            Hệ thống kiểm tra GBR-09: không phân bổ vượt tổng công suất trong khung giờ chồng lấn. Nếu không thể đổi thiết bị,
            gửi yêu cầu ngoại lệ để Admin quyết định (A-18).
          </Typography>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy bỏ</Button>
        {conflict ? (
          <>
            <Button onClick={() => { setConflict(null); setNote(""); }} disabled={busy}>Chọn thiết bị khác</Button>
            <Button variant="contained" color="warning" onClick={() => save({ requestException: true })} disabled={busy || !note.trim()} id="requestAllocationExceptionBtn">
              Gửi yêu cầu ngoại lệ
            </Button>
          </>
        ) : (
          <Button variant="contained" startIcon={<SaveIcon />} onClick={() => save()} disabled={busy} id="saveAllocationBtn">
            {busy ? "Đang lưu…" : "Lưu phân bổ tài nguyên"}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

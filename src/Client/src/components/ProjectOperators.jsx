import { useState } from "react";
import {
  Accordion, AccordionDetails, AccordionSummary, Alert, Avatar, Box, Button, Card, CardContent, Chip, Dialog, DialogActions,
  DialogContent, DialogTitle, IconButton, List, ListItem, ListItemAvatar, ListItemText, MenuItem, Stack, TextField, Tooltip, Typography,
} from "@mui/material";
import PersonAddIcon from "@mui/icons-material/PersonAdd";
import PersonRemoveIcon from "@mui/icons-material/PersonRemove";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import DataState from "./DataState";
import TextDialog from "./TextDialog";
import { useAsync } from "../hooks/useAsync";
import { useAction } from "../hooks/useAction";
import { projectService } from "../services/projectService";
import { dateTimeVi, initials } from "../utils/format";

const ROLE = { Primary: "Manager chính (người tạo)", CoManager: "Đồng điều hành" };

// Who operates the project. The Primary Manager (creator) or an Admin adds co-managers with a reason and can remove them;
// removed co-managers stay in the history so it is always clear who operated the event and when.
export default function ProjectOperators({ projectCode, onChanged }) {
  const { data, loading, error, reload, setData } = useAsync(() => projectService.getProjectManagers(projectCode), [projectCode]);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState(null);
  const { busy, run } = useAction();

  const remove = async (reason) => {
    const view = await run(() => projectService.removeProjectManager(projectCode, removing.projectManagerId, reason), `Đã gỡ quyền điều hành của ${removing.name}.`);
    if (view) { setData(view); setRemoving(null); onChanged?.(); }
  };

  const active = data?.managers.filter((m) => m.active) ?? [];
  const history = data?.managers.filter((m) => !m.active) ?? [];
  return (
    <Card id="projectOperators">
      <CardContent>
        <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1} sx={{ mb: 1 }}>
          <Box>
            <Typography variant="h6">Người điều hành</Typography>
            <Typography variant="body2" color="text.secondary">
              {data?.canManage ? "Bạn có thể ủy quyền thêm Manager đồng điều hành khi cần." : "Chỉ Manager tạo sự kiện hoặc Admin mới thay đổi được danh sách này."}
            </Typography>
          </Box>
          {data?.canManage && (
            <Button variant="outlined" startIcon={<PersonAddIcon />} onClick={() => setAdding(true)} id="addOperator" sx={{ flexShrink: 0, whiteSpace: "nowrap" }}>Ủy quyền</Button>
          )}
        </Stack>
        <DataState loading={loading} error={error} onRetry={reload}>
          <List dense>
            {active.map((m) => (
              <ListItem key={`${m.projectManagerId}-${m.managerId}`} disableGutters data-operator={m.email}
                secondaryAction={data.canManage && m.role === "CoManager" && (
                  <Tooltip title="Gỡ quyền điều hành">
                    <IconButton edge="end" onClick={() => setRemoving(m)} aria-label={`Gỡ ${m.name}`}><PersonRemoveIcon /></IconButton>
                  </Tooltip>
                )}>
                <ListItemAvatar><Avatar sx={{ bgcolor: m.role === "Primary" ? "primary.main" : "secondary.main" }}>{initials(m.name)}</Avatar></ListItemAvatar>
                <ListItemText
                  primary={<Stack direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap"><span>{m.name}</span>
                    <Chip size="small" label={ROLE[m.role]} color={m.role === "Primary" ? "primary" : "default"} variant="outlined" /></Stack>}
                  secondary={`${m.email} · từ ${dateTimeVi(m.assignedAt)}${m.assignedByName && m.role === "CoManager" ? ` · ủy quyền bởi ${m.assignedByName}` : ""}${m.reason && m.role === "CoManager" ? ` · lý do: ${m.reason}` : ""}`} />
              </ListItem>
            ))}
          </List>
          {history.length > 0 && (
            <Accordion disableGutters elevation={0} sx={{ bgcolor: "transparent", "&:before": { display: "none" } }}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ px: 0 }}>
                <Typography variant="body2" color="text.secondary">Lịch sử ủy quyền ({history.length})</Typography>
              </AccordionSummary>
              <AccordionDetails sx={{ px: 0 }}>
                {history.map((m) => (
                  <Typography key={m.projectManagerId} variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                    <b>{m.name}</b> điều hành {dateTimeVi(m.assignedAt)} → {dateTimeVi(m.removedAt)}
                    {m.removedByName ? ` · gỡ bởi ${m.removedByName}` : ""}{m.removalReason ? ` · ${m.removalReason}` : ""}
                  </Typography>
                ))}
              </AccordionDetails>
            </Accordion>
          )}
        </DataState>
      </CardContent>
      {adding && (
        <AddOperatorDialog projectCode={projectCode} onClose={() => setAdding(false)}
          onAdded={(view) => { setData(view); setAdding(false); onChanged?.(); }} />
      )}
      <TextDialog open={!!removing} title={`Gỡ quyền điều hành của ${removing?.name ?? ""}?`} color="error" confirmText="Gỡ quyền"
        description="Người này sẽ không còn truy cập và thao tác trên sự kiện. Lịch sử điều hành vẫn được giữ lại."
        label="Lý do gỡ quyền" required busy={busy} onClose={() => setRemoving(null)} onConfirm={remove} />
    </Card>
  );
}

function AddOperatorDialog({ projectCode, onClose, onAdded }) {
  const candidates = useAsync(() => projectService.getManagerCandidates(projectCode), [projectCode]);
  const [managerId, setManagerId] = useState("");
  const [reason, setReason] = useState("");
  const [touched, setTouched] = useState(false);
  const { busy, run } = useAction();
  const reasonError = touched && reason.trim().length < 5 ? "Nhập lý do ủy quyền (ít nhất 5 ký tự)." : "";

  const submit = async () => {
    setTouched(true);
    if (!managerId || reason.trim().length < 5) return;
    const view = await run(() => projectService.addProjectManager(projectCode, Number(managerId), reason.trim()), "Đã ủy quyền đồng điều hành.");
    if (view) onAdded(view);
  };

  return (
    <Dialog open onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Ủy quyền đồng điều hành {projectCode}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Manager được ủy quyền sẽ thấy sự kiện trong danh sách của mình và thao tác như người điều hành. Mọi thao tác đều được ghi vào nhật ký.
        </Typography>
        <DataState loading={candidates.loading} error={candidates.error} onRetry={candidates.reload}>
          {candidates.data?.length === 0 ? (
            <Alert severity="info">Không còn Manager đang hoạt động nào khác để ủy quyền.</Alert>
          ) : (
            <Stack spacing={2}>
              <TextField select label="Manager được ủy quyền" value={managerId} onChange={(e) => setManagerId(e.target.value)} required
                error={touched && !managerId} helperText={touched && !managerId ? "Chọn một Manager." : ""} id="operatorCandidate">
                {(candidates.data ?? []).map((c) => <MenuItem key={c.id} value={String(c.id)}>{c.name}</MenuItem>)}
              </TextField>
              <TextField label="Lý do ủy quyền" value={reason} onChange={(e) => setReason(e.target.value)} required multiline minRows={3}
                error={!!reasonError} helperText={reasonError || `${reason.length}/500 · ví dụ: nghỉ phép, hỗ trợ ngày cưới đông khách`}
                slotProps={{ htmlInput: { maxLength: 500, "data-field": "operatorReason" } }} />
            </Stack>
          )}
        </DataState>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" onClick={submit} disabled={busy || !candidates.data?.length} data-action="confirm">Ủy quyền</Button>
      </DialogActions>
    </Dialog>
  );
}

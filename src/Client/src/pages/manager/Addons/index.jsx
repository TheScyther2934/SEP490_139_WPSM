import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert, Box, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, MenuItem, Stack, Table, TableBody,
  TableCell, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import NotificationsActiveIcon from "@mui/icons-material/NotificationsActive";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import TextDialog from "../../../components/TextDialog";
import { useProject } from "../../../hooks/useProject";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { eventFlowService } from "../../../services/eventFlowService";
import { loadProject } from "../../../store/projectSlice";
import { dateTimeVi, money, moneyInput } from "../../../utils/format";

const emptyLine = () => ({ catalogItemId: "", description: "", quantity: 1, unitPrice: "" });

// Addon contracts of a locked event: extra services, no deposit. The customer must confirm on their account before the
// service is carried out; confirmed addons are added to the settlement invoice. Only waiting addons can be reminded or cancelled.
export default function Addons() {
  const dispatch = useDispatch();
  const { project, catalog } = useProject();
  const [params, setParams] = useSearchParams();
  const list = useAsync(() => eventFlowService.addons(project.id), [project.id]);
  const { busy, run } = useAction();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [note, setNote] = useState("");
  const [lines, setLines] = useState([emptyLine()]);
  const [cancelling, setCancelling] = useState(null);

  useEffect(() => { if (params.get("new") === "1" && project.canAddAddon) { setOpen(true); setParams({}, { replace: true }); } }, [params, project.canAddAddon, setParams]);

  const total = lines.reduce((s, l) => s + Number(l.quantity || 0) * moneyInput(l.unitPrice), 0);
  const valid = title.trim().length >= 3 && lines.length > 0 && lines.every((l) => l.description.trim() && Number(l.quantity) > 0 && moneyInput(l.unitPrice) > 0);
  const patch = (i, values) => setLines((ls) => ls.map((l, k) => (k === i ? { ...l, ...values } : l)));
  const refresh = () => { list.reload({ silent: true }); dispatch(loadProject(project.id)); };

  const create = async () => {
    const ok = await run(() => eventFlowService.createAddon(project.id, {
      title: title.trim(), note: note.trim() || null,
      items: lines.map((l) => ({ catalogItemId: l.catalogItemId ? Number(l.catalogItemId) : null, description: l.description.trim(), quantity: Number(l.quantity), unitPrice: moneyInput(l.unitPrice) })),
    }), "Đã tạo hợp đồng phát sinh và gửi cho khách xác nhận.");
    if (ok) { setOpen(false); setTitle(""); setNote(""); setLines([emptyLine()]); refresh(); }
  };

  return (
    <Stack spacing={2}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between" }}>
        <div>
          <Typography variant="h6">Hợp đồng phát sinh</Typography>
          <Typography variant="body2" color="text.secondary">Không đặt cọc. Chỉ thực hiện và tính tiền sau khi khách bấm xác nhận trên tài khoản.</Typography>
        </div>
        <Button variant="contained" startIcon={<AddIcon />} disabled={!project.canAddAddon} onClick={() => setOpen(true)}>Tạo hợp đồng phát sinh</Button>
      </Stack>
      {!project.canAddAddon && (
        <Alert severity="info">
          {project.status === "Adjusting" ? "Sự kiện còn trong giai đoạn điều chỉnh: thay đổi trực tiếp ở mục Dịch vụ đã đặt. Phát sinh chỉ có sau khi khóa." : "Sự kiện đã tất toán, đóng hoặc hủy nên không thêm phát sinh."}
        </Alert>
      )}
      <DataState loading={list.loading} error={list.error} onRetry={list.reload} empty={!list.data?.length} emptyText="Chưa có hợp đồng phát sinh.">
        <Stack spacing={2}>
          {list.data?.map((a) => (
            <Card key={a.code}>
              <CardContent>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
                    <Typography variant="subtitle1" fontWeight={700}>{a.code}</Typography>
                    <StatusChip kind="addon" status={a.status} />
                    <Typography>{a.title}</Typography>
                  </Stack>
                  <Typography variant="h6" color="primary">{money(a.total)}</Typography>
                </Stack>
                <Typography variant="caption" color="text.secondary">
                  Tạo bởi {a.createdBy} · {dateTimeVi(a.createdAt)}
                  {a.confirmedAt && ` · Khách xác nhận ${dateTimeVi(a.confirmedAt)}`}
                  {a.status === "PendingCustomer" && a.remindedAt && ` · Nhắc lần cuối ${dateTimeVi(a.remindedAt)}`}
                </Typography>
                {a.note && <Typography variant="body2" sx={{ mt: 1 }}>{a.note}</Typography>}
                {a.cancelReason && <Alert severity="warning" sx={{ mt: 1 }}>Lý do hủy: {a.cancelReason}</Alert>}
                <Table size="small" sx={{ mt: 1 }}>
                  <TableHead><TableRow><TableCell>Nội dung</TableCell><TableCell align="right">SL</TableCell><TableCell align="right">Đơn giá</TableCell><TableCell align="right">Thành tiền</TableCell></TableRow></TableHead>
                  <TableBody>
                    {a.items.map((i, k) => (
                      <TableRow key={k}><TableCell>{i.description}</TableCell><TableCell align="right">{i.quantity}</TableCell><TableCell align="right">{money(i.unitPrice)}</TableCell><TableCell align="right">{money(i.lineTotal)}</TableCell></TableRow>
                    ))}
                  </TableBody>
                </Table>
                {a.status === "PendingCustomer" && (
                  <Stack direction="row" spacing={1} sx={{ mt: 2, justifyContent: "flex-end" }}>
                    <Button color="error" disabled={busy} onClick={() => setCancelling(a)}>Hủy phát sinh</Button>
                    <Button variant="outlined" startIcon={<NotificationsActiveIcon />} disabled={busy}
                      onClick={async () => { if (await run(() => eventFlowService.remindAddon(project.id, a.code), "Đã nhắc khách xác nhận.")) refresh(); }}>
                      Nhắc khách xác nhận
                    </Button>
                  </Stack>
                )}
              </CardContent>
            </Card>
          ))}
        </Stack>
      </DataState>

      <Dialog open={open} onClose={busy ? undefined : () => setOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>Tạo hợp đồng phát sinh · {project.id}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Tiêu đề" required value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Ví dụ: Thêm 3 bàn tiệc" />
            <TextField label="Ghi chú cho khách" multiline minRows={2} value={note} onChange={(e) => setNote(e.target.value)} />
            {lines.map((l, i) => (
              <Box key={i} sx={{ display: "grid", gap: 1, gridTemplateColumns: { md: "1.2fr 1.5fr 0.6fr 0.9fr auto" }, alignItems: "start" }}>
                <TextField select size="small" label="Dịch vụ (không bắt buộc)" value={l.catalogItemId}
                  onChange={(e) => { const c = catalog.find((x) => String(x.id) === String(e.target.value)); patch(i, { catalogItemId: e.target.value, description: l.description || c?.name || "", unitPrice: l.unitPrice || c?.basePrice || "" }); }}>
                  <MenuItem value="">Khác</MenuItem>
                  {catalog.filter((c) => c.active).map((c) => <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}
                </TextField>
                <TextField size="small" label="Nội dung" required value={l.description} onChange={(e) => patch(i, { description: e.target.value })} />
                <TextField size="small" type="number" label="SL" value={l.quantity} onChange={(e) => patch(i, { quantity: e.target.value })} slotProps={{ htmlInput: { min: 1 } }} />
                <TextField size="small" label="Đơn giá" value={l.unitPrice} onChange={(e) => patch(i, { unitPrice: e.target.value })} slotProps={{ htmlInput: { inputMode: "numeric" } }} />
                <IconButton aria-label="Bỏ dòng" color="error" disabled={lines.length === 1} onClick={() => setLines((ls) => ls.filter((_, k) => k !== i))}><DeleteIcon /></IconButton>
              </Box>
            ))}
            <Button startIcon={<AddIcon />} onClick={() => setLines((ls) => [...ls, emptyLine()])} sx={{ alignSelf: "flex-start" }}>Thêm dòng</Button>
            <Typography variant="h6" sx={{ textAlign: "right" }}>Tổng: {money(total)}</Typography>
            <Alert severity="info">Sau khi tạo, khách nhận thông báo và email để xác nhận. Chưa xác nhận thì chưa thực hiện và chưa tính tiền.</Alert>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)} disabled={busy}>Hủy</Button>
          <Button variant="contained" onClick={create} disabled={busy || !valid}>Tạo và gửi khách</Button>
        </DialogActions>
      </Dialog>

      <TextDialog open={!!cancelling} title={`Hủy ${cancelling?.code ?? ""}`} color="error" confirmText="Hủy phát sinh" busy={busy}
        label="Lý do hủy" required onClose={() => setCancelling(null)}
        onConfirm={async (reason) => { if (await run(() => eventFlowService.cancelAddon(project.id, cancelling.code, reason), "Đã hủy hợp đồng phát sinh.")) { setCancelling(null); refresh(); } }} />
    </Stack>
  );
}

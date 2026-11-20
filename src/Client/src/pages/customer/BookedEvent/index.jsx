import { useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, Table, TableBody,
  TableCell, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import LockIcon from "@mui/icons-material/Lock";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import EventStages from "../../../components/EventStages";
import ContactCard from "../../../components/ContactCard";
import InvoiceCard from "../../../components/InvoiceCard";
import StatusChip from "../../../components/StatusChip";
import StageNotes from "../../manager/ProjectHub/components/StageNotes";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { customerService } from "../../../services/customerService";
import { dateTimeVi, dateVi, money, slotLabel } from "../../../utils/format";

// The customer's booked event: stages, services (read-only: changes go through the operators), the notes they wrote,
// addon contracts to check and confirm, who to contact and the hotline, the final invoice.
export default function BookedEvent() {
  const events = useAsync(() => customerService.getEvents());
  const [code, setCode] = useState("");
  const event = useAsync(() => customerService.getEvent(code || undefined), [code]);
  const { busy, run } = useAction();
  const [confirming, setConfirming] = useState(null);
  const e = event.data;
  const pending = e?.addons.filter((a) => a.status === "PendingCustomer") ?? [];

  const confirm = async () => {
    const ok = await run(() => customerService.confirmAddon(confirming.code), "Đã xác nhận hợp đồng phát sinh.");
    if (ok) { setConfirming(null); event.reload({ silent: true }); }
  };

  return (
    <>
      <PageHeader title="Dịch vụ đã đặt" subtitle="Theo dõi dịch vụ, tiến độ và các hợp đồng phát sinh của sự kiện."
        actions={(events.data?.length ?? 0) > 1 && (
          <TextField select size="small" label="Sự kiện" value={code || e?.eventCode || ""} onChange={(ev) => setCode(ev.target.value)} sx={{ minWidth: 240 }}>
            {events.data.map((x) => <MenuItem key={x.eventCode} value={x.eventCode}>{x.eventCode} · {dateVi(x.eventDate)} · {x.statusLabel}</MenuItem>)}
          </TextField>
        )} />
      <DataState loading={event.loading} error={event.error} onRetry={event.reload} empty={!e} emptyText="Bạn chưa có sự kiện nào.">
        {e && (
          <Stack spacing={2}>
            {pending.length > 0 && (
              <Alert severity="warning">
                Có {pending.length} hợp đồng phát sinh cần bạn đối chiếu và xác nhận. Dịch vụ phát sinh chỉ được thực hiện sau khi bạn xác nhận.
              </Alert>
            )}
            {e.status === "Cancelled" && <Alert severity="error">Sự kiện đã hủy. Tiền đặt cọc không được hoàn lại theo hợp đồng.</Alert>}
            <Card>
              <CardContent>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between" }}>
                  <Box>
                    <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
                      <Typography variant="h5" color="primary">{e.eventCode}</Typography>
                      <StatusChip kind="project" status={e.status} />
                    </Stack>
                    <Typography sx={{ mt: 0.5 }}>{e.coupleName}</Typography>
                    <Typography variant="body2" color="text.secondary">{e.hall} · Ca {slotLabel(e.slot)} ({e.slotTime}) · {dateVi(e.eventDate)}</Typography>
                  </Box>
                  <Stack sx={{ textAlign: { sm: "right" } }}>
                    <Typography variant="body2" color="text.secondary">Tổng giá trị hiện tại</Typography>
                    <Typography variant="h5" color="primary">{money(e.currentTotal + e.confirmedAddonTotal)}</Typography>
                    <Typography variant="caption" color="text.secondary">Đã đặt cọc {money(e.depositAmount)}</Typography>
                  </Stack>
                </Stack>
              </CardContent>
            </Card>
            <EventStages stages={e.stages} />
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
              <Stack spacing={2}>
                <Card>
                  <CardContent>
                    <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1 }}>
                      <Typography variant="h6">Dịch vụ</Typography>
                      {!e.canAdjust && <Chip size="small" icon={<LockIcon />} label="Đã chốt" />}
                    </Stack>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                      {e.canAdjust ? `Bạn có thể đổi món, định lượng, số bàn đến ${dateVi(e.lockDate)} qua người phụ trách.` : "Dịch vụ đã chốt, chỉ theo dõi."}
                    </Typography>
                    <Table size="small">
                      <TableHead><TableRow><TableCell>Dịch vụ</TableCell><TableCell align="right">Số lượng</TableCell><TableCell align="right">Đơn giá</TableCell><TableCell align="right">Thành tiền</TableCell></TableRow></TableHead>
                      <TableBody>
                        {e.services.map((s) => (
                          <TableRow key={s.catalogItemId}>
                            <TableCell><Typography fontWeight={600}>{s.name}</Typography><Typography variant="caption" color="text.secondary">{s.category} · {s.description}</Typography></TableCell>
                            <TableCell align="right">{s.quantity} {s.unit}</TableCell><TableCell align="right">{money(s.unitPrice)}</TableCell><TableCell align="right">{money(s.lineTotal)}</TableCell>
                          </TableRow>
                        ))}
                        <TableRow><TableCell colSpan={3} align="right"><b>Tổng hợp đồng</b></TableCell><TableCell align="right"><b>{money(e.currentTotal)}</b></TableCell></TableRow>
                      </TableBody>
                    </Table>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent>
                    <Typography variant="h6" sx={{ mb: 1 }}>Hợp đồng phát sinh</Typography>
                    {e.addons.length === 0 && <Typography color="text.secondary">Chưa có hợp đồng phát sinh.</Typography>}
                    <Stack spacing={1.5}>
                      {e.addons.map((a) => (
                        <Box key={a.code} sx={{ p: 1.5, border: 1, borderColor: a.status === "PendingCustomer" ? "warning.main" : "divider", borderRadius: 1 }}>
                          <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
                            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
                              <Typography fontWeight={700}>{a.code}</Typography><StatusChip kind="addon" status={a.status} /><Typography>{a.title}</Typography>
                            </Stack>
                            <Typography fontWeight={700} color="primary">{money(a.total)}</Typography>
                          </Stack>
                          {a.items.map((i, k) => <Typography key={k} variant="body2" color="text.secondary">• {i.description}: {i.quantity} × {money(i.unitPrice)}</Typography>)}
                          {a.confirmedAt && <Typography variant="caption" color="text.secondary">Bạn đã xác nhận lúc {dateTimeVi(a.confirmedAt)}</Typography>}
                          {a.status === "PendingCustomer" && (
                            <Stack direction="row" sx={{ justifyContent: "flex-end", mt: 1 }}>
                              <Button variant="contained" onClick={() => setConfirming(a)}>Đối chiếu và xác nhận</Button>
                            </Stack>
                          )}
                        </Box>
                      ))}
                    </Stack>
                  </CardContent>
                </Card>
                <StageNotes code={e.eventCode} notes={e.notes} showForm={false} />
              </Stack>
              <Stack spacing={2}>
                <ContactCard contacts={e.contacts} hotline={e.hotline} locked={!e.canAdjust} />
                <InvoiceCard invoice={e.invoice} />
              </Stack>
            </Box>
          </Stack>
        )}
      </DataState>
      <Dialog open={!!confirming} onClose={busy ? undefined : () => setConfirming(null)} fullWidth maxWidth="sm">
        <DialogTitle>Xác nhận {confirming?.code}</DialogTitle>
        <DialogContent>
          <Typography sx={{ mb: 1 }}>{confirming?.title}</Typography>
          {confirming?.items.map((i, k) => <Typography key={k} variant="body2">• {i.description}: {i.quantity} × {money(i.unitPrice)} = {money(i.lineTotal)}</Typography>)}
          <Typography variant="h6" sx={{ mt: 1 }}>Tổng: {money(confirming?.total)}</Typography>
          <Alert severity="info" sx={{ mt: 2 }}>Khi xác nhận, dịch vụ phát sinh sẽ được thực hiện và cộng vào hóa đơn tất toán. Không cần đặt cọc.</Alert>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirming(null)} disabled={busy}>Để sau</Button>
          <Button variant="contained" onClick={confirm} disabled={busy}>Tôi đồng ý</Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

import { useEffect, useState } from "react";
import { Alert, Box, Button, Card, CardContent, Stack, TextField, Typography } from "@mui/material";
import SendIcon from "@mui/icons-material/Send";
import { money, signedMoney, errorMessage } from "../../../../utils/format";
import { rowIssue, toCommand } from "../../../../components/changeRequest/crMeta";

const digits = (text) => String(text ?? "").replace(/[^0-9]/g, "");
const toRows = (ticket) => ticket.items.map((item) => ({
  specItemId: String(item.specItemId),
  newQuantity: String(item.newQuantity),
  newUnitPrice: String(item.newUnitPrice),
}));

// The item shape rowIssue() expects: the CR item's Old* values are the frozen baseline.
const asForm = (ticket) => ({
  specItems: ticket.items.map((item) => ({
    specItemId: item.specItemId,
    name: item.itemName,
    category: item.category,
    unit: item.unit,
    description: item.description,
    quantity: item.oldQuantity,
    unitPrice: item.oldUnitPrice,
    minPrice: item.minPrice,
    maxPrice: item.maxPrice,
    hasPriceFrame: item.hasPriceFrame,
  })),
});

// M-08 counter-proposal: the Manager restates the new values; the API recalculates GBR-03 and notifies
// the Customer. Approvals reset because the terms changed (GBR-04).
export default function CounterCard({ ticket, onCounter }) {
  const [rows, setRows] = useState(() => toRows(ticket));
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => { setRows(toRows(ticket)); setNote(""); setError(""); }, [ticket]);

  const form = asForm(ticket);
  const issues = rows.map((row) => rowIssue(form, rows, row));
  const valid = issues.every((issue) => !issue);
  const changed = rows.some((row, index) => Number(row.newQuantity) !== ticket.items[index].newQuantity
    || Number(row.newUnitPrice) !== ticket.items[index].newUnitPrice);

  const update = (index, patch) => setRows((list) => list.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const submit = async () => {
    setError("");
    if (!valid) return;
    setBusy(true);
    try {
      await onCounter({ items: toCommand(rows), note: note.trim() || null });
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Typography variant="h6">Phản hồi &amp; Đàm phán của Quản lý</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Điều chỉnh giá trị đề xuất; hệ thống tính lại chênh lệch và phí phạt, đồng thời thông báo cho khách hàng.
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }} role="alert">{error}</Alert>}
        <Stack spacing={2}>
          {ticket.items.map((item, index) => {
            const row = rows[index] ?? { newQuantity: "", newUnitPrice: "" };
            const delta = Number(row.newQuantity) * Number(row.newUnitPrice) - item.oldQuantity * item.oldUnitPrice;
            return (
              <Box key={item.specItemId} sx={{ border: 1, borderColor: issues[index] ? "error.main" : "divider", borderRadius: 2, p: 1.5 }}>
                <Typography variant="subtitle2">{item.itemName}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Baseline {item.oldQuantity} {item.unit} × {money(item.oldUnitPrice)} · Đề xuất hiện tại {item.newQuantity} × {money(item.newUnitPrice)}
                </Typography>
                <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                  <TextField size="small" label="Số lượng" inputMode="numeric" value={row.newQuantity}
                    onChange={(e) => update(index, { newQuantity: digits(e.target.value) })} sx={{ width: 110 }} />
                  <TextField size="small" label="Đơn giá" inputMode="numeric" value={row.newUnitPrice}
                    onChange={(e) => update(index, { newUnitPrice: digits(e.target.value) })} sx={{ flex: 1, minWidth: 0 }} />
                </Stack>
                <Stack direction="row" spacing={1} sx={{ mt: 0.5, justifyContent: "space-between" }}>
                  <Typography variant="caption" color="text.secondary">Khung giá {money(item.minPrice)} – {money(item.maxPrice)}</Typography>
                  <Typography variant="caption" fontWeight={600} color={delta > 0 ? "error.main" : delta < 0 ? "success.main" : "text.secondary"}>
                    Chênh lệch {signedMoney(delta)}
                  </Typography>
                </Stack>
                {issues[index] && <Typography variant="caption" color="error.main" component="p">{issues[index]}</Typography>}
              </Box>
            );
          })}
          <TextField id="counterNote" label="Ghi chú gửi khách hàng" multiline minRows={2} value={note}
            onChange={(e) => setNote(e.target.value)} helperText={`${note.length}/500 ký tự`}
            slotProps={{ htmlInput: { maxLength: 500 } }} />
          <Button variant="contained" startIcon={<SendIcon />} onClick={submit} disabled={busy || !valid || !changed} id="sendCounterBtn">
            {busy ? "Đang gửi…" : "Gửi đề xuất đối ứng"}
          </Button>
          {!changed && <Typography variant="caption" color="text.secondary">Thay đổi số lượng hoặc đơn giá để gửi đề xuất đối ứng.</Typography>}
        </Stack>
      </CardContent>
    </Card>
  );
}

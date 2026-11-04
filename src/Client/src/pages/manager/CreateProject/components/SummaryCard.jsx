import { Card, CardContent, Divider, LinearProgress, Stack, Typography } from "@mui/material";
import { dateVi, money, moneyInput, slotLabel } from "../../../../utils/format";

const Row = ({ label, value }) => (
  <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between", py: 0.5 }}>
    <Typography variant="body2" color="text.secondary">{label}</Typography>
    <Typography variant="body2" fontWeight={600} sx={{ textAlign: "right", wordBreak: "break-word" }}>{value}</Typography>
  </Stack>
);

export default function SummaryCard({ form, minimum }) {
  const base = moneyInput(form.baseValue);
  const nrd = moneyInput(form.nrdAmount);
  const pct = base > 0 ? Math.round((nrd / base) * 100) : 0;
  return (
    <Card sx={{ position: { lg: "sticky" }, top: { lg: 88 } }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Tóm tắt hợp đồng</Typography>
        <Row label="Khách hàng" value={form.coupleName || "Chưa nhập"} />
        <Row label="Email" value={form.customerEmail || "—"} />
        <Row label="Sảnh" value={form.hall || "Chưa chọn"} />
        <Row label="Ngày · ca" value={`${dateVi(form.eventDate)} · ${form.slot ? `Ca ${slotLabel(form.slot)}` : "Chưa chọn ca"}`} />
        <Divider sx={{ my: 1 }} />
        <Row label="Giá trị hợp đồng" value={money(base)} />
        <Row label="NRD tối thiểu" value={minimum == null ? "Đang tải" : money(minimum)} />
        <Row label="NRD đã thu" value={money(nrd)} />
        <Row label="Còn lại" value={money(Math.max(0, base - nrd))} />
        <LinearProgress variant="determinate" value={Math.min(pct, 100)} sx={{ mt: 1.5, height: 8, borderRadius: 4 }} aria-label="Tỷ lệ NRD" />
        <Typography variant="caption" color="text.secondary">{pct}% giá trị hợp đồng</Typography>
      </CardContent>
    </Card>
  );
}

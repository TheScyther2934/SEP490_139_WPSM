import { Card, CardContent, Divider, Stack, Typography } from "@mui/material";
import { contractValue } from "../validation";
import { dateVi, money, moneyInput, slotLabel } from "../../../../utils/format";

const Row = ({ label, value }) => (
  <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between", py: 0.5 }}>
    <Typography variant="body2" color="text.secondary">{label}</Typography>
    <Typography variant="body2" fontWeight={600} sx={{ textAlign: "right", wordBreak: "break-word" }}>{value}</Typography>
  </Stack>
);

export default function SummaryCard({ form, minimum, policy }) {
  const value = contractValue(form.lines);
  const deposit = moneyInput(form.depositAmount) || minimum;
  const floor = Math.round((value * (100 - (policy?.maxDecreaseRatio ?? 30))) / 100);
  return (
    <Card sx={{ position: { lg: "sticky" }, top: { lg: 88 } }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Tóm tắt hợp đồng</Typography>
        <Row label="Khách hàng" value={form.coupleName || "Chưa nhập"} />
        <Row label="Email" value={form.customerEmail || "—"} />
        <Row label="Sảnh" value={form.hall || "Chưa chọn"} />
        <Row label="Ngày · ca" value={`${dateVi(form.eventDate)} · ${form.slot ? `Ca ${slotLabel(form.slot)}` : "Chưa chọn ca"}`} />
        <Row label="Số dịch vụ" value={form.lines.length} />
        <Divider sx={{ my: 1 }} />
        <Row label="Giá trị hợp đồng" value={money(value)} />
        <Row label="Tiền đặt cọc" value={money(deposit)} />
        <Row label="Tất toán sau ngày cưới" value={money(Math.max(0, value - deposit))} />
        <Divider sx={{ my: 1 }} />
        <Typography variant="caption" color="text.secondary" component="p">
          Trong giai đoạn điều chỉnh, tổng giá trị không được thấp hơn {money(floor)}. Dịch vụ tự khóa {policy?.lockDays ?? 10} ngày trước ngày cưới.
        </Typography>
      </CardContent>
    </Card>
  );
}

import { Box, Card, CardContent, LinearProgress, Stack, Typography } from "@mui/material";
import { dateVi, money } from "../../../../utils/format";

const Row = ({ label, value, strong }) => (
  <Stack direction="row" sx={{ justifyContent: "space-between", py: 0.5 }}>
    <Typography color="text.secondary">{label}</Typography>
    <Typography sx={{ fontWeight: strong ? 700 : 500 }}>{value}</Typography>
  </Stack>
);

// Stage 1 / 2 money: what was signed, the current total, the lowest allowed total and the deposit; key dates of stage 3.
export default function MoneyLimits({ workspace: w }) {
  const room = Math.max(0, w.currentTotal - w.minimumTotal);
  const used = w.signedValue > w.minimumTotal ? Math.min(100, ((w.signedValue - w.currentTotal) / (w.signedValue - w.minimumTotal)) * 100) : 0;
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 1 }}>Giá trị hợp đồng</Typography>
        <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { md: "1fr 1fr" } }}>
          <Box>
            <Row label="Giá trị lúc ký" value={money(w.signedValue)} />
            <Row label="Giá trị hiện tại" value={money(w.currentTotal)} strong />
            <Row label="Mức tối thiểu được phép" value={money(w.minimumTotal)} />
            <Row label="Tiền đặt cọc (không hoàn khi hủy)" value={money(w.depositAmount)} />
          </Box>
          <Box>
            <Row label="Hạn điều chỉnh / khóa dịch vụ" value={dateVi(w.lockDate)} />
            <Row label="Hạn đặt nhà cung cấp" value={dateVi(w.supplierDeadline)} />
            <Row label="Ngày cưới" value={dateVi(w.eventDate)} />
          </Box>
        </Box>
        {w.canAdjust && (
          <Box sx={{ mt: 2 }}>
            <Typography variant="body2" color="text.secondary">
              Còn được giảm tối đa <b>{money(room)}</b> (tăng thì không giới hạn).
            </Typography>
            <LinearProgress variant="determinate" value={Math.max(0, used)} color={used > 80 ? "warning" : "primary"}
              aria-label="Mức giảm đã dùng" sx={{ mt: 1, height: 8, borderRadius: 4 }} />
          </Box>
        )}
      </CardContent>
    </Card>
  );
}

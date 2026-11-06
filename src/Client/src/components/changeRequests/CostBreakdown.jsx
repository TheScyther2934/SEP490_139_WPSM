import { Alert, Card, CardContent, Divider, Stack, Typography } from "@mui/material";
import { money, signedMoney } from "../../utils/format";

// GBR-03 breakdown: price delta, T-day penalty, total impact, project total before and after.
function Row({ label, value, strong, field }) {
  return (
    <Stack direction="row" sx={{ justifyContent: "space-between", py: 0.5 }}>
      <Typography color={strong ? "text.primary" : "text.secondary"} fontWeight={strong ? 600 : 400}>{label}</Typography>
      <Typography fontWeight={strong ? 700 : 500} data-field={field}>{value}</Typography>
    </Stack>
  );
}

export default function CostBreakdown({ cost, estimate, approved }) {
  if (!cost) return null;
  return (
    <Card id="costBreakdown">
      <CardContent>
        <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
          {estimate ? "Ước tính chi phí (chưa lưu)" : "Chi tiết chi phí"}
        </Typography>
        <Row label={cost.priceDelta >= 0 ? "Chênh lệch giá (phụ thu)" : "Chênh lệch giá (giảm trừ)"} value={signedMoney(cost.priceDelta)} field="priceDelta" />
        <Row label={`Phí phạt T-${cost.tDay} (${cost.penaltyRate}%)`} value={money(cost.penaltyFee)} field="penaltyFee" />
        {cost.penaltyWaived && <Alert severity="success" sx={{ my: 1 }}>Admin đã chấp thuận miễn phí phạt.</Alert>}
        <Divider sx={{ my: 1 }} />
        <Row label="Tổng ảnh hưởng của CR" value={signedMoney(cost.totalImpact)} strong field="totalImpact" />
        <Row label={approved ? "Tổng dự án trước CR" : "Tổng dự án hiện tại"} value={money(cost.currentTotal)} />
        <Row label={approved ? "Tổng dự án sau CR" : "Tổng dự án nếu duyệt"} value={money(cost.newTotal)} strong field="newTotal" />
        {estimate && cost.penaltyRate > 0 && (
          <Alert severity="warning" sx={{ mt: 1 }}>Thay đổi muộn nằm trong khung phạt T-day. Phí phạt được tính lại theo ngày duyệt.</Alert>
        )}
      </CardContent>
    </Card>
  );
}

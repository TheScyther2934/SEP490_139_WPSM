import { Alert, Box, Card, CardContent, Chip, Divider, Stack, Typography } from "@mui/material";
import { money, signedMoney } from "../../utils/format";
import { penaltyPercent } from "./crMeta";

function Row({ label, value, tone = "text.primary", hint, strong }) {
  return (
    <Stack direction="row" spacing={1} sx={{ justifyContent: "space-between", alignItems: "flex-start" }}>
      <Typography variant="body2" color="text.secondary" sx={{ flex: 1, minWidth: 0 }}>{label}</Typography>
      <Box sx={{ textAlign: "right", flexShrink: 0, whiteSpace: "nowrap" }}>
        <Typography variant={strong ? "subtitle1" : "body2"} fontWeight={strong ? 700 : 500} color={tone}>{value}</Typography>
        {hint && <Typography variant="caption" color="text.secondary" component="p">{hint}</Typography>}
      </Box>
    </Stack>
  );
}

// C-06 cost preview (GBR-03). Numbers are an estimate computed from the values the user is typing;
// the API recalculates and stores the authoritative figures when the ticket is saved.
export default function CostPreviewPanel({ form, cost }) {
  const increase = Math.max(0, cost.priceDelta);
  const decrease = Math.min(0, cost.priceDelta);
  return (
    <Card sx={{ position: { lg: "sticky" }, top: { lg: 88 } }}>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="h6">Ước tính chi phí</Typography>
          <Chip size="small" color="secondary" label="Tạm tính" />
        </Stack>
        <Stack spacing={1.25}>
          <Row label="Giá trị hợp đồng hiện tại" value={money(cost.currentTotal)} />
          <Row label="Chênh lệch hạng mục (+)" value={money(increase)} tone="error.main" />
          <Row label="Giảm trừ hạng mục (−)" value={money(Math.abs(decrease))} tone="success.main" />
          <Row label="Phí phạt T-day" value={money(cost.penaltyFee)} tone={cost.penaltyFee > 0 ? "warning.main" : "text.primary"}
            hint={cost.inPenaltyWindow ? `${penaltyPercent(cost.penaltyRate)} trên giá trị chênh lệch` : "Ngoài khung phạt"} />
          <Divider />
          <Row label="Tổng chênh lệch dự kiến" value={signedMoney(cost.totalImpact)} strong
            tone={cost.totalImpact > 0 ? "error.main" : cost.totalImpact < 0 ? "success.main" : "text.primary"} />
          <Row label="Tổng hợp đồng dự kiến" value={money(cost.projectedTotal)} strong />
        </Stack>

        <Divider sx={{ my: 2 }} />
        <Typography variant="caption" color="text.secondary" component="p" gutterBottom>Quy tắc áp dụng phí thay đổi</Typography>
        <Typography variant="body2" color="text.secondary">
          {form?.penaltyWindow ?? "Chưa cấu hình khung phạt T-day."}
        </Typography>
        <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }}>
          Còn {form?.daysToEvent ?? 0} ngày tới ngày tổ chức ({form?.eventDate ?? "—"}). Phí phạt tính trên |chênh lệch giá trị|.
        </Typography>

        {cost.inPenaltyWindow && cost.priceDelta !== 0 && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            Yêu cầu gửi trong khung T-day sẽ bị áp phí phạt {penaltyPercent(cost.penaltyRate)} trên giá trị chênh lệch.
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}

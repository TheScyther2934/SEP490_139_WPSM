import { Box, Card, CardContent, Chip, Divider, Stack, Typography } from "@mui/material";
import { money, signedMoney } from "../../utils/format";
import { penaltyPercent } from "./crMeta";

// GBR-03 breakdown shown on M-08: item delta, T-day penalty and the resulting contract total.
export default function CrCostBreakdown({ cost }) {
  return (
    <Card>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="h6">Chi tiết tác động chi phí</Typography>
          <Chip size="small" variant="outlined" label={cost.inPenaltyWindow ? "Trong khung phạt T-day" : "Ngoài khung phạt"} />
        </Stack>
        <Stack spacing={1.25}>
          <Stack direction="row" sx={{ justifyContent: "space-between" }}>
            <Typography variant="body2" color="text.secondary">Chênh lệch giá trị hạng mục</Typography>
            <Typography fontWeight={500}>{signedMoney(cost.priceDelta)}</Typography>
          </Stack>
          <Stack direction="row" sx={{ justifyContent: "space-between" }}>
            <Typography variant="body2" color="text.secondary">Phí phạt T-day ({penaltyPercent(cost.penaltyRate)})</Typography>
            <Typography fontWeight={500} color={cost.penaltyFee > 0 ? "warning.main" : "text.primary"}>{money(cost.penaltyFee)}</Typography>
          </Stack>
          <Box sx={{ typography: "caption", color: "text.secondary" }}>{cost.penaltyWindow}</Box>
          <Divider />
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "baseline" }}>
            <Typography variant="subtitle1">Tổng chênh lệch dự kiến</Typography>
            <Typography variant="h6" color={cost.totalImpact > 0 ? "error.main" : cost.totalImpact < 0 ? "success.main" : "text.primary"}>
              {signedMoney(cost.totalImpact)}
            </Typography>
          </Stack>
          <Stack direction="row" sx={{ justifyContent: "space-between" }}>
            <Typography variant="body2" color="text.secondary">Tổng hợp đồng hiện tại</Typography>
            <Typography variant="body2">{money(cost.currentTotal)}</Typography>
          </Stack>
          <Stack direction="row" sx={{ justifyContent: "space-between" }}>
            <Typography variant="body2" color="text.secondary">Tổng hợp đồng dự kiến sau duyệt</Typography>
            <Typography variant="body2" fontWeight={600}>{money(cost.projectedTotal)}</Typography>
          </Stack>
          <Typography variant="caption" color="text.secondary">
            Còn {cost.daysToEvent} ngày tới ngày tổ chức. Phí phạt được chốt theo ngày phê duyệt cuối cùng (GBR-03).
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}

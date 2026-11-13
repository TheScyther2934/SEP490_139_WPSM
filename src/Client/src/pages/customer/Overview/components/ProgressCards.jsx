import { Link as RouterLink } from "react-router-dom";
import { Button, Card, CardContent, LinearProgress, Stack, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";
import { money } from "../../../../utils/format";

// Balance, roadmap progress and open change requests of the customer's project.
export default function ProgressCards({ dashboard }) {
  const base = dashboard.baseContractValue || 0;
  const paidPct = base > 0 ? Math.min(100, Math.round(((dashboard.totalPaid || 0) / base) * 100)) : 0;
  return (
    <Stack spacing={2}>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Dư nợ</Typography>
          <Typography variant="h5" color="primary" id="balance-outstanding">{money(dashboard.outstandingBalance)}</Typography>
          <LinearProgress variant="determinate" value={paidPct} sx={{ my: 1, height: 8, borderRadius: 4 }} aria-label="Tỷ lệ đã thanh toán" />
          <Typography variant="body2" color="text.secondary">Đã thanh toán {money(dashboard.totalPaid)} / {money(base)}</Typography>
          <Button component={RouterLink} to={PATHS.customer.payments} size="small" sx={{ mt: 1 }}>Chi tiết thanh toán</Button>
        </CardContent>
      </Card>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Tiến độ chuẩn bị</Typography>
          <Typography variant="h5" color="primary">{dashboard.roadmapProgress}%</Typography>
          <LinearProgress variant="determinate" value={dashboard.roadmapProgress} sx={{ mt: 1, height: 8, borderRadius: 4 }} aria-label="Tiến độ lộ trình" />
        </CardContent>
      </Card>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Yêu cầu thay đổi đang mở</Typography>
          <Typography variant="h5" color="primary" id="cr-count">{dashboard.openCrCount}</Typography>
          <Button component={RouterLink} to={PATHS.customer.changeRequests} size="small" sx={{ mt: 1 }}>Xem yêu cầu</Button>
        </CardContent>
      </Card>
    </Stack>
  );
}

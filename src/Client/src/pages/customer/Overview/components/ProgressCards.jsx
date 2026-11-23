import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Card, CardContent, LinearProgress, Stack, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";
import { money } from "../../../../utils/format";

// Money of the event and the addon contracts waiting for the customer's confirmation.
export default function ProgressCards({ dashboard }) {
  const total = dashboard.currentTotal || dashboard.baseContractValue || 0;
  const paidPct = total > 0 ? Math.min(100, Math.round(((dashboard.totalPaid || 0) / total) * 100)) : 0;
  return (
    <Stack spacing={2}>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Còn phải thanh toán</Typography>
          <Typography variant="h5" color="primary" id="balance-outstanding">{money(dashboard.outstandingBalance)}</Typography>
          <LinearProgress variant="determinate" value={paidPct} sx={{ my: 1, height: 8, borderRadius: 4 }} aria-label="Tỷ lệ đã thanh toán" />
          <Typography variant="body2" color="text.secondary">Đã thanh toán {money(dashboard.totalPaid)} / {money(total)}</Typography>
          <Typography variant="caption" color="text.secondary" component="p">Đặt cọc khi ký, phần còn lại thanh toán một lần sau ngày cưới.</Typography>
          <Button component={RouterLink} to={PATHS.customer.payments} size="small" sx={{ mt: 1 }}>Chi tiết thanh toán</Button>
        </CardContent>
      </Card>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Hợp đồng phát sinh chờ xác nhận</Typography>
          <Typography variant="h5" color={dashboard.pendingAddonCount ? "warning.main" : "primary"} id="addon-count">{dashboard.pendingAddonCount}</Typography>
          {dashboard.pendingAddonCount > 0 && <Alert severity="warning" sx={{ mt: 1 }}>Phát sinh chỉ được thực hiện sau khi bạn xác nhận.</Alert>}
          <Button component={RouterLink} to={PATHS.customer.event} size="small" sx={{ mt: 1 }}>Xem dịch vụ đã đặt</Button>
        </CardContent>
      </Card>
    </Stack>
  );
}

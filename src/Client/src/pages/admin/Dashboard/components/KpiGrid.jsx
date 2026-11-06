import { Link as RouterLink } from "react-router-dom";
import { Box, Card, CardActionArea, CardContent, Typography } from "@mui/material";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { PATHS } from "../../../../routes/paths";
import { percent, shortMoney } from "../../../../utils/format";

// SRS 10.1: six read-only KPI cards; each opens its detail screen. Overdue and escalated turn red when not zero.
export default function KpiGrid({ data }) {
  const A = PATHS.admin;
  const cards = [
    { label: "Sự kiện sắp tới (30 ngày)", value: data.upcomingEvents },
    { label: "Công suất sảnh tháng này", value: percent(data.hallUtilization), to: A.hallUtilization },
    { label: "Doanh thu thuần tháng này", value: shortMoney(data.netRevenueThisMonth), to: A.netRevenue },
    { label: "CR đang xử lý", value: data.activeCrs, to: A.crStatistics },
    { label: "Nhiệm vụ quá hạn", value: data.overdueTasks, to: A.overdueAlerts, alert: data.overdueTasks > 0 },
    { label: "Khiếu nại chuyển cấp", value: data.escalatedComplaints, to: A.complaints, alert: data.escalatedComplaints > 0 },
  ];
  return (
    <Box id="a01Kpis" sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", md: "repeat(3, 1fr)" } }}>
      {cards.map((card) => <KpiCard key={card.label} {...card} />)}
    </Box>
  );
}

function KpiCard({ label, value, to, alert }) {
  const body = (
    <CardContent sx={{ p: 2.5, "&:last-child": { pb: 2.5 } }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="body2" color="text.secondary">{label}</Typography>
        {to && <ChevronRightIcon fontSize="small" sx={{ color: "text.disabled" }} />}
      </Box>
      <Typography sx={{ mt: 1, fontSize: 32, fontWeight: 700, lineHeight: 1.2, color: alert ? "error.main" : "secondary.main" }}>{value}</Typography>
    </CardContent>
  );
  return (
    <Card sx={{ height: "100%", borderLeft: 4, borderLeftColor: alert ? "error.main" : "primary.main" }}>
      {to ? <CardActionArea component={RouterLink} to={to} sx={{ height: "100%" }}>{body}</CardActionArea> : body}
    </Card>
  );
}

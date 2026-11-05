import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";
import { percent, shortMoney } from "../../../../utils/format";

export default function KpiGrid({ data }) {
  const A = PATHS.admin;
  const cards = [
    { label: "Sự kiện sắp tới", value: data.upcomingEvents },
    { label: "Công suất sảnh tháng này", value: percent(data.hallUtilization), to: A.hallUtilization },
    { label: "Doanh thu thuần tháng này", value: shortMoney(data.netRevenueThisMonth), to: A.netRevenue },
    { label: "CR đang xử lý", value: data.activeCrs, to: A.crStatistics },
    { label: "Nhiệm vụ quá hạn", value: data.overdueTasks, to: A.overdueAlerts, tone: data.overdueTasks ? "error" : "primary" },
    { label: "Khiếu nại chuyển cấp", value: data.escalatedComplaints, to: A.complaints, tone: data.escalatedComplaints ? "error" : "primary" },
  ];
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(3, 1fr)", lg: "repeat(6, 1fr)" }, mb: 3 }}>
      {cards.map((c) => <StatCard key={c.label} {...c} />)}
    </Box>
  );
}

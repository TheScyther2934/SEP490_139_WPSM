import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";

export default function ManagerKpis({ kpis }) {
  if (!kpis) return null;

  // 5 KPIs required by SRS §3.3
  const cards = [
    {
      label: "Dự án đang phụ trách",
      value: kpis.activeProjects ?? 0,
      to: PATHS.manager.projects,
    },
    {
      label: "Nhiệm vụ đến hạn hôm nay",
      value: kpis.tasksDueToday ?? 0,
      tone: (kpis.tasksDueToday ?? 0) > 0 ? "warning" : "primary",
    },
    {
      label: "Yêu cầu thay đổi (CR)",
      value: kpis.openChangeRequests ?? 0,
      to: PATHS.manager.projects,
    },
    {
      label: "BEO lỗi thời",
      value: kpis.staleBeos ?? 0,
      tone: (kpis.staleBeos ?? 0) > 0 ? "warning" : "primary",
    },
    {
      label: "Cảnh báo quá hạn",
      value: kpis.overdueTasksCount ?? 0,
      tone: (kpis.overdueTasksCount ?? 0) > 0 ? "error" : "primary",
      to: PATHS.manager.overdueAlerts,
    },
  ];

  return (
    <Box
      sx={{
        display: "grid",
        gap: 2,
        gridTemplateColumns: {
          xs: "1fr 1fr",
          sm: "repeat(3, 1fr)",
          md: "repeat(5, 1fr)",
        },
        mb: 3,
      }}
    >
      {cards.map((c) => (
        <StatCard key={c.label} {...c} />
      ))}
    </Box>
  );
}

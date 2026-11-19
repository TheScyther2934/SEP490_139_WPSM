import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";

export default function ManagerKpis({ kpis }) {
  if (!kpis) return null;

  const cards = [
    {
      label: "Sự kiện đang phụ trách",
      value: kpis.activeProjects ?? 0,
      to: PATHS.manager.projects,
    },
    {
      label: "Việc đến hạn hôm nay",
      value: kpis.tasksDueToday ?? 0,
      tone: (kpis.tasksDueToday ?? 0) > 0 ? "warning" : "primary",
    },
    {
      label: "Phát sinh chờ khách xác nhận",
      value: kpis.pendingAddons ?? 0,
      tone: (kpis.pendingAddons ?? 0) > 0 ? "warning" : "primary",
      to: PATHS.manager.projects,
    },
    {
      label: "Phiếu điều phối cần duyệt lại",
      value: kpis.staleBeos ?? 0,
      tone: (kpis.staleBeos ?? 0) > 0 ? "warning" : "primary",
    },
    {
      label: "Việc trễ hạn",
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

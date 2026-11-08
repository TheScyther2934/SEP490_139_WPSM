import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";

export default function ManagerKpis({ kpis }) {
  if (!kpis) return null;

  const cards = [
    {
      label: "Dự án đang hoạt động",
      value: kpis.activeProjects,
      to: PATHS.manager.projects,
    },
    {
      label: "Đang soạn đặc tả",
      value: kpis.draftingProjects,
      to: PATHS.manager.projects,
    },
    {
      label: "Tiệc trong 7 ngày",
      value: kpis.eventsIn7Days,
      to: PATHS.manager.projects,
    },
    {
      label: "CR đang mở",
      value: kpis.openChangeRequests,
      to: PATHS.manager.projects,
    },
    {
      label: "BEO lỗi thời",
      value: kpis.staleBeos,
      tone: kpis.staleBeos > 0 ? "warning" : "primary",
    },
    {
      label: "Nhiệm vụ quá hạn",
      value: kpis.overdueTasksCount,
      tone: kpis.overdueTasksCount > 0 ? "error" : "primary",
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
          md: "repeat(6, 1fr)",
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

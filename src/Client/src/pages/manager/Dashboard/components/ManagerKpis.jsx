import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";
import { daysUntil } from "../../../../utils/format";

const ACTIVE = ["Active Baseline", "Baseline Frozen", "Event Completed"];

// M-01 KPI strip, counted from the Manager's own projects (GBR-12).
export function summarize(projects) {
  const active = projects.filter((p) => ACTIVE.includes(p.status));
  return {
    active,
    drafting: active.filter((p) => p.status === "Active Baseline"),
    openCrs: active.reduce((sum, p) => sum + (p.openChangeRequests || 0), 0),
    stale: active.filter((p) => p.beoStatus === "Stale"),
    thisWeek: active.filter((p) => { const d = daysUntil(p.eventDate); return d >= 0 && d <= 7; }),
  };
}

export default function ManagerKpis({ summary }) {
  const cards = [
    { label: "Dự án đang hoạt động", value: summary.active.length, to: PATHS.manager.projects },
    { label: "Đang soạn đặc tả", value: summary.drafting.length, to: PATHS.manager.projects },
    { label: "Tiệc trong 7 ngày", value: summary.thisWeek.length },
    { label: "CR đang mở", value: summary.openCrs },
    { label: "BEO lỗi thời", value: summary.stale.length, tone: summary.stale.length ? "error" : "primary" },
  ];
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(5, 1fr)" }, mb: 3 }}>
      {cards.map((c) => <StatCard key={c.label} {...c} />)}
    </Box>
  );
}

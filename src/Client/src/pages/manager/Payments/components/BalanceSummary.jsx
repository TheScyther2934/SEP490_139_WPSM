import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { money } from "../../../../utils/format";

export default function BalanceSummary({ project }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "repeat(3, 1fr)" }, mb: 2 }}>
      <StatCard label="Tổng hợp đồng hiện tại" value={money(project.currentTotal)} />
      <StatCard label="Đặt cọc NRD" value={money(project.nrdAmount)} />
      <StatCard label="Còn phải thu" value={money(project.outstandingBalance)} tone={project.outstandingBalance > 0 ? "secondary" : "primary"} />
    </Box>
  );
}
